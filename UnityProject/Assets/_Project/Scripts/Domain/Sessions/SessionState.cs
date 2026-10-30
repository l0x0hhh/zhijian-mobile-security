// 说明：向规则与应用层提供独立复制的只读调查状态。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Results;

namespace Zhijian.Domain.Sessions
{
    public sealed class SessionState
    {
        public Guid SessionId { get; }
        public long Revision { get; }
        public CaseDefinition Definition { get; }
        public SessionPhase Phase { get; }
        public string FeatureIntentId { get; }
        public IReadOnlyCollection<string> ViewedSourceIds { get; }
        public IReadOnlyCollection<string> CollectedEvidenceIds { get; }
        public IReadOnlyCollection<string> PlayedMediaIds { get; }
        public IReadOnlyCollection<string> DetectedMediaIds { get; }
        public IReadOnlyCollection<string> HistoricalOverreachIds { get; }
        public IReadOnlyDictionary<string, PermissionDecision> PermissionDecisions { get; }
        public IReadOnlyDictionary<string, MediaTaskState> PlaybackTasks { get; }
        public IReadOnlyDictionary<string, MediaTaskState> DetectionTasks { get; }
        public string VerificationOptionId { get; }
        public string RiskOptionId { get; }
        public string DispositionOptionId { get; }
        public SimulatedInstallState InstallState { get; }
        public int CurrentPrivacyRisk { get; }
        public IReadOnlyList<SessionAction> Actions { get; }
        public CaseResult Result { get; }

        internal SessionState(Guid sessionId, long revision, CaseDefinition definition, SessionPhase phase,
            string featureIntentId, IEnumerable<string> viewedSourceIds, IEnumerable<string> collectedEvidenceIds,
            IEnumerable<string> playedMediaIds, IEnumerable<string> detectedMediaIds,
            IEnumerable<string> historicalOverreachIds, IDictionary<string, PermissionDecision> permissionDecisions,
            IDictionary<string, MediaTaskState> playbackTasks, IDictionary<string, MediaTaskState> detectionTasks,
            string verificationOptionId, string riskOptionId, string dispositionOptionId,
            SimulatedInstallState installState, int currentPrivacyRisk, IEnumerable<SessionAction> actions, CaseResult result)
        {
            SessionId = sessionId;
            Revision = revision;
            Definition = definition;
            Phase = phase;
            FeatureIntentId = featureIntentId;
            ViewedSourceIds = CopyIds(viewedSourceIds);
            CollectedEvidenceIds = CopyIds(collectedEvidenceIds);
            PlayedMediaIds = CopyIds(playedMediaIds);
            DetectedMediaIds = CopyIds(detectedMediaIds);
            HistoricalOverreachIds = CopyIds(historicalOverreachIds);
            PermissionDecisions = new ReadOnlyDictionary<string, PermissionDecision>(new Dictionary<string, PermissionDecision>(permissionDecisions, StringComparer.Ordinal));
            PlaybackTasks = new ReadOnlyDictionary<string, MediaTaskState>(new Dictionary<string, MediaTaskState>(playbackTasks, StringComparer.Ordinal));
            DetectionTasks = new ReadOnlyDictionary<string, MediaTaskState>(new Dictionary<string, MediaTaskState>(detectionTasks, StringComparer.Ordinal));
            VerificationOptionId = verificationOptionId;
            RiskOptionId = riskOptionId;
            DispositionOptionId = dispositionOptionId;
            InstallState = installState;
            CurrentPrivacyRisk = currentPrivacyRisk;
            Actions = new ReadOnlyCollection<SessionAction>(actions.ToArray());
            Result = result;
        }

        private static IReadOnlyCollection<string> CopyIds(IEnumerable<string> values)
        {
            return new ReadOnlyCollection<string>(values.OrderBy(value => value, StringComparer.Ordinal).ToArray());
        }
    }
}
