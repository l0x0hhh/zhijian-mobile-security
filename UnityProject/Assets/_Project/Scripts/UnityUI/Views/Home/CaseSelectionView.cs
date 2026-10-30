// 说明：按目录动态实例化列表行，新增同类案件无需修改页面布局。
using System;
using System.Collections.Generic;
using UnityEngine;
using Zhijian.Presentation.Home;

namespace Zhijian.UnityUI.Views.Home
{
    public sealed class CaseSelectionView : MonoBehaviour, ICaseSelectionView
    {
        [SerializeField] private RectTransform listContent;
        [SerializeField] private CaseRowView rowPrefab;
        [SerializeField] private float rowHeight = 288;
        private readonly List<CaseRowView> rows = new List<CaseRowView>();
        public event Action<string> CaseSelected;
        public void Render(CaseSelectionViewData data)
        {
            ClearRows();
            for (var i = 0; i < data.Cases.Count; i++)
            {
                var row = Instantiate(rowPrefab, listContent, false);
                var rect = (RectTransform)row.transform;
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -i * rowHeight);
                rect.sizeDelta = new Vector2(0, rowHeight);
                row.Render(data.Cases[i]);
                row.Selected += ForwardSelection;
                rows.Add(row);
            }
            listContent.sizeDelta = new Vector2(0, data.Cases.Count * rowHeight);
        }
        private void ForwardSelection(string id) => CaseSelected?.Invoke(id);
        private void ClearRows()
        {
            foreach (var row in rows)
            {
                row.Selected -= ForwardSelection;
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            rows.Clear();
        }
        private void OnDestroy() { ClearRows(); CaseSelected = null; }
    }
}
