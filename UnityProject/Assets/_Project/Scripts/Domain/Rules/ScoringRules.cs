// 说明：按当前答案和去重历史事实重算 40/30/30 分及解释明细。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Results;
using Zhijian.Domain.Sessions;

namespace Zhijian.Domain.Rules
{
    public static class ScoringRules
    {
        public static ScoreBreakdown Calculate(SessionState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var entries = new List<ScoreEntry>();
            int evidenceScore = 0;
            foreach (var evidence in state.Definition.Evidence)
            {
                var collected = state.CollectedEvidenceIds.Contains(evidence.Id);
                if (!evidence.IsKey && !collected) continue;
                var points = collected && evidence.IsKey ? evidence.ScoreWeight : 0;
                evidenceScore += points;
                entries.Add(new ScoreEntry(collected ? "evidence.collected" : "evidence.not-collected", ScoreDimension.Evidence,
                    points, collected ? (evidence.IsKey ? "已有效收集关键证据。" : "补充线索不另计分。") : "未收集这条关键证据。",
                    evidence.Id, collected ? null : "返回关联来源查看事实，并通过统一取证入口收集。"));
            }
            int permissionRaw = 0;
            foreach (var permission in state.Definition.Permissions)
            {
                state.PermissionDecisions.TryGetValue(permission.Id, out var decision);
                var correct = PermissionRules.IsExpectedDecision(permission, state.FeatureIntentId, decision);
                var points = correct ? permission.ScoreWeight : 0;
                permissionRaw += points;
                var unreviewed = decision == null || decision.State == PermissionDecisionState.Unreviewed;
                entries.Add(new ScoreEntry(correct ? "permission.correct" : unreviewed ? "permission.unreviewed" : "permission.incorrect",
                    ScoreDimension.Permission, points, correct ? "最终决策符合当前用途与最小访问范围。" :
                    unreviewed ? "未完成权限判断。" : "最终决策与当前用途或最小范围不匹配。", permission.Id,
                    correct ? null : "明确用途，仅授权必要范围；拒绝必要访问时选择放弃对应功能。"));
                if (state.HistoricalOverreachIds.Contains(permission.Id))
                {
                    permissionRaw -= 5;
                    entries.Add(new ScoreEntry("permission.first-overreach", ScoreDimension.Permission, -5,
                        "曾允许超出当前用途或范围的访问，本项目只扣一次。", permission.Id,
                        "撤销非必要访问可降低当前风险，历史越权事实与扣分仍保留。"));
                }
            }
            var permissionScore = Math.Max(0, permissionRaw);
            if (permissionRaw < 0)
                entries.Add(new ScoreEntry("permission.floor-adjustment", ScoreDimension.Permission, -permissionRaw,
                    "权限维度最低为 0 分；此项仅解释下限，不抵消越权事实。", null));
            var verification = state.Definition.VerificationOptions.FirstOrDefault(item => item.Id == state.VerificationOptionId);
            var risk = state.Definition.RiskOptions.FirstOrDefault(item => item.Id == state.RiskOptionId);
            var disposition = state.Definition.DispositionOptions.FirstOrDefault(item => item.Id == state.DispositionOptionId);
            int dispositionScore = 0;
            dispositionScore += AddDecision(entries, "verification", verification != null, verification != null && verification.Reliable,
                state.VerificationOptionId, "核验", "使用原有可靠联系方式或可信官方来源核验。" + (verification == null ? "" : verification.Feedback));
            dispositionScore += AddDecision(entries, "risk", risk != null, risk != null && risk.IsCorrect,
                state.RiskOptionId, "风险分类", "结合已收集事实选择对应风险类型，模拟检测只能作为辅助证据。");
            dispositionScore += AddDecision(entries, "disposition", disposition != null, disposition != null && disposition.IsCorrect,
                state.DispositionOptionId, "最终处置", "拒绝转账、未知安装或不可信应用，并保存证据报告风险。");
            return new ScoreBreakdown(evidenceScore, permissionScore, dispositionScore, entries);
        }

        private static int AddDecision(ICollection<ScoreEntry> entries, string rule, bool selected, bool correct,
            string itemId, string label, string recommendation)
        {
            var points = correct ? 10 : 0;
            entries.Add(new ScoreEntry(rule + (correct ? ".correct" : selected ? ".incorrect" : ".not-completed"),
                ScoreDimension.Disposition, points, correct ? label + "正确。" : selected ? label + "错误。" : label + "未完成。",
                itemId, correct ? null : recommendation));
            return points;
        }
    }
}
