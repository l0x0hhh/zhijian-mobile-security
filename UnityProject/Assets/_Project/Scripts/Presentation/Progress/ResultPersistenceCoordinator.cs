// 说明：应用生命周期保留被阻断或失败的冻结结果，恢复后重送；未落盘不声称已保存。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zhijian.Application.Ports;
using Zhijian.Application.Progress;
using Zhijian.Domain.Results;
namespace Zhijian.Presentation.Progress
{
    public sealed class ResultPersistenceCoordinator : IProgressService
    {
        private readonly IProgressService storage;
        private readonly Dictionary<Guid, CaseResult> retained=new Dictionary<Guid, CaseResult>();
        private readonly List<Guid> order=new List<Guid>();
        private readonly HashSet<Guid> accepted=new HashSet<Guid>();
        private bool disposed;
        public event Action<ProgressSnapshot> Changed;
        public ProgressSnapshot Current
        {
            get
            {
                var snapshot=storage.Current;
                var count=Math.Max(snapshot.PendingResultCount, retained.Count);
                return new ProgressSnapshot(snapshot.LoadState,
                    count > 0 && (snapshot.SaveState == ProgressSaveState.Idle || snapshot.SaveState == ProgressSaveState.Saved) ? ProgressSaveState.Pending : snapshot.SaveState,
                    snapshot.Cases, snapshot.RecentResults, count,
                    snapshot.StatusMessage + (count > 0 ? "\n有 " + count + " 条结果尚未保存，关闭应用前请重试。" : ""));
            }
        }
        public ResultPersistenceCoordinator(IProgressService storage)
        { this.storage=storage ?? throw new ArgumentNullException(nameof(storage)); storage.Changed += OnChanged; }
        public Task InitializeAsync() { Guard(); return storage.InitializeAsync(); }
        public async Task<ProgressWriteResult> RecordAsync(CaseResult result)
        {
            Guard(); if (result == null) throw new ArgumentNullException(nameof(result));
            if (!retained.ContainsKey(result.ResultId)) order.Add(result.ResultId);
            retained[result.ResultId]=result; Publish();
            try
            {
                var reply=await storage.RecordAsync(result);
                if (reply.State == ProgressSaveState.Saved) Forget(result.ResultId);
                Publish(); return reply;
            }
            catch { Publish(); throw; }
        }
        public async Task RetryPendingAsync()
        {
            Guard(); await storage.RetryPendingAsync();
            // 首次接收顺序在调用方主线程保留，重试不按存储完成时间排序。
            foreach (var id in order.ToArray()) if (retained.TryGetValue(id, out var result)) await RecordAsync(result);
            Publish();
        }
        public async Task StartNewSaveAsync()
        { Guard(); await storage.StartNewSaveAsync(); await RetryPendingAsync(); }
        private void OnChanged(ProgressSnapshot snapshot)
        {
            foreach (var result in snapshot.RecentResults) if (retained.ContainsKey(result.ResultId)) accepted.Add(result.ResultId);
            if (snapshot.SaveState == ProgressSaveState.Saved && snapshot.PendingResultCount == 0)
                foreach (var id in accepted.ToArray()) Forget(id);
            Publish();
        }
        private void Forget(Guid id) { retained.Remove(id); order.Remove(id); accepted.Remove(id); }
        private void Publish()
        {
            if (disposed) return;
            var snapshot=Current;
            var observers=Changed;
            if (observers == null) return;
            foreach (Action<ProgressSnapshot> observer in observers.GetInvocationList())
                try { observer(snapshot); } catch { /* 单个页面关闭不改变实际保存状态。 */ }
        }
        private void Guard() { if (disposed) throw new ObjectDisposedException(nameof(ResultPersistenceCoordinator)); }
        public void Dispose() { if (disposed) return; disposed=true; storage.Changed -= OnChanged; Changed=null; storage.Dispose(); }
    }
}
