// 说明：换脸会议在安装与敏感授权任一种操作顺序下检查组合危险。
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Sessions;

namespace Zhijian.Domain.Outcomes
{
    public sealed class FaceMeetingOutcomePolicy : ICaseOutcomePolicy
    {
        public string PolicyId => "face-meeting";
        public CaseKind CaseKind => Zhijian.Domain.Cases.CaseKind.FaceMeeting;

        public OutcomeDecision EvaluateImmediate(SessionState state)
        {
            if (state.InstallState != SimulatedInstallState.Installed) return null;
            var sensitive = state.Definition.Permissions.FirstOrDefault(item =>
                (item.Category == PermissionCategory.Sms || item.Category == PermissionCategory.Contacts) &&
                state.PermissionDecisions.TryGetValue(item.Id, out var decision) && decision.State == PermissionDecisionState.Allowed);
            if (sensitive == null) return null;
            return new OutcomeDecision(OutcomeKind.MalwareFailure, "face.installed-with-sensitive-access", sensitive.Id,
                "未知来源模拟应用已安装且允许短信或通讯录访问，触发恶意应用结局。",
                "拒绝未知安装包与无关敏感授权，通过已知官方渠道确认会议安排。");
        }

        public OutcomeDecision EvaluateFinal(SessionState state)
        {
            var immediate = EvaluateImmediate(state);
            if (immediate != null) return immediate;
            if (state.InstallState != SimulatedInstallState.NotInstalled)
                return OutcomeResolver.Failure("face.unremoved-package", state.InstallState.ToString(),
                    "下载、安装包或模拟应用仍未清除，未知来源安装风险尚未解除。",
                    "按当前状态取消下载、删除安装包或移除模拟应用，再完成安全结案。");
            return OutcomeResolver.EvaluateCommonFinal(state);
        }
    }
}
