// 说明：协调只读案件、应用会话和结果通知，预览不绕过资源或发布门槛。
using System;
using System.Collections.Generic;
using Zhijian.Application.Cases;
using Zhijian.Application.Investigation;
using Zhijian.Application.Ports;
using Zhijian.Domain.Results;
using Zhijian.Domain.Sessions;
namespace Zhijian.Presentation.Investigation
{
    public sealed class InvestigationWorkspace : IDisposable
    {
        private readonly IInvestigationCaseRepository repository;
        private readonly InvestigationService service;
        private readonly HashSet<Guid> announced = new HashSet<Guid>();
        public string CaseId { get; private set; }
        public CaseLoadResult Loaded { get; private set; }
        public CaseContent Content => Loaded?.Content;
        public SessionState State => service.Current != null && CaseId != null && service.Current.State.Definition.Id == CaseId ? service.Current.State : null;
        public bool CanChange => State != null && State.Phase == SessionPhase.Investigating;
        public event Action Changed;
        public event Action<CaseResult> ResultProduced;
        public InvestigationWorkspace(IInvestigationCaseRepository repository, InvestigationService service)
        { this.repository = repository; this.service = service; service.Changed += OnChanged; }
        public void Enter(string caseId)
        {
            if (!repository.TryGetCase(caseId, out var loaded)) throw new ArgumentException("未知案件。", nameof(caseId));
            CaseId = caseId; Loaded = loaded;
            if (loaded.CanStartTraining && (State == null || State.Phase != SessionPhase.Investigating)) service.Start(caseId);
            Changed?.Invoke();
        }
        public CommandResult Execute(InvestigationCommand command)
        {
            var reply = service.Execute(command);
            if (reply.ProducedResult && reply.Snapshot?.State.Result != null && announced.Add(reply.Snapshot.State.Result.ResultId))
                ResultProduced?.Invoke(reply.Snapshot.State.Result);
            return reply;
        }
        public void Abort()
        { if (CanChange) Execute(new InvestigationCommand(State.SessionId, InvestigationCommandKind.Abort)); }
        public void Leave() { Abort(); CaseId = null; Loaded = null; }
        private void OnChanged(SessionSnapshot snapshot) { if (snapshot.State.Definition.Id == CaseId) Changed?.Invoke(); }
        public void Dispose() { service.Changed -= OnChanged; Changed = null; ResultProduced = null; }
    }
}
