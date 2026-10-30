// 说明：显示帮助、设置和未开放说明，不在 View 中决定业务规则。
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zhijian.Presentation.Info;

namespace Zhijian.UnityUI.Views.Info
{
    public sealed class InfoView : MonoBehaviour, IInfoView
    {
        [SerializeField] private TMP_Text heading;
        [SerializeField] private TMP_Text lead;
        [SerializeField] private TMP_Text body;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private Button actionButton;
        public event Action ActionRequested;
        private void Awake() { actionButton.onClick.AddListener(OnAction); }
        public void Render(InfoViewData data)
        { heading.text = data.Title; lead.text = data.Lead; body.text = data.Body; actionLabel.text = data.ActionLabel; }
        private void OnAction() => ActionRequested?.Invoke();
        private void OnDestroy() { actionButton.onClick.RemoveListener(OnAction); ActionRequested = null; }
    }
}
