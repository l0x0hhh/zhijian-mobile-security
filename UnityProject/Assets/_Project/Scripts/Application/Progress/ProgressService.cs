// 说明：串行接收冻结结果并合并待保存成绩，在创建同步上下文发布快照，文件失败不修改业务结果。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zhijian.Application.Ports;
using Zhijian.Domain.Results;
using Zhijian.Domain.Sessions;

namespace Zhijian.Application.Progress
{
    public sealed class ProgressService : IProgressService
    {
        private readonly IProgressRepository repository;
        private readonly SynchronizationContext creationContext;
        private readonly object queueGate = new object();
        private readonly HashSet<Guid> pending = new HashSet<Guid>();
        private Task tail = Task.CompletedTask;
        private ProgressArchive archive = ProgressArchive.Empty;
        private ProgressSnapshot current;
        private ProgressLoadState loadState = ProgressLoadState.NotLoaded;
        private ProgressSaveState saveState = ProgressSaveState.Idle;
        private string message = string.Empty;
        private bool initialized;
        private bool disposed;
        private bool notifying;

        public ProgressSnapshot Current => Volatile.Read(ref current);
        public event Action<ProgressSnapshot> Changed;

        public ProgressService(IProgressRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            creationContext = SynchronizationContext.Current;
            current = new ProgressSnapshot(loadState, saveState, archive.Cases, archive.RecentResults, 0, message);
        }

        public Task InitializeAsync() => Enqueue(async () => { await InitializeCoreAsync().ConfigureAwait(false); return true; });

        public Task<ProgressWriteResult> RecordAsync(CaseResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.FinalState == null || result.FinalState.Phase != SessionPhase.Resolved)
                throw new ArgumentException("只能记录已经终局冻结的结果。", nameof(result));
            return Enqueue(() => RecordCoreAsync(result));
        }

        public Task RetryPendingAsync() => Enqueue(async () =>
        {
            await InitializeCoreAsync().ConfigureAwait(false);
            if (!CanWrite) { await PublishBlockedAsync().ConfigureAwait(false); return false; }
            if (pending.Count > 0) await FlushAsync(false).ConfigureAwait(false);
            return true;
        });

        public Task StartNewSaveAsync() => Enqueue(async () =>
        {
            await InitializeCoreAsync().ConfigureAwait(false);
            if (loadState != ProgressLoadState.RecoveryRequired)
                throw new InvalidOperationException("只有损坏恢复状态允许在用户确认后新建；不兼容格式禁止覆盖。");
            // 初次双坏加载时 archive 为空；运行中遇损坏时保留已验证的内存数据和待保存顺序。
            await FlushAsync(true).ConfigureAwait(false);
            return true;
        });

        private bool CanWrite => loadState == ProgressLoadState.Ready ||
            loadState == ProgressLoadState.RecoveredFromBackup;

        // 所有调用在同一 FIFO 链上执行；前一操作即使抛错也不会使后续队列失效。
        private Task<T> Enqueue<T>(Func<Task<T>> operation)
        {
            lock (queueGate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(ProgressService));
                if (notifying) throw new InvalidOperationException("Changed 通知期间不能重入存档命令，请在通知结束后调度。");
                var next = tail.ContinueWith(ignored => operation(), CancellationToken.None,
                    TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
                tail = next;
                return next;
            }
        }

        private async Task InitializeCoreAsync()
        {
            if (initialized) return;
            initialized = true;
            loadState = ProgressLoadState.Loading; message = "正在读取本地成绩。";
            await PublishAsync().ConfigureAwait(false);
            try
            {
                var loaded = await repository.LoadAsync().ConfigureAwait(false);
                if (loaded == null) throw new InvalidOperationException("仓储没有返回加载结果。");
                archive = loaded.Archive; loadState = loaded.State; message = loaded.Message;
                saveState = CanWrite ? ProgressSaveState.Idle : ProgressSaveState.Blocked;
            }
            catch (Exception error)
            {
                loadState = ProgressLoadState.Failed; saveState = ProgressSaveState.Blocked;
                ReportStorageError(error);
                message = "存档读取失败，未覆盖原文件。请稍后重新打开应用再读取。";
            }
            await PublishAsync().ConfigureAwait(false);
        }

        private async Task<ProgressWriteResult> RecordCoreAsync(CaseResult result)
        {
            await InitializeCoreAsync().ConfigureAwait(false);
            if (!CanWrite)
            {
                await PublishBlockedAsync().ConfigureAwait(false);
                return new ProgressWriteResult(result.ResultId, ProgressSaveState.Blocked,
                    message + " 此结果尚未接收，恢复可写后请重新提交原结果。");
            }
            if (archive.Contains(result.ResultId))
            {
                if (pending.Contains(result.ResultId)) await FlushAsync(false).ConfigureAwait(false);
                return new ProgressWriteResult(result.ResultId,
                    pending.Contains(result.ResultId) ? WriteFailureState : ProgressSaveState.Saved,
                    pending.Contains(result.ResultId) ? message : "该结果已保存，未重复记分或改变顺序。");
            }
            if (archive.LastSequence == long.MaxValue)
                return new ProgressWriteResult(result.ResultId, ProgressSaveState.Failed, "结果顺序已达上限，未接收新结果。");

            // 只复制结果展示数据，不保留/序列化 FinalState、案件定义或整个 CaseResult。
            var entries = result.Score.Entries.Select(item => new ScoreEntry(item.RuleId ?? string.Empty,
                item.Dimension, item.Delta, item.Reason ?? string.Empty, item.RelatedItemId ?? string.Empty,
                item.Recommendation ?? string.Empty));
            var feedback = result.Feedback.Select(item => new SavedFeedback(item.RuleId ?? string.Empty,
                item.RelatedItemId ?? string.Empty, item.Reason, item.Recommendation));
            var saved = new SavedResult(result.ResultId, result.CaseId, result.CaseTitle, result.ContentVersion,
                result.ScoringRuleVersion, archive.LastSequence + 1, result.ResolvedAtUtc.ToUniversalTime(),
                result.Outcome, result.Score.EvidenceScore, result.Score.PermissionScore,
                result.Score.DispositionScore, entries, feedback);
            archive = archive.Append(saved);
            pending.Add(saved.ResultId);
            saveState = ProgressSaveState.Pending; message = "成绩已接收，尚未保存到本机。";
            await PublishAsync().ConfigureAwait(false);
            await FlushAsync(false).ConfigureAwait(false);
            return new ProgressWriteResult(saved.ResultId,
                pending.Contains(saved.ResultId) ? WriteFailureState : ProgressSaveState.Saved, message);
        }

        private ProgressSaveState WriteFailureState => CanWrite ? ProgressSaveState.Failed : ProgressSaveState.Blocked;

        private async Task FlushAsync(bool confirmedRecovery)
        {
            saveState = ProgressSaveState.Saving; message = "正在保存成绩。";
            await PublishAsync().ConfigureAwait(false);
            try
            {
                if (confirmedRecovery) await repository.ArchiveAndCreateAsync(archive).ConfigureAwait(false);
                else await repository.SaveAsync(archive).ConfigureAwait(false);
                pending.Clear();
                loadState = ProgressLoadState.Ready; saveState = ProgressSaveState.Saved;
                message = confirmedRecovery ? "已保留原文件并建立可用存档。" : "成绩已保存。";
            }
            catch (ProgressWriteBlockedException error)
            {
                loadState = error.LoadState; saveState = ProgressSaveState.Blocked;
                message = error.Message + " 待保存结果仍保留在本次运行内。";
            }
            catch (Exception error)
            {
                saveState = ProgressSaveState.Failed;
                ReportStorageError(error);
                message = "保存失败，结果仍在本次运行内；尚未确认保存成功，可重试。";
            }
            // 通知与 IO 成功分开处理；订阅者抛错绝不会进入上面的保存失败分支。
            await PublishAsync().ConfigureAwait(false);
        }

        private Task PublishBlockedAsync()
        {
            saveState = ProgressSaveState.Blocked;
            if (string.IsNullOrEmpty(message)) message = "存档尚不可写，未修改原文件。";
            return PublishAsync();
        }

        private Task PublishAsync()
        {
            var snapshot = new ProgressSnapshot(loadState, saveState, archive.Cases, archive.RecentResults,
                pending.Count, message);
            if (creationContext == null)
            {
                Deliver(snapshot);
                return Task.CompletedTask;
            }
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                creationContext.Post(ignored =>
                {
                    try { Deliver(snapshot); }
                    catch (Exception error) { ReportNotificationError(error); }
                    finally { completion.TrySetResult(true); }
                }, null);
            }
            catch (Exception error)
            {
                // 同步上下文已停止时仍可查询保存事实，不把通知改在错误线程触发。
                Volatile.Write(ref current, snapshot);
                ReportNotificationError(error);
                completion.TrySetResult(true);
            }
            return completion.Task;
        }

        private void Deliver(ProgressSnapshot snapshot)
        {
            Action<ProgressSnapshot> handlers;
            lock (queueGate)
            {
                Volatile.Write(ref current, snapshot);
                if (disposed) return;
                notifying = true; handlers = Changed;
            }
            try
            {
                if (handlers == null) return;
                foreach (Action<ProgressSnapshot> handler in handlers.GetInvocationList())
                {
                    lock (queueGate) { if (disposed) break; }
                    try { handler(snapshot); }
                    catch (Exception error) { ReportNotificationError(error); }
                }
            }
            finally { lock (queueGate) notifying = false; }
        }

        private static void ReportStorageError(Exception error)
        {
            try { Trace.TraceError("ProgressService 存档操作失败: " + error); }
            catch (Exception) { /* 不让诊断输出干扰待保存数据。 */ }
        }

        private static void ReportNotificationError(Exception error)
        {
            try { Trace.TraceError("ProgressService Changed 通知失败（不影响存档提交）: " + error); }
            catch (Exception) { /* 诊断输出失败也不能回滚已经成功的保存。 */ }
        }

        public void Dispose()
        {
            lock (queueGate)
            {
                disposed = true;
                Changed = null;
            }
            // 已接收的串行操作继续完成；不删除文件，也不声称未落盘数据能跨进程恢复。
        }
    }
}
