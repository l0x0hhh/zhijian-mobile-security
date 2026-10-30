// 说明：提供选关查询，复制目录并验证稳定 ID，防止界面修改内容配置。
using System;
using System.Collections.Generic;
using Zhijian.Application.Ports;
using Zhijian.Domain.Cases;

namespace Zhijian.Application.Cases
{
    public sealed class CaseSelectionService
    {
        private readonly IReadOnlyList<CaseSummary> cases;
        private readonly Dictionary<string, CaseSummary> byId = new Dictionary<string, CaseSummary>(StringComparer.Ordinal);
        public IReadOnlyList<CaseSummary> Cases => cases;

        public CaseSelectionService(ICaseRepository repository)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var entries = repository.GetSummaries() ?? throw new InvalidOperationException("Case catalog is null.");
            var copy = new List<CaseSummary>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry == null || byId.ContainsKey(entry.Id)) throw new InvalidOperationException("Case catalog contains null or duplicate ids.");
                byId.Add(entry.Id, entry);
                copy.Add(entry);
            }
            cases = copy.AsReadOnly();
        }

        public bool TryGetCase(string id, out CaseSummary value)
        {
            value = null;
            return id != null && byId.TryGetValue(id, out value);
        }
    }
}
