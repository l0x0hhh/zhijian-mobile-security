// 说明：定义显式字段式 JSON DTO，集合使用数组，Guid 和 UTC 时间使用字符串，不序列化领域会话。
using System;

namespace Zhijian.Infrastructure.Persistence
{
    [Serializable]
    internal sealed class ProgressHeaderDto
    {
        public int schemaVersion;
        public string formatId;
    }

    [Serializable]
    internal sealed class ProgressFileDto
    {
        public int schemaVersion;
        public string formatId;
        public long lastSequence;
        public ProgressReceiptDto[] receipts;
        public CaseProgressDto[] cases;
        public SavedResultDto[] recentResults;
    }

    [Serializable]
    internal sealed class ProgressReceiptDto
    {
        public string resultId;
        public long sequence;
        public string caseId;
        public int scoringRuleVersion;
        public int totalScore;
        public int outcome;
    }

    [Serializable]
    internal sealed class CaseProgressDto
    {
        public string caseId;
        public int scoringRuleVersion;
        public int bestTotalScore;
        public bool hasBestSafeScore;
        public int bestSafeScore;
        public bool hasSafeClear;
        public SavedResultDto lastResult;
    }

    [Serializable]
    internal sealed class SavedResultDto
    {
        public string resultId;
        public string caseId;
        public string caseTitle;
        public int contentVersion;
        public int scoringRuleVersion;
        public long sequence;
        public string resolvedAtUtc;
        public int outcome;
        public int evidenceScore;
        public int permissionScore;
        public int dispositionScore;
        public int totalScore;
        public ScoreEntryDto[] scoreEntries;
        public SavedFeedbackDto[] feedback;
    }

    [Serializable]
    internal sealed class ScoreEntryDto
    {
        public string ruleId;
        public int dimension;
        public int delta;
        public string reason;
        public string relatedItemId;
        public string recommendation;
    }

    [Serializable]
    internal sealed class SavedFeedbackDto
    {
        public string ruleId;
        public string relatedItemId;
        public string reason;
        public string recommendation;
    }
}
