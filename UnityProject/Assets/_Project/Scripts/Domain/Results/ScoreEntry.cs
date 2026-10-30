// 说明：保存三维分数及与实际总分一致的评分解释。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Zhijian.Domain.Results
{
    public enum ScoreDimension { Evidence, Permission, Disposition }

    public sealed class ScoreEntry
    {
        public string RuleId { get; }
        public ScoreDimension Dimension { get; }
        public int Delta { get; }
        public string Reason { get; }
        public string RelatedItemId { get; }
        public string Recommendation { get; }

        public ScoreEntry(string ruleId, ScoreDimension dimension, int delta, string reason,
            string relatedItemId, string recommendation = null)
        {
            RuleId = ruleId;
            Dimension = dimension;
            Delta = delta;
            Reason = reason;
            RelatedItemId = relatedItemId;
            Recommendation = recommendation;
        }
    }

    public sealed class ScoreBreakdown
    {
        public int EvidenceScore { get; }
        public int PermissionScore { get; }
        public int DispositionScore { get; }
        public int Total => EvidenceScore + PermissionScore + DispositionScore;
        public IReadOnlyList<ScoreEntry> Entries { get; }

        public ScoreBreakdown(int evidenceScore, int permissionScore, int dispositionScore,
            IEnumerable<ScoreEntry> entries)
        {
            if (evidenceScore < 0 || evidenceScore > 40 || permissionScore < 0 || permissionScore > 30 ||
                dispositionScore < 0 || dispositionScore > 30)
                throw new ArgumentOutOfRangeException(nameof(evidenceScore), "分项分数必须位于 40/30/30 上限内。");
            var copy = (entries ?? throw new ArgumentNullException(nameof(entries))).ToArray();
            if (copy.Any(entry => entry == null || !Enum.IsDefined(typeof(ScoreDimension), entry.Dimension)))
                throw new ArgumentException("评分明细包含空项或未知维度。", nameof(entries));
            if (copy.Where(entry => entry.Dimension == ScoreDimension.Evidence).Sum(entry => entry.Delta) != evidenceScore ||
                copy.Where(entry => entry.Dimension == ScoreDimension.Permission).Sum(entry => entry.Delta) != permissionScore ||
                copy.Where(entry => entry.Dimension == ScoreDimension.Disposition).Sum(entry => entry.Delta) != dispositionScore)
                throw new ArgumentException("评分明细与实际分项合计不一致。", nameof(entries));
            EvidenceScore = evidenceScore;
            PermissionScore = permissionScore;
            DispositionScore = dispositionScore;
            Entries = new ReadOnlyCollection<ScoreEntry>(copy);
        }
    }
}
