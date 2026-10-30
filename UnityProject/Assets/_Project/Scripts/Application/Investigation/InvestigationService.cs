// 说明：在所有者线程串行分发调查用例，拒绝旧会话与通知重入，并保留一次冻结结果。
using System;
using System.Threading;
using System.Linq;
using Zhijian.Application.Cases;
using Zhijian.Application.Ports;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Outcomes;
using Zhijian.Domain.Sessions;

namespace Zhijian.Application.Investigation
{
    public sealed class InvestigationService : IDisposable
    {
        private readonly IInvestigationCaseRepository repository;
        private readonly OutcomeResolver resolver;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly Action<Exception> reportNotificationError;
        private InvestigationSession session;
        private CaseContent activeContent;
        private SessionSnapshot snapshot;
        private bool dispatching, disposed;
        public event Action<SessionSnapshot> Changed;
        public Exception LastNotificationError { get; private set; }
        public SessionSnapshot Current { get { AssertThread(); return snapshot; } }

        public InvestigationService(IInvestigationCaseRepository repository, OutcomeResolver resolver,
            Action<Exception> reportNotificationError = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            this.reportNotificationError = reportNotificationError;
        }
        public CommandResult Start(string caseId)
        {
            AssertThread();
            if (disposed) return Reply(DispatchStatus.Disposed);
            if (dispatching) return Reply(DispatchStatus.Busy);
            if (session != null && snapshot.State.Phase == SessionPhase.Investigating) return Reply(DispatchStatus.ActiveSession);
            if (!repository.TryGetCase(caseId, out var loaded)) return Reply(DispatchStatus.UnknownCase);
            if (!loaded.CanStartTraining) return new CommandResult(DispatchStatus.ContentBlocked, snapshot, problems: loaded.Problems);
            dispatching = true;
            try
            {
                var next = new InvestigationSession(loaded.Content.Definition, resolver);
                var nextSnapshot = new SessionSnapshot(next.CaptureState());
                session = next; activeContent = loaded.Content; snapshot = nextSnapshot;
                Publish();
                return Reply(DispatchStatus.Started);
            }
            finally { dispatching = false; }
        }
        public CommandResult Execute(InvestigationCommand command)
        {
            AssertThread();
            if (disposed) return Reply(DispatchStatus.Disposed);
            if (dispatching) return Reply(DispatchStatus.Busy);
            if (session == null) return Reply(DispatchStatus.NoSession);
            if (command == null || !Enum.IsDefined(typeof(InvestigationCommandKind), command.Kind)) return Reply(DispatchStatus.InvalidCommand);
            if (command.SessionId != session.SessionId) return Reply(DispatchStatus.StaleSession);
            dispatching = true;
            try
            {
                var previousRevision = snapshot.State.Revision;
                var previousResult = snapshot.State.Result;
                var response = Apply(command);
                snapshot = new SessionSnapshot(session.CaptureState());
                if (snapshot.State.Revision != previousRevision) Publish();
                return new CommandResult(DispatchStatus.Completed, snapshot, response,
                    producedResult: previousResult == null && snapshot.State.Result != null);
            }
            finally { dispatching = false; }
        }
        private SessionCommandResult Apply(InvestigationCommand command)
        {
            switch (command.Kind)
            {
                case InvestigationCommandKind.ViewSource: return session.ViewSource(command.ItemId);
                case InvestigationCommandKind.CollectEvidence: return session.CollectEvidence(command.ItemId);
                case InvestigationCommandKind.SelectFeatureIntent: return session.SelectFeatureIntent(command.ItemId);
                case InvestigationCommandKind.SetPermission: return session.SetPermission(command.ItemId, command.Permission);
                case InvestigationCommandKind.SelectVerification: return session.SelectVerification(command.ItemId);
                case InvestigationCommandKind.SelectRisk: return session.SelectRisk(command.ItemId);
                case InvestigationCommandKind.SelectDisposition: return SelectDisposition(command.ItemId);
                case InvestigationCommandKind.SetInstallation: return session.SetInstallation(command.Installation);
                case InvestigationCommandKind.BeginPlayback: return session.BeginPlayback(command.SessionId, command.ItemId, command.RequestId);
                case InvestigationCommandKind.CompletePlayback: return session.CompletePlayback(command.SessionId, command.ItemId, command.RequestId, command.Succeeded);
                case InvestigationCommandKind.CancelPlayback: return session.CancelPlayback(command.SessionId, command.ItemId, command.RequestId);
                case InvestigationCommandKind.BeginDetection: return session.BeginDetection(command.SessionId, command.ItemId, command.RequestId);
                case InvestigationCommandKind.CompleteDetection: return session.CompleteDetection(command.SessionId, command.ItemId, command.RequestId, command.Succeeded);
                case InvestigationCommandKind.CancelDetection: return session.CancelDetection(command.SessionId, command.ItemId, command.RequestId);
                case InvestigationCommandKind.Submit: return session.Submit(command.ExpectedRevision, command.ConfirmMissingEvidence);
                case InvestigationCommandKind.Abort: return session.Abort();
                default: throw new ArgumentOutOfRangeException(nameof(command.Kind));
            }
        }
        private SessionCommandResult SelectDisposition(string optionId)
        {
            var response = session.SelectDisposition(optionId);
            if (response.Result != null || (response.Status != SessionCommandStatus.Accepted && response.Status != SessionCommandStatus.AlreadyApplied)) return response;
            if (!activeContent.DispositionEffects.TryGetValue(optionId, out var effect) || effect == DispositionEffect.None) return response;
            if (effect == DispositionEffect.RemoveSimulatedApp) return session.SetInstallation(SimulatedInstallState.NotInstalled);
            response = session.SetInstallation(SimulatedInstallState.Installed);
            if (response.Result != null) return response;
            foreach (var category in new[] { PermissionCategory.Sms, PermissionCategory.Contacts })
            {
                var permission = activeContent.Definition.Permissions.First(item => item.Category == category);
                response = session.SetPermission(permission.Id, PermissionDecision.Allow(PermissionScope.Full));
                if (response.Result != null) return response;
            }
            return response;
        }
        private void Publish()
        {
            var observers = Changed;
            if (observers == null) return;
            foreach (Action<SessionSnapshot> observer in observers.GetInvocationList())
                try { observer(snapshot); }
                catch (Exception error)
                {
                    LastNotificationError = error;
                    try { reportNotificationError?.Invoke(error); } catch { /* 已提交的业务事务不因诊断回调回滚。 */ }
                }
        }
        private CommandResult Reply(DispatchStatus status) => new CommandResult(status, snapshot);
        private void AssertThread()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("调查用例须回到创建服务的线程执行。"); }
        public void Dispose()
        {
            AssertThread();
            if (disposed) return;
            if (dispatching) throw new InvalidOperationException("不能在状态通知中销毁调查服务。");
            if (session != null && snapshot.State.Phase == SessionPhase.Investigating) session.Abort();
            disposed = true; session = null; activeContent = null; snapshot = null; Changed = null;
        }
    }
}
