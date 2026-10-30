// 说明：根据当前用途判断权限答案、越权事实与当前隐私风险。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Domain.Cases;

namespace Zhijian.Domain.Rules
{
    public static class PermissionRules
    {
        public static bool IsAllowedScopeValid(PermissionDefinition definition, PermissionDecision decision)
        {
            if (definition == null || decision == null) return false;
            if (decision.State == PermissionDecisionState.Unreviewed || decision.State == PermissionDecisionState.Denied)
                return decision.Scope == PermissionScope.None;
            return decision.State == PermissionDecisionState.Allowed && decision.Scope != PermissionScope.None &&
                definition.Scopes.Contains(decision.Scope);
        }

        public static bool IsExpectedDecision(PermissionDefinition definition, string intentId, PermissionDecision decision)
        {
            if (!IsAllowedScopeValid(definition, decision)) return false;
            var rule = definition.IntentRules.FirstOrDefault(item => item.IntentId == intentId);
            if (rule == null || decision.State == PermissionDecisionState.Unreviewed) return false;
            return decision.State == PermissionDecisionState.Denied
                ? rule.AcceptsDenied : rule.AllowedScopes.Contains(decision.Scope);
        }

        public static bool IsOverreach(PermissionDefinition definition, string intentId, PermissionDecision decision)
        {
            return decision != null && decision.State == PermissionDecisionState.Allowed &&
                !IsExpectedDecision(definition, intentId, decision);
        }

        public static int CalculateRisk(CaseDefinition definition, string intentId,
            IReadOnlyDictionary<string, PermissionDecision> decisions)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (decisions == null) throw new ArgumentNullException(nameof(decisions));
            long risk = 0;
            foreach (var permission in definition.Permissions)
            {
                if (!decisions.TryGetValue(permission.Id, out var decision) || decision == null ||
                    decision.State != PermissionDecisionState.Allowed) continue;
                var rule = permission.IntentRules.FirstOrDefault(item => item.IntentId == intentId);
                if (rule == null || !rule.RiskWeights.TryGetValue(decision.Scope, out var weight)) return 100;
                risk += Math.Max(0, weight);
            }
            return (int)Math.Min(100L, risk);
        }
    }
}
