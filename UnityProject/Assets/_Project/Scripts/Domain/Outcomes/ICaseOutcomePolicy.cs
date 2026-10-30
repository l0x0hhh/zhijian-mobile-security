// 说明：约定只读案件结局策略及其原因与补救建议。
using Zhijian.Domain.Cases;
using Zhijian.Domain.Sessions;

namespace Zhijian.Domain.Outcomes
{
    public interface ICaseOutcomePolicy
    {
        string PolicyId { get; }
        CaseKind CaseKind { get; }
        OutcomeDecision EvaluateImmediate(SessionState state);
        OutcomeDecision EvaluateFinal(SessionState state);
    }

    public sealed class OutcomeDecision
    {
        public OutcomeKind Outcome { get; }
        public string ReasonId { get; }
        public string RelatedItemId { get; }
        public string Explanation { get; }
        public string Recommendation { get; }

        public OutcomeDecision(OutcomeKind outcome, string reasonId, string relatedItemId,
            string explanation, string recommendation)
        {
            Outcome = outcome;
            ReasonId = reasonId;
            RelatedItemId = relatedItemId;
            Explanation = explanation;
            Recommendation = recommendation;
        }
    }
}
