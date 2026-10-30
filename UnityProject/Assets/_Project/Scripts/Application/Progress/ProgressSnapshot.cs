// 说明：定义存档与页面共用的不可变快照，保存状态、最高分与历史结果互不混淆。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Results;

namespace Zhijian.Application.Progress
{
    public enum ProgressLoadState { NotLoaded, Loading, Ready, RecoveredFromBackup, RecoveryRequired, Incompatible, Failed }
    public enum ProgressSaveState { Idle, Pending, Saving, Saved, Failed, Blocked }

    public sealed class SavedFeedback
    {
        public string RuleId { get; }
        public string RelatedItemId { get; }
        public string Reason { get; }
        public string Recommendation { get; }
        public SavedFeedback(string ruleId, string relatedItemId, string reason, string recommendation)
        {
            RuleId = ruleId; RelatedItemId = relatedItemId;
            Reason = reason ?? string.Empty; Recommendation = recommendation ?? string.Empty;
        }
    }

    public sealed class SavedResult
    {
        public Guid ResultId { get; }
        public string CaseId { get; }
        public string CaseTitle { get; }
        public int ContentVersion { get; }
        public int ScoringRuleVersion { get; }
        public long Sequence { get; }
        public DateTimeOffset ResolvedAtUtc { get; }
        public OutcomeKind Outcome { get; }
        public int EvidenceScore { get; }
        public int PermissionScore { get; }
        public int DispositionScore { get; }
        public int TotalScore => EvidenceScore + PermissionScore + DispositionScore;
        public IReadOnlyList<ScoreEntry> ScoreEntries { get; }
        public IReadOnlyList<SavedFeedback> Feedback { get; }

        public SavedResult(Guid resultId, string caseId, string caseTitle, int contentVersion, int scoringRuleVersion,
            long sequence, DateTimeOffset resolvedAtUtc, OutcomeKind outcome, int evidenceScore, int permissionScore,
            int dispositionScore, IEnumerable<ScoreEntry> scoreEntries, IEnumerable<SavedFeedback> feedback)
        {
            if (resultId == Guid.Empty || string.IsNullOrWhiteSpace(caseId) || string.IsNullOrWhiteSpace(caseTitle))
                throw new ArgumentException("存档结果必须有稳定标识和标题。");
            if (contentVersion <= 0 || scoringRuleVersion <= 0 || sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequence), "版本与结果顺序必须为正整数。");
            if (!Enum.IsDefined(typeof(OutcomeKind), outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
            var score = new ScoreBreakdown(evidenceScore, permissionScore, dispositionScore, scoreEntries);
            var copiedFeedback = (feedback ?? Enumerable.Empty<SavedFeedback>()).ToArray();
            if (copiedFeedback.Any(item => item == null)) throw new ArgumentException("复盘条目不能含空值。", nameof(feedback));
            ResultId = resultId; CaseId = caseId; CaseTitle = caseTitle; ContentVersion = contentVersion;
            ScoringRuleVersion = scoringRuleVersion; Sequence = sequence; ResolvedAtUtc = resolvedAtUtc; Outcome = outcome;
            EvidenceScore = score.EvidenceScore; PermissionScore = score.PermissionScore; DispositionScore = score.DispositionScore;
            ScoreEntries = score.Entries; Feedback = Array.AsReadOnly(copiedFeedback);
        }
    }

    public sealed class CaseProgress
    {
        public string CaseId { get; }
        public int ScoringRuleVersion { get; }
        public int BestTotalScore { get; }
        public int? BestSafeScore { get; }
        public bool HasSafeClear { get; }
        public SavedResult LastResult { get; }

        public CaseProgress(string caseId, int scoringRuleVersion, int bestTotalScore, int? bestSafeScore,
            bool hasSafeClear, SavedResult lastResult)
        {
            if (string.IsNullOrWhiteSpace(caseId) || scoringRuleVersion <= 0)
                throw new ArgumentException("成绩聚合必须有案件与评分规则版本。");
            if (bestTotalScore < 0 || bestTotalScore > 100 || (bestSafeScore.HasValue && (bestSafeScore.Value < 0 || bestSafeScore.Value > bestTotalScore)))
                throw new ArgumentOutOfRangeException(nameof(bestTotalScore));
            if (hasSafeClear != bestSafeScore.HasValue) throw new ArgumentException("安全通关与安全最高分必须对应。");
            if (lastResult == null || lastResult.CaseId != caseId || lastResult.ScoringRuleVersion != scoringRuleVersion || lastResult.TotalScore > bestTotalScore)
                throw new ArgumentException("最近结果须属于同一案件和评分规则。", nameof(lastResult));
            CaseId = caseId; ScoringRuleVersion = scoringRuleVersion; BestTotalScore = bestTotalScore;
            BestSafeScore = bestSafeScore; HasSafeClear = hasSafeClear; LastResult = lastResult;
        }
    }

    public sealed class ProgressSnapshot
    {
        public ProgressLoadState LoadState { get; }
        public ProgressSaveState SaveState { get; }
        public IReadOnlyList<CaseProgress> Cases { get; }
        public IReadOnlyList<SavedResult> RecentResults { get; }
        public int PendingResultCount { get; }
        public string StatusMessage { get; }
        public bool CanWrite => LoadState == ProgressLoadState.Ready || LoadState == ProgressLoadState.RecoveredFromBackup;

        public ProgressSnapshot(ProgressLoadState loadState, ProgressSaveState saveState,
            IEnumerable<CaseProgress> cases, IEnumerable<SavedResult> recentResults, int pendingResultCount, string statusMessage)
        {
            if (!Enum.IsDefined(typeof(ProgressLoadState), loadState) || !Enum.IsDefined(typeof(ProgressSaveState), saveState))
                throw new ArgumentException("未知存档状态。");
            if (pendingResultCount < 0) throw new ArgumentOutOfRangeException(nameof(pendingResultCount));
            var copiedCases = (cases ?? Enumerable.Empty<CaseProgress>()).ToArray();
            var copiedRecent = (recentResults ?? Enumerable.Empty<SavedResult>()).ToArray();
            if (copiedCases.Any(item => item == null) || copiedRecent.Any(item => item == null))
                throw new ArgumentException("成绩快照不能含空项。");
            LoadState = loadState; SaveState = saveState; PendingResultCount = pendingResultCount;
            Cases = Array.AsReadOnly(copiedCases); RecentResults = Array.AsReadOnly(copiedRecent);
            StatusMessage = statusMessage ?? string.Empty;
        }
    }

    public sealed class ProgressWriteResult
    {
        public Guid ResultId { get; }
        public ProgressSaveState State { get; }
        public string Message { get; }
        public ProgressWriteResult(Guid resultId, ProgressSaveState state, string message)
        {
            if (resultId == Guid.Empty) throw new ArgumentException("写入反馈须关联原结果。", nameof(resultId));
            if (state != ProgressSaveState.Saved && state != ProgressSaveState.Pending && state != ProgressSaveState.Failed && state != ProgressSaveState.Blocked)
                throw new ArgumentOutOfRangeException(nameof(state));
            ResultId = resultId; State = state; Message = message ?? string.Empty;
        }
    }
}
