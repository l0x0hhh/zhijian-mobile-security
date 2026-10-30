// 说明：加载独立只读的目录与完整案件，配置、素材或发布门槛未满足时保持预览。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Application.Cases;
using Zhijian.Application.Ports;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Outcomes;

namespace Zhijian.Infrastructure.Content
{
    public sealed class CaseRepository : ICaseRepository, IInvestigationCaseRepository
    {
        private readonly IReadOnlyList<CaseSummary> summaries;
        private readonly IReadOnlyList<string> caseIds;
        private readonly Dictionary<string, CaseLoadResult> content = new Dictionary<string, CaseLoadResult>(StringComparer.Ordinal);
        public IReadOnlyList<string> CaseIds => caseIds;
        public CaseRepository(CaseCatalogAsset catalog, OutcomeResolver resolver)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            var entries = catalog.CreateSummaries();
            var safeSummaries = new List<CaseSummary>();
            foreach (var entry in entries)
            {
                if (content.ContainsKey(entry.Id)) throw new InvalidOperationException("情境目录 ID 重复：" + entry.Id);
                var loaded = catalog.LoadContent(entry.Id, resolver);
                content.Add(entry.Id, loaded);
                safeSummaries.Add(new CaseSummary(entry.Id, entry.Title, entry.Summary,
                    loaded.CanStartTraining ? CaseAvailability.Available : CaseAvailability.Preview));
            }
            summaries = safeSummaries.AsReadOnly();
            caseIds = Array.AsReadOnly(entries.Select(item => item.Id).ToArray());
        }
        public IReadOnlyList<CaseSummary> GetSummaries() => summaries;
        public bool TryGetCase(string caseId, out CaseLoadResult result)
        { result = null; return caseId != null && content.TryGetValue(caseId, out result); }
    }
}
