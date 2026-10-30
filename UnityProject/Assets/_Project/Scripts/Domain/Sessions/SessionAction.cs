// 说明：定义领域命令反馈、只读操作记录与调查生命周期。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zhijian.Domain.Results;

namespace Zhijian.Domain.Sessions
{
    public enum SessionPhase { Investigating, Resolved, Aborted }
    public enum SimulatedInstallState { NotInstalled, Downloading, Downloaded, Installed }
    public enum MediaTaskPhase { Idle, Running, Completed, Failed }
    public enum SessionCommandStatus
    {
        Accepted, AlreadyApplied, InvalidAction, SessionClosed, StaleSession,
        StaleRequest, MissingRequirements, ConfirmationRequired, StaleRevision
    }

    public sealed class SessionAction
    {
        public long Revision { get; }
        public string ActionId { get; }
        public string RelatedItemId { get; }
        public string Detail { get; }

        internal SessionAction(long revision, string actionId, string relatedItemId, string detail)
        {
            Revision = revision;
            ActionId = actionId;
            RelatedItemId = relatedItemId;
            Detail = detail;
        }
    }

    public sealed class MediaTaskState
    {
        public Guid RequestId { get; }
        public MediaTaskPhase Phase { get; }
        public bool HasCompletedResult { get; }

        internal MediaTaskState(Guid requestId, MediaTaskPhase phase, bool hasCompletedResult)
        {
            RequestId = requestId;
            Phase = phase;
            HasCompletedResult = hasCompletedResult;
        }
    }

    public sealed class SessionCommandResult
    {
        public SessionCommandStatus Status { get; }
        public string ReasonId { get; }
        public long Revision { get; }
        public IReadOnlyList<string> MissingRequirements { get; }
        public int MissingKeyEvidenceCount { get; }
        public IReadOnlyList<string> PendingSourceIds { get; }
        public CaseResult Result { get; }

        internal SessionCommandResult(SessionCommandStatus status, string reasonId, long revision,
            CaseResult result, IEnumerable<string> missingRequirements = null,
            int missingKeyEvidenceCount = 0, IEnumerable<string> pendingSourceIds = null)
        {
            Status = status;
            ReasonId = reasonId;
            Revision = revision;
            Result = result;
            MissingRequirements = new ReadOnlyCollection<string>((missingRequirements ?? Enumerable.Empty<string>()).ToArray());
            MissingKeyEvidenceCount = missingKeyEvidenceCount;
            PendingSourceIds = new ReadOnlyCollection<string>((pendingSourceIds ?? Enumerable.Empty<string>()).ToArray());
        }
    }
}
