// 说明：检查普通提交必做项；缺少线索时仅提示未查看的来源，不暴露证据答案。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Sessions;

namespace Zhijian.Domain.Rules
{
    public sealed class SubmissionCheck
    {
        public bool IsReady => MissingRequirements.Count == 0;
        public IReadOnlyList<string> MissingRequirements { get; }
        public int MissingKeyEvidenceCount { get; }
        public IReadOnlyList<string> PendingSourceIds { get; }

        internal SubmissionCheck(IEnumerable<string> missing, int missingKeyEvidenceCount, IEnumerable<string> pendingSourceIds)
        {
            MissingRequirements = new ReadOnlyCollection<string>(missing.ToArray());
            MissingKeyEvidenceCount = missingKeyEvidenceCount;
            PendingSourceIds = new ReadOnlyCollection<string>(pendingSourceIds.ToArray());
        }
    }

    public static class SubmissionValidator
    {
        public static SubmissionCheck Validate(SessionState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var missing = new List<string>();
            var definition = state.Definition;
            if (state.Phase != SessionPhase.Investigating) missing.Add("session-closed");
            foreach (var step in definition.RequiredSteps)
            {
                bool completed;
                string reason;
                switch (step)
                {
                    case InvestigationStep.BackgroundViewed:
                        completed = definition.Sources.Where(item => item.RequiredForBackground).All(item => state.ViewedSourceIds.Contains(item.Id));
                        reason = "background-viewed"; break;
                    case InvestigationStep.AnyEvidenceCollected:
                        completed = definition.Evidence.Any(item => state.CollectedEvidenceIds.Contains(item.Id));
                        reason = "any-evidence-collected"; break;
                    case InvestigationStep.MediaPlayed:
                        completed = definition.Media.Where(item => item.Kind == MediaKind.Audio || item.Kind == MediaKind.Video)
                            .All(item => state.PlayedMediaIds.Contains(item.Id));
                        reason = "media-played"; break;
                    case InvestigationStep.MediaDetected:
                        completed = definition.Media.Where(item => item.DetectionIndicators.Count > 0)
                            .All(item => state.DetectedMediaIds.Contains(item.Id));
                        reason = "media-detected"; break;
                    case InvestigationStep.PermissionsReviewed:
                        completed = definition.Permissions.All(item => state.PermissionDecisions.TryGetValue(item.Id, out var decision) &&
                            decision.State != PermissionDecisionState.Unreviewed);
                        reason = "permissions-reviewed"; break;
                    case InvestigationStep.VerificationSelected:
                        completed = definition.VerificationOptions.Any(item => item.Id == state.VerificationOptionId);
                        reason = "verification-selected"; break;
                    case InvestigationStep.RiskSelected:
                        completed = definition.RiskOptions.Any(item => item.Id == state.RiskOptionId);
                        reason = "risk-selected"; break;
                    case InvestigationStep.DispositionSelected:
                        completed = definition.DispositionOptions.Any(item => item.Id == state.DispositionOptionId);
                        reason = "disposition-selected"; break;
                    case InvestigationStep.FeatureIntentSelected:
                        completed = definition.FeatureIntents.Any(item => item.Id == state.FeatureIntentId);
                        reason = "feature-intent-selected"; break;
                    default:
                        completed = false; reason = "unknown-required-step"; break;
                }
                if (!completed) missing.Add(reason);
            }
            var undiscovered = definition.Evidence.Where(item => item.IsKey && !state.CollectedEvidenceIds.Contains(item.Id)).ToArray();
            return new SubmissionCheck(missing, undiscovered.Length,
                undiscovered.SelectMany(item => item.SourceIds.Concat(item.RequiredSourceIds))
                    .Where(id => !state.ViewedSourceIds.Contains(id)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal));
        }
    }
}
