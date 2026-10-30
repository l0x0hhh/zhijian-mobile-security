// 说明：编辑器菜单复用同一内容加载与领域校验，分别列出配置、素材和开放状态。
using UnityEditor;
using UnityEngine;
using Zhijian.Domain.Outcomes;
using Zhijian.Infrastructure.Content;

namespace Zhijian.Editor.Content
{
    public static class CaseValidationMenu
    {
        [MenuItem("智鉴/检查案件内容")]
        private static void Validate()
        {
            var resolver = new OutcomeResolver(new ICaseOutcomePolicy[]
            { new VoiceLoanOutcomePolicy(), new FaceMeetingOutcomePolicy(), new PhotoPermissionOutcomePolicy() });
            var catalogs = AssetDatabase.FindAssets("t:CaseCatalogAsset");
            if (catalogs.Length == 0) { Debug.LogWarning("未找到智鉴案件目录。"); return; }
            foreach (var id in catalogs)
            {
                var catalog = AssetDatabase.LoadAssetAtPath<CaseCatalogAsset>(AssetDatabase.GUIDToAssetPath(id));
                var repository = new CaseRepository(catalog, resolver);
                foreach (var caseId in repository.CaseIds)
                {
                    repository.TryGetCase(caseId, out var loaded);
                    Debug.Log(caseId + "：定义有效=" + loaded.DefinitionValid + "，允许开始=" + loaded.CanStartTraining, catalog);
                    foreach (var problem in loaded.Problems)
                        Debug.LogWarning("[" + problem.Kind + "] " + problem.Code + " / " + problem.ItemId + "：" + problem.Message, catalog);
                }
            }
        }
    }
}
