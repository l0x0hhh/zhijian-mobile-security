// 说明：显式注册案件策略，先检查即时危险，再判断最终安全要求。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Rules;
using Zhijian.Domain.Sessions;

namespace Zhijian.Domain.Outcomes
{
    public sealed class OutcomeResolver
    {
        private readonly Dictionary<string, ICaseOutcomePolicy> policies = new Dictionary<string, ICaseOutcomePolicy>(StringComparer.Ordinal);

        public OutcomeResolver(IEnumerable<ICaseOutcomePolicy> policies)
        {
            if (policies == null) throw new ArgumentNullException(nameof(policies));
            foreach (var policy in policies)
            {
                if (policy == null || string.IsNullOrWhiteSpace(policy.PolicyId) ||
                    !Enum.IsDefined(typeof(CaseKind), policy.CaseKind) || this.policies.ContainsKey(policy.PolicyId))
                    throw new ArgumentException("策略不能为空、类型必须有效且 policyId 不可重复。", nameof(policies));
                this.policies.Add(policy.PolicyId, policy);
            }
        }

        public static OutcomeResolver CreateDefault()
        {
            return new OutcomeResolver(new ICaseOutcomePolicy[]
            {
                new VoiceLoanOutcomePolicy(), new FaceMeetingOutcomePolicy(), new PhotoPermissionOutcomePolicy()
            });
        }

        public bool IsRegistered(string policyId, CaseKind kind)
        {
            return policyId != null && policies.TryGetValue(policyId, out var policy) && policy.CaseKind == kind;
        }

        public OutcomeDecision EvaluateImmediate(SessionState state)
        {
            return CheckSupported(state, FindPolicy(state).EvaluateImmediate(state));
        }

        public OutcomeDecision EvaluateFinal(SessionState state)
        {
            var policy = FindPolicy(state);
            return CheckSupported(state, policy.EvaluateImmediate(state) ?? policy.EvaluateFinal(state))
                ?? throw new InvalidOperationException("最终结局策略必须返回一个结局。");
        }

        private ICaseOutcomePolicy FindPolicy(SessionState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!IsRegistered(state.Definition.PolicyId, state.Definition.Kind))
                throw new InvalidOperationException("案件类型与已注册策略不匹配。");
            return policies[state.Definition.PolicyId];
        }

        private static OutcomeDecision CheckSupported(SessionState state, OutcomeDecision decision)
        {
            if (decision != null && (!state.Definition.SupportedOutcomes.Contains(decision.Outcome) ||
                string.IsNullOrWhiteSpace(decision.ReasonId) || string.IsNullOrWhiteSpace(decision.Explanation)))
                throw new InvalidOperationException("策略输出了未声明或缺少原因的结局。");
            return decision;
        }

        internal static OutcomeDecision EvaluateCommonFinal(SessionState state)
        {
            var verification = state.Definition.VerificationOptions.FirstOrDefault(item => item.Id == state.VerificationOptionId);
            if (verification == null || !verification.Reliable)
                return Failure("judgment.unreliable-verification", state.VerificationOptionId,
                    verification == null ? "未选择核验方式。" : "所选核验方式不能可靠确认身份或来源。",
                    "使用原有可靠联系方式，或检查可信官方来源与开发者信息。");
            var risk = state.Definition.RiskOptions.FirstOrDefault(item => item.Id == state.RiskOptionId);
            if (risk == null || !risk.IsCorrect)
                return Failure("judgment.risk-classification", state.RiskOptionId,
                    risk == null ? "未选择风险分类。" : "风险分类与案件事实不匹配。", "综合证据判断风险，不把模拟检测当作真实鉴定。");
            var disposition = state.Definition.DispositionOptions.FirstOrDefault(item => item.Id == state.DispositionOptionId);
            if (disposition == null || !disposition.IsCorrect)
                return Failure("judgment.disposition", state.DispositionOptionId,
                    disposition == null ? "未选择最终处置。" : "最终处置没有阻止案件危险。", "拒绝风险行为并保存证据，通过可靠渠道报告。");
            var overreach = state.Definition.Permissions.FirstOrDefault(item => state.PermissionDecisions.TryGetValue(item.Id, out var decision) &&
                PermissionRules.IsOverreach(item, state.FeatureIntentId, decision));
            if (overreach != null || state.CurrentPrivacyRisk > 0)
                return Failure("judgment.unresolved-permission-risk", overreach == null ? null : overreach.Id,
                    "仍有超出当前用途的授权或未解除的隐私风险。", "撤销非必要访问，确认用途与最小授权范围后再结案。");
            return new OutcomeDecision(OutcomeKind.Safe, "outcome.safe", null,
                "已通过可靠渠道核验、正确识别风险并采取安全处置，当前危险状态已解除。",
                "继续坚持可靠核验、最小授权与保存证据；教学模拟不能代替真实鉴定。");
        }

        internal static OutcomeDecision Failure(string reasonId, string itemId, string explanation, string recommendation)
        {
            return new OutcomeDecision(OutcomeKind.JudgmentFailure, reasonId, itemId, explanation, recommendation);
        }
    }
}
