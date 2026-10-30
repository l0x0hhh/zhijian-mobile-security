// 说明：行组件只更新变化字段，按钮监听注册一次并读取最新操作，复用后清除旧上下文。
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zhijian.Presentation.Investigation;
using Zhijian.UnityUI.Theme;

namespace Zhijian.UnityUI.Views.Investigation
{
    [Flags]
    public enum RowUpdate { None = 0, Layout = 1, Appearance = 2 }

    public sealed class InvestigationRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title, body, secondaryLabel;
        [SerializeField] private Button primary, secondary;
        [SerializeField] private ThemeBinding background;
        private InvestigationRow current;
        private Action<PageAction> requested;
        public string SourceId => current?.ReadSourceId;
        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            primary.onClick.AddListener(OnPrimary);
            secondary.onClick.AddListener(OnSecondary);
        }

        public RowUpdate Render(InvestigationRow data, Action<PageAction> requested)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var update = RowUpdate.None;
            if (SetText(title, data.Title)) update |= RowUpdate.Layout;
            if (SetText(body, data.Body)) update |= RowUpdate.Layout;
            if (SetActive(body.gameObject, !string.IsNullOrEmpty(data.Body))) update |= RowUpdate.Layout;
            if (SetText(secondaryLabel, data.SecondaryText)) update |= RowUpdate.Layout;
            if (SetActive(secondary.gameObject, data.SecondaryAction != null)) update |= RowUpdate.Layout;
            var canPrimary = data.Enabled && data.Action != null;
            var canSecondary = data.Enabled && data.SecondaryAction != null;
            if (primary.interactable != canPrimary) primary.interactable = canPrimary;
            if (secondary.interactable != canSecondary) secondary.interactable = canSecondary;
            if (current == null || current.Tone != data.Tone)
            {
                background.SetRole(data.Tone == RowTone.Plain ? ThemeRole.Paper : ThemeRole.Soft);
                update |= RowUpdate.Appearance;
            }
            // 不捕获第一次渲染的数据；每次点击都读取最新的只读操作。
            current = data;
            this.requested = requested;
            return update;
        }

        public void Unbind()
        {
            current = null;
            requested = null;
            primary.interactable = false;
            secondary.interactable = false;
        }

        private void OnPrimary()
        {
            if (current != null && current.Enabled && current.Action != null) requested?.Invoke(current.Action);
        }

        private void OnSecondary()
        {
            if (current != null && current.Enabled && current.SecondaryAction != null) requested?.Invoke(current.SecondaryAction);
        }

        private static bool SetText(TMP_Text label, string value)
        {
            value = value ?? string.Empty;
            if (label.text == value) return false;
            label.text = value;
            return true;
        }

        private static bool SetActive(GameObject target, bool value)
        {
            if (target.activeSelf == value) return false;
            target.SetActive(value);
            return true;
        }

        private void OnDestroy()
        {
            if (primary != null) primary.onClick.RemoveListener(OnPrimary);
            if (secondary != null) secondary.onClick.RemoveListener(OnSecondary);
            current = null;
            requested = null;
        }
    }
}
