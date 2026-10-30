// 说明：在基础设施层显式转换 JSON、Guid、UTC 时间及评分条目，先识别较新格式再校验当前结构。
using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zhijian.Application.Progress;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Results;

namespace Zhijian.Infrastructure.Persistence
{
    internal sealed class ProgressFutureFormatException : Exception
    {
        public int Version { get; }
        public ProgressFutureFormatException(int version) : base("存档版本高于当前支持范围：" + version)
        { Version = version; }
    }

    internal static class ProgressJsonCodec
    {
        public const int SchemaVersion = 1;
        private const string FormatId = "zhijian.progress";

        public static string Encode(ProgressArchive archive)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            return JsonUtility.ToJson(new ProgressFileDto
            {
                schemaVersion = SchemaVersion, formatId = FormatId, lastSequence = archive.LastSequence,
                receipts = archive.Receipts.Select(item => new ProgressReceiptDto
                {
                    resultId = item.ResultId.ToString("D"), sequence = item.Sequence, caseId = item.CaseId,
                    scoringRuleVersion = item.ScoringRuleVersion, totalScore = item.TotalScore, outcome = (int)item.Outcome
                }).ToArray(),
                cases = archive.Cases.Select(item => new CaseProgressDto
                {
                    caseId = item.CaseId, scoringRuleVersion = item.ScoringRuleVersion,
                    bestTotalScore = item.BestTotalScore, hasBestSafeScore = item.BestSafeScore.HasValue,
                    bestSafeScore = item.BestSafeScore ?? 0, hasSafeClear = item.HasSafeClear,
                    lastResult = ToDto(item.LastResult)
                }).ToArray(),
                recentResults = archive.RecentResults.Select(ToDto).ToArray()
            }, true);
        }

        public static ProgressArchive Decode(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new FormatException("存档为空。");
                var header = JsonUtility.FromJson<ProgressHeaderDto>(json);
                if (header == null) throw new FormatException("缺少存档格式头。");
                // 较新格式即使尚无当前版本认识的字段也必须只读，不能当损坏回退。
                if (header.schemaVersion > SchemaVersion) throw new ProgressFutureFormatException(header.schemaVersion);
                if (header.schemaVersion != SchemaVersion || header.formatId != FormatId)
                    throw new FormatException("存档格式标识或版本无效；不猜测旧版迁移。");
                var dto = JsonUtility.FromJson<ProgressFileDto>(json);
                if (dto == null) throw new FormatException("缺少存档对象。");
                var receipts = RequireArray(dto.receipts).Select(item => new ProgressReceipt(ReadGuid(item.resultId),
                    item.sequence, item.caseId, item.scoringRuleVersion, item.totalScore, (OutcomeKind)item.outcome));
                var cases = RequireArray(dto.cases).Select(item =>
                {
                    if (!item.hasBestSafeScore && item.bestSafeScore != 0)
                        throw new FormatException("未安全通关时不应记录安全最高分。");
                    return new CaseProgress(item.caseId, item.scoringRuleVersion, item.bestTotalScore,
                        item.hasBestSafeScore ? (int?)item.bestSafeScore : null, item.hasSafeClear, FromDto(item.lastResult));
                });
                return new ProgressArchive(dto.lastSequence, receipts, cases,
                    RequireArray(dto.recentResults).Select(FromDto));
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is OverflowException)
            {
                throw new FormatException("存档结构、枚举、分数或顺序校验失败。", error);
            }
        }

        private static SavedResultDto ToDto(SavedResult item)
        {
            return new SavedResultDto
            {
                resultId = item.ResultId.ToString("D"), caseId = item.CaseId, caseTitle = item.CaseTitle,
                contentVersion = item.ContentVersion, scoringRuleVersion = item.ScoringRuleVersion,
                sequence = item.Sequence,
                resolvedAtUtc = item.ResolvedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                outcome = (int)item.Outcome, evidenceScore = item.EvidenceScore, permissionScore = item.PermissionScore,
                dispositionScore = item.DispositionScore, totalScore = item.TotalScore,
                scoreEntries = item.ScoreEntries.Select(entry => new ScoreEntryDto
                {
                    ruleId = Text(entry.RuleId), dimension = (int)entry.Dimension, delta = entry.Delta,
                    reason = Text(entry.Reason), relatedItemId = Text(entry.RelatedItemId),
                    recommendation = Text(entry.Recommendation)
                }).ToArray(),
                feedback = item.Feedback.Select(entry => new SavedFeedbackDto
                {
                    ruleId = Text(entry.RuleId), relatedItemId = Text(entry.RelatedItemId),
                    reason = Text(entry.Reason), recommendation = Text(entry.Recommendation)
                }).ToArray()
            };
        }

        private static SavedResult FromDto(SavedResultDto item)
        {
            if (item == null) throw new FormatException("缺少冻结结果。");
            if (!DateTimeOffset.TryParseExact(item.resolvedAtUtc, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var resolved) || resolved.Offset != TimeSpan.Zero)
                throw new FormatException("结果时间必须是显式 UTC 的往返格式。");
            var entries = RequireArray(item.scoreEntries).Select(entry => new ScoreEntry(Text(entry.ruleId),
                (ScoreDimension)entry.dimension, entry.delta, Text(entry.reason), Text(entry.relatedItemId),
                Text(entry.recommendation)));
            var feedback = RequireArray(item.feedback).Select(entry => new SavedFeedback(Text(entry.ruleId),
                Text(entry.relatedItemId), Text(entry.reason), Text(entry.recommendation)));
            var result = new SavedResult(ReadGuid(item.resultId), item.caseId, item.caseTitle,
                item.contentVersion, item.scoringRuleVersion, item.sequence, resolved, (OutcomeKind)item.outcome,
                item.evidenceScore, item.permissionScore, item.dispositionScore, entries, feedback);
            if (result.TotalScore != item.totalScore) throw new FormatException("结果总分与分项不一致。");
            return result;
        }

        private static Guid ReadGuid(string value)
        {
            if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty)
                throw new FormatException("结果 ID 必须是非空的 D 格式 Guid。");
            return id;
        }

        private static T[] RequireArray<T>(T[] values) where T : class
        {
            if (values == null || values.Any(item => item == null))
                throw new FormatException("存档集合缺失或包含空项。");
            return values;
        }

        private static string Text(string value) => value ?? string.Empty;
    }
}
