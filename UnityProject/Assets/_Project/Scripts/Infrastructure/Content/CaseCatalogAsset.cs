// 说明：编辑情境摘要与完整内容引用；资产不保存玩家进度或替代资源校验。
using System;
using System.Linq;
using Zhijian.Application.Cases;
using Zhijian.Domain.Outcomes;
using UnityEngine;
using Zhijian.Domain.Cases;

namespace Zhijian.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Zhijian/Case Catalog", fileName = "CaseCatalog")]
    public sealed class CaseCatalogAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string title;
            [TextArea(2, 4)] public string summary;
            public CaseAvailability availability;
            public CaseAsset content;
        }
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public bool TryGetAsset(string caseId, out CaseAsset asset)
        { asset = entries?.FirstOrDefault(item => item != null && item.id == caseId)?.content; return asset != null; }

        public CaseLoadResult LoadContent(string caseId, OutcomeResolver resolver)
        {
            var entry = entries?.FirstOrDefault(item => item != null && item.id == caseId);
            if (entry == null || entry.content == null)
                return new CaseLoadResult(caseId, null, new[] { new ContentProblem(ContentProblemKind.Definition,
                    "content.asset-missing", caseId, "案件完整内容资产未配置。") });
            return entry.content.Load(caseId, resolver);
        }

        public CaseSummary[] CreateSummaries()
        {
            if (entries == null) throw new InvalidOperationException("Case catalog entries are missing.");
            var result = new CaseSummary[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i] ?? throw new InvalidOperationException("Case catalog contains an empty entry.");
                result[i] = new CaseSummary(entry.id, entry.title, entry.summary, entry.availability);
            }
            return result;
        }
    }
}
