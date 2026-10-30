// 说明：成绩、评分项和反馈保持稳定行身份；保存状态变化只增删对应操作行。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Application.Progress;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Results;
using Zhijian.Presentation.Navigation;
namespace Zhijian.Presentation.Investigation
{
    public static class ResultsPageComposer
    {
        public static InvestigationPageData Build(PageComposerContext c)
        {
            var rows = new List<InvestigationRow>();
            var progress = c.Progress?.Current;
            var status = progress == null ? "记录服务尚未接入；本次结果尚未保存。" : progress.StatusMessage;
            if (progress != null && (progress.PendingResultCount > 0 || progress.SaveState == ProgressSaveState.Failed))
                rows.Add(new InvestigationRow("重试保存", "待保存：" + progress.PendingResultCount, new PageAction(PageActionKind.RetrySave)));
            if (progress?.LoadState == ProgressLoadState.RecoveryRequired)
                rows.Add(new InvestigationRow("保留损坏原件后新建存档", "需要明确确认；不会删除原文件。", new PageAction(PageActionKind.NewSave)));
            if (c.Route.Page == PageId.Records)
            {
                if (progress != null)
                {
                    foreach (var p in progress.Cases) rows.Add(new InvestigationRow(p.LastResult.CaseTitle + " · 规则 " + p.ScoringRuleVersion,
                        "最高 " + p.BestTotalScore + " 分 · 安全最高 " + (p.BestSafeScore.HasValue ? p.BestSafeScore + " 分" : "尚无") + " · " + (p.HasSafeClear ? "已安全通关" : "尚未安全通关"), key: "case:" + p.CaseId + ":" + p.ScoringRuleVersion));
                    foreach (var result in progress.RecentResults) rows.Add(c.Link(result.CaseTitle + " · " + result.TotalScore + " 分", PageId.RecordDetail, result.ResultId.ToString(),
                        result.ResolvedAtUtc.ToLocalTime().ToString("MM-dd HH:mm") + " · " + Outcome(result.Outcome)));
                    if (progress.RecentResults.Count == 0) rows.Add(new InvestigationRow("还没有调查记录", "完成一次正式练习后，在这里查看复盘。"));
                }
                return c.Page("调查记录", "最高分与安全通关分别记录", status, rows);
            }
            var live = c.Route.Page == PageId.Result ? c.State?.Result : null;
            var saved = progress?.RecentResults.FirstOrDefault(x => x.ResultId.ToString() == c.Route.ItemId);
            if (live == null && saved == null) return c.Page("没有可查看的结果", null, status, rows);
            var feedbackIndex = 0;
            if (live != null)
            {
                ScoreRows(rows, live.Score.EvidenceScore, live.Score.PermissionScore, live.Score.DispositionScore, live.Score.Entries);
                rows.Add(new InvestigationRow(live.Decision.Explanation, live.Decision.Recommendation, key: "decision"));
                foreach (var feedback in live.Feedback) rows.Add(new InvestigationRow(feedback.Reason, feedback.Recommendation, key: "feedback:" + feedbackIndex++));
            }
            else
            {
                ScoreRows(rows, saved.EvidenceScore, saved.PermissionScore, saved.DispositionScore, saved.ScoreEntries);
                foreach (var feedback in saved.Feedback) rows.Add(new InvestigationRow(feedback.Reason, feedback.Recommendation, key: "feedback:" + feedbackIndex++));
            }
            rows.Add(c.Link("重新调查", PageId.Phone, live?.CaseId ?? saved.CaseId)); rows.Add(c.Link("查看记录", PageId.Records));
            return c.Page(live?.CaseTitle ?? saved.CaseTitle, Outcome(live?.Outcome ?? saved.Outcome), status, rows);
        }
        private static void ScoreRows(List<InvestigationRow> rows, int evidence, int permission, int disposition, IEnumerable<ScoreEntry> entries)
        {
            rows.Add(new InvestigationRow((evidence + permission + disposition) + " / 100", "线索 " + evidence + "/40 · 权限 " + permission + "/30 · 处置 " + disposition + "/30", tone: RowTone.Selected, key: "score-total"));
            var entryIndex = 0;
            foreach (var entry in entries) rows.Add(new InvestigationRow(entry.Reason, "分值 " + entry.Delta + (string.IsNullOrEmpty(entry.Recommendation) ? "" : "\n" + entry.Recommendation), key: "score-entry:" + entryIndex++));
        }
        private static string Outcome(OutcomeKind kind)
        {
            switch (kind) { case OutcomeKind.Safe: return "安全结案"; case OutcomeKind.ScamFailure: return "转账风险";
                case OutcomeKind.MalwareFailure: return "恶意应用风险"; case OutcomeKind.PrivacyFailure: return "隐私暴露"; default: return "判断需要改进"; }
        }
    }
}
