// 说明：可复用的情境列表行，仅呈现数据和转发点击。
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zhijian.Presentation.Home;

namespace Zhijian.UnityUI.Views.Home
{
    public sealed class CaseRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text summary;
        [SerializeField] private TMP_Text status;
        private string caseId;
        public event Action<string> Selected;
        private void Awake() { button.onClick.AddListener(OnClick); }
        public void Render(CaseRowData data)
        {
            caseId = data.Id;
            title.text = data.Title;
            summary.text = data.Summary;
            status.text = data.Status + "  ›";
        }
        private void OnClick() { if (caseId != null) Selected?.Invoke(caseId); }
        private void OnDestroy() { button.onClick.RemoveListener(OnClick); Selected = null; }
    }
}
