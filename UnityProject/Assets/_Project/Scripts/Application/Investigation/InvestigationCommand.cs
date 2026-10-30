// 说明：为一次串行用例携带会话标识、操作及媒体请求标识，避免旧页面写入新调查。
using System;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Sessions;

namespace Zhijian.Application.Investigation
{
    public enum InvestigationCommandKind
    {
        ViewSource, CollectEvidence, SelectFeatureIntent, SetPermission, SelectVerification,
        SelectRisk, SelectDisposition, SetInstallation, BeginPlayback, CompletePlayback,
        CancelPlayback, BeginDetection, CompleteDetection, CancelDetection, Submit, Abort
    }
    public sealed class InvestigationCommand
    {
        public Guid SessionId { get; }
        public InvestigationCommandKind Kind { get; }
        public string ItemId { get; }
        public PermissionDecision Permission { get; }
        public SimulatedInstallState Installation { get; }
        public Guid RequestId { get; }
        public bool Succeeded { get; }
        public long ExpectedRevision { get; }
        public bool ConfirmMissingEvidence { get; }
        public InvestigationCommand(Guid sessionId, InvestigationCommandKind kind, string itemId = null,
            PermissionDecision permission = null, SimulatedInstallState installation = SimulatedInstallState.NotInstalled,
            Guid requestId = default(Guid), bool succeeded = false, long expectedRevision = -1,
            bool confirmMissingEvidence = false)
        {
            SessionId = sessionId; Kind = kind; ItemId = itemId; Permission = permission;
            Installation = installation; RequestId = requestId; Succeeded = succeeded;
            ExpectedRevision = expectedRevision; ConfirmMissingEvidence = confirmMissingEvidence;
        }
    }
}
