// 说明：终局时一次冻结结果、评分、操作事实及复盘建议。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Outcomes;
using Zhijian.Domain.Sessions;

namespace Zhijian.Domain.Results
{
    public sealed class ResultFeedback
    {
        public string RuleId { get; }
        public string RelatedItemId { get; }
        public string Reason { get; }
        public string Recommendation { get; }

        internal ResultFeedback(string ruleId, string relatedItemId, string reason, string recommendation)
        {
            RuleId = ruleId;
            RelatedItemId = relatedItemId;
            Reason = reason;
            Recommendation = recommendation;
        }
    }

    public sealed class CaseResult
    {
        public Guid ResultId { get; }
        public Guid SessionId => FinalState.SessionId;
        public string CaseId => FinalState.Definition.Id;
        public string CaseTitle => FinalState.Definition.Title;
        public int ContentVersion => FinalState.Definition.ContentVersion;
        public int ScoringRuleVersion => FinalState.Definition.ScoringRuleVersion;
        public long Revision => FinalState.Revision;
        public DateTimeOffset ResolvedAtUtc { get; }
        public OutcomeKind Outcome => Decision.Outcome;
        public OutcomeDecision Decision { get; }
        public ScoreBreakdown Score { get; }
        public int TotalScore => Score.Total;
        public int CurrentPrivacyRisk => FinalState.CurrentPrivacyRisk;
        public SessionState FinalState { get; }
        public IReadOnlyList<ResultFeedback> Feedback { get; }

        internal CaseResult(Guid resultId, SessionState finalState, OutcomeDecision decision, ScoreBreakdown score)
        {
            ResultId = resultId;
            FinalState = finalState;
            Decision = decision;
            Score = score;
            ResolvedAtUtc = DateTimeOffset.UtcNow;
            var feedback = score.Entries
                .Where(entry => entry.Delta <= 0 && !string.IsNullOrWhiteSpace(entry.Recommendation))
                .Select(entry => new ResultFeedback(entry.RuleId, entry.RelatedItemId, entry.Reason, entry.Recommendation))
                .ToList();
            if (decision.Outcome != OutcomeKind.Safe)
                feedback.Insert(0, new ResultFeedback(decision.ReasonId, decision.RelatedItemId,
                    decision.Explanation, decision.Recommendation));
            Feedback = new ReadOnlyCollection<ResultFeedback>(feedback);
        }
    }
}
