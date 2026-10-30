// 说明：保存与引擎无关的不可变存档、永久结果回执和按案件及评分版本的成绩聚合。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Domain.Cases;

namespace Zhijian.Application.Progress
{
    // 回执不随展示历史裁剪；只保留去重、顺序及校验聚合所需的最小结果摘要。
    public sealed class ProgressReceipt
    {
        public Guid ResultId { get; }
        public long Sequence { get; }
        public string CaseId { get; }
        public int ScoringRuleVersion { get; }
        public int TotalScore { get; }
        public OutcomeKind Outcome { get; }

        public ProgressReceipt(Guid resultId, long sequence, string caseId, int scoringRuleVersion,
            int totalScore, OutcomeKind outcome)
        {
            if (resultId == Guid.Empty || sequence <= 0 || string.IsNullOrWhiteSpace(caseId) ||
                scoringRuleVersion <= 0 || totalScore < 0 || totalScore > 100 ||
                !Enum.IsDefined(typeof(OutcomeKind), outcome))
                throw new ArgumentException("结果回执的标识、顺序、版本或分数无效。");
            ResultId = resultId; Sequence = sequence; CaseId = caseId;
            ScoringRuleVersion = scoringRuleVersion; TotalScore = totalScore; Outcome = outcome;
        }

        public bool Matches(ProgressReceipt other)
        {
            return other != null && ResultId == other.ResultId && Sequence == other.Sequence &&
                CaseId == other.CaseId && ScoringRuleVersion == other.ScoringRuleVersion &&
                TotalScore == other.TotalScore && Outcome == other.Outcome;
        }
    }

    public sealed class ProgressArchive
    {
        public const int HistoryLimit = 10;
        private readonly Dictionary<Guid, ProgressReceipt> receiptsById;
        public long LastSequence { get; }
        public IReadOnlyList<ProgressReceipt> Receipts { get; }
        public IReadOnlyList<CaseProgress> Cases { get; }
        public IReadOnlyList<SavedResult> RecentResults { get; }

        public static ProgressArchive Empty => new ProgressArchive(0, new ProgressReceipt[0],
            new CaseProgress[0], new SavedResult[0]);

        public ProgressArchive(long lastSequence, IEnumerable<ProgressReceipt> receipts,
            IEnumerable<CaseProgress> cases, IEnumerable<SavedResult> recentResults)
        {
            var copiedReceipts = (receipts ?? throw new ArgumentNullException(nameof(receipts))).ToArray();
            var copiedCases = (cases ?? throw new ArgumentNullException(nameof(cases))).ToArray();
            var copiedRecent = (recentResults ?? throw new ArgumentNullException(nameof(recentResults))).ToArray();
            if (lastSequence < 0 || copiedReceipts.Any(item => item == null) ||
                copiedCases.Any(item => item == null) || copiedRecent.Any(item => item == null))
                throw new ArgumentException("存档不能包含空条目或负顺序。");
            receiptsById = new Dictionary<Guid, ProgressReceipt>();
            var sequences = new HashSet<long>();
            foreach (var receipt in copiedReceipts)
            {
                if (receiptsById.ContainsKey(receipt.ResultId) || !sequences.Add(receipt.Sequence))
                    throw new ArgumentException("存档存在重复 resultId 或 Sequence。");
                receiptsById.Add(receipt.ResultId, receipt);
            }
            if (lastSequence != (copiedReceipts.Length == 0 ? 0 : copiedReceipts.Max(item => item.Sequence)))
                throw new ArgumentException("存档顺序水位与回执不一致。");

            // 不根据当前案件目录过滤，旧案件及旧评分版本的聚合都必须保留。
            var groups = copiedReceipts.GroupBy(item => new { item.CaseId, item.ScoringRuleVersion }).ToArray();
            if (groups.Length != copiedCases.Length)
                throw new ArgumentException("成绩聚合与永久回执的案件版本数量不一致。");
            var knownResults = new Dictionary<Guid, SavedResult>();
            foreach (var group in groups)
            {
                var matches = copiedCases.Where(item => item.CaseId == group.Key.CaseId &&
                    item.ScoringRuleVersion == group.Key.ScoringRuleVersion).ToArray();
                if (matches.Length != 1) throw new ArgumentException("案件评分版本聚合缺失或重复。");
                var aggregate = matches[0];
                var safe = group.Where(item => item.Outcome == OutcomeKind.Safe).ToArray();
                int? bestSafe = safe.Length == 0 ? (int?)null : safe.Max(item => item.TotalScore);
                if (aggregate.BestTotalScore != group.Max(item => item.TotalScore) ||
                    aggregate.BestSafeScore != bestSafe || aggregate.HasSafeClear != bestSafe.HasValue ||
                    aggregate.LastResult.Sequence != group.Max(item => item.Sequence))
                    throw new ArgumentException("成绩聚合与回执的最高分、安全状态或最近顺序不一致。");
                ValidateResult(aggregate.LastResult, knownResults);
            }
            var expectedRecent = copiedReceipts.OrderByDescending(item => item.Sequence)
                .Take(HistoryLimit).Select(item => item.ResultId).ToArray();
            var orderedRecent = copiedRecent.OrderByDescending(item => item.Sequence).ToArray();
            if (!expectedRecent.SequenceEqual(orderedRecent.Select(item => item.ResultId)))
                throw new ArgumentException("最近结果必须恰好对应最新的至多十个回执。");
            foreach (var result in orderedRecent) ValidateResult(result, knownResults);

            LastSequence = lastSequence;
            Receipts = Array.AsReadOnly(copiedReceipts.OrderBy(item => item.Sequence).ToArray());
            Cases = Array.AsReadOnly(copiedCases.OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ThenBy(item => item.ScoringRuleVersion).ToArray());
            RecentResults = Array.AsReadOnly(orderedRecent);
        }

        public bool Contains(Guid resultId) => receiptsById.ContainsKey(resultId);

        public ProgressArchive Append(SavedResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (Contains(result.ResultId) || LastSequence == long.MaxValue || result.Sequence != LastSequence + 1)
                throw new InvalidOperationException("新结果必须使用未出现的 ID 和下一首次接收顺序。");
            var previous = Cases.FirstOrDefault(item => item.CaseId == result.CaseId &&
                item.ScoringRuleVersion == result.ScoringRuleVersion);
            var bestTotal = Math.Max(previous == null ? 0 : previous.BestTotalScore, result.TotalScore);
            int? bestSafe = previous == null ? null : previous.BestSafeScore;
            if (result.Outcome == OutcomeKind.Safe)
                bestSafe = Math.Max(bestSafe ?? 0, result.TotalScore);
            var aggregate = new CaseProgress(result.CaseId, result.ScoringRuleVersion, bestTotal,
                bestSafe, bestSafe.HasValue, result);
            var nextCases = Cases.Where(item => item.CaseId != result.CaseId ||
                item.ScoringRuleVersion != result.ScoringRuleVersion).Concat(new[] { aggregate });
            var receipt = new ProgressReceipt(result.ResultId, result.Sequence, result.CaseId,
                result.ScoringRuleVersion, result.TotalScore, result.Outcome);
            return new ProgressArchive(result.Sequence, Receipts.Concat(new[] { receipt }), nextCases,
                RecentResults.Concat(new[] { result }).OrderByDescending(item => item.Sequence).Take(HistoryLimit));
        }

        // 拒绝用旧实例覆盖其他写入者已持久化的结果，也拒绝更改已冻结结果的可见内容。
        public void RequireExtensionOf(ProgressArchive previous)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            foreach (var receipt in previous.Receipts)
                if (!receiptsById.TryGetValue(receipt.ResultId, out var currentReceipt) || !currentReceipt.Matches(receipt))
                    throw new InvalidOperationException("待保存数据缺少或改写了磁盘已有结果，请重新加载存档。");
            var visible = Cases.Select(item => item.LastResult).Concat(RecentResults)
                .GroupBy(item => item.ResultId).ToDictionary(group => group.Key, group => group.First());
            foreach (var result in previous.Cases.Select(item => item.LastResult).Concat(previous.RecentResults))
                if (visible.TryGetValue(result.ResultId, out var currentResult) && !SameResult(currentResult, result))
                    throw new InvalidOperationException("已有结果的冻结内容发生变化。");
        }

        private void ValidateResult(SavedResult result, Dictionary<Guid, SavedResult> known)
        {
            if (!receiptsById.TryGetValue(result.ResultId, out var receipt) ||
                !receipt.Matches(new ProgressReceipt(result.ResultId, result.Sequence, result.CaseId,
                    result.ScoringRuleVersion, result.TotalScore, result.Outcome)))
                throw new ArgumentException("结果内容与永久回执不一致。");
            if (known.TryGetValue(result.ResultId, out var previous) && !SameResult(result, previous))
                throw new ArgumentException("相同结果在历史和聚合中的冻结内容不一致。");
            known[result.ResultId] = result;
        }

        private static bool SameResult(SavedResult first, SavedResult second)
        {
            if (first.ResultId != second.ResultId || first.CaseId != second.CaseId ||
                first.CaseTitle != second.CaseTitle || first.ContentVersion != second.ContentVersion ||
                first.ScoringRuleVersion != second.ScoringRuleVersion || first.Sequence != second.Sequence ||
                first.ResolvedAtUtc != second.ResolvedAtUtc || first.Outcome != second.Outcome ||
                first.EvidenceScore != second.EvidenceScore || first.PermissionScore != second.PermissionScore ||
                first.DispositionScore != second.DispositionScore || first.ScoreEntries.Count != second.ScoreEntries.Count ||
                first.Feedback.Count != second.Feedback.Count) return false;
            for (var i = 0; i < first.ScoreEntries.Count; i++)
            {
                var a = first.ScoreEntries[i]; var b = second.ScoreEntries[i];
                if (a.RuleId != b.RuleId || a.Dimension != b.Dimension || a.Delta != b.Delta ||
                    a.Reason != b.Reason || a.RelatedItemId != b.RelatedItemId ||
                    a.Recommendation != b.Recommendation) return false;
            }
            for (var i = 0; i < first.Feedback.Count; i++)
            {
                var a = first.Feedback[i]; var b = second.Feedback[i];
                if (a.RuleId != b.RuleId || a.RelatedItemId != b.RelatedItemId || a.Reason != b.Reason ||
                    a.Recommendation != b.Recommendation) return false;
            }
            return true;
        }
    }
}
