// 说明：不可变证据定义支持替代取证入口和必须同时查看的来源，所有入口共用证据 ID。
using System.Collections.Generic;
using System.Linq;

namespace Zhijian.Domain.Cases
{
    public sealed class EvidenceDefinition
    {
        public EvidenceDefinition(string id, string sourceId, string title, string explanation, bool isKey, int scoreWeight,
            IEnumerable<string> alternativeSourceIds = null, IEnumerable<string> requiredSourceIds = null)
        {
            Id = id; SourceId = sourceId; Title = title; Explanation = explanation; IsKey = isKey; ScoreWeight = scoreWeight;
            var sources = new List<string> { sourceId };
            if (alternativeSourceIds != null) sources.AddRange(alternativeSourceIds);
            SourceIds = sources.AsReadOnly();
            RequiredSourceIds = DefinitionCopies.Values(requiredSourceIds);
        }
        public string Id { get; }
        public string SourceId { get; }
        public IReadOnlyList<string> SourceIds { get; }
        public IReadOnlyList<string> RequiredSourceIds { get; }
        public string Title { get; }
        public string Explanation { get; }
        public bool IsKey { get; }
        public int ScoreWeight { get; }
        internal EvidenceDefinition Copy()
        { return new EvidenceDefinition(Id, SourceId, Title, Explanation, IsKey, ScoreWeight, SourceIds.Skip(1), RequiredSourceIds); }
    }
}
