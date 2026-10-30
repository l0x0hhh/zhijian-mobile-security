// 说明：证件照案件在最终提交时检查当前用途越权与全部相册访问。
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Rules;
using Zhijian.Domain.Sessions;

namespace Zhijian.Domain.Outcomes
{
    public sealed class PhotoPermissionOutcomePolicy : ICaseOutcomePolicy
    {
        public string PolicyId => "photo-permission";
        public CaseKind CaseKind => Zhijian.Domain.Cases.CaseKind.PhotoPermission;
        public OutcomeDecision EvaluateImmediate(SessionState state) { return null; }

        public OutcomeDecision EvaluateFinal(SessionState state)
        {
            var overreach = state.Definition.Permissions.FirstOrDefault(item =>
                state.PermissionDecisions.TryGetValue(item.Id, out var decision) && decision.State == PermissionDecisionState.Allowed &&
                ((item.Category == PermissionCategory.Photos && decision.Scope == PermissionScope.AllPhotos) ||
                PermissionRules.IsOverreach(item, state.FeatureIntentId, decision)));
            if (overreach != null)
                return new OutcomeDecision(OutcomeKind.PrivacyFailure, "photo.excessive-final-access", overreach.Id,
                    "提交时仍允许非当前用途的访问或全部相册访问，存在隐私泄露风险。",
                    "按拍摄、指定照片或放弃功能重新判断，仅保留必要范围，并拒绝不可信应用。");
            return OutcomeResolver.EvaluateCommonFinal(state);
        }
    }
}
