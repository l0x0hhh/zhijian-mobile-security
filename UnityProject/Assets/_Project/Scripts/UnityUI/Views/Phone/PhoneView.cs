// 说明：证件照案突出开始调查与线索入口，收起无关入口并复用原有布局位置。
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zhijian.Presentation.Navigation;
using Zhijian.Presentation.Phone;

namespace Zhijian.UnityUI.Views.Phone
{
    public sealed class PhoneView : MonoBehaviour, IPhoneView
    {
        [Serializable] private sealed class AppButton { public Button button; public PageId page; }
        [SerializeField] private TMP_Text caseTitle;
        [SerializeField] private TMP_Text caseSummary;
        [SerializeField] private AppButton[] apps;
        private UnityAction[] listeners;
        private Vector2[] appPositions;
        private string[] appLabels;
        private RectTransform content;
        private float contentHeight;
        public event Action<PageId> AppSelected;
        private void Awake()
        {
            listeners = new UnityAction[apps.Length];
            appPositions = new Vector2[apps.Length];
            appLabels = new string[apps.Length];
            if (apps.Length > 0)
            {
                content = apps[0].button.transform.parent as RectTransform;
                contentHeight = content != null ? content.rect.height : 0;
            }
            for (var i = 0; i < apps.Length; i++)
            {
                appPositions[i] = ((RectTransform)apps[i].button.transform).anchoredPosition;
                var label = apps[i].button.GetComponentInChildren<TMP_Text>(true);
                appLabels[i] = label != null ? label.text : string.Empty;
                var page = apps[i].page;
                listeners[i] = () => AppSelected?.Invoke(page);
                apps[i].button.onClick.AddListener(listeners[i]);
            }
        }
        public void Render(PhoneViewData data)
        {
            caseTitle.text = data.Title;
            caseSummary.text = data.IsPhoto ? "你要制作一张证件照。先从应用详情开始，看看它为什么索取七项访问。" : data.Summary;
            var visible = 0;
            for (var i = 0; i < apps.Length; i++)
            {
                var app = apps[i];
                var show = !data.IsPhoto || app.page == PageId.Chat || app.page == PageId.Evidence || app.page == PageId.Settings;
                app.button.gameObject.SetActive(show);
                var rect = (RectTransform)app.button.transform;
                var slot = data.IsPhoto ? (app.page == PageId.Chat ? 0 : app.page == PageId.Evidence ? 1 : 2) : i;
                rect.anchoredPosition = show ? appPositions[slot] : appPositions[i];
                if (show) visible++;
                var label = app.button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = data.IsPhoto && app.page == PageId.Chat ? "开始调查 · 应用详情" :
                        data.IsPhoto && app.page == PageId.Evidence ? "线索夹 · 回看依据" : appLabels[i];
            }
            // 现有预制体为等距手工排版，收起入口后同步缩短滚动内容；其他案件恢复原高度。
            if (content != null && apps.Length > 1)
            {
                var spacing = Mathf.Abs(appPositions[1].y - appPositions[0].y);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight - (apps.Length - visible) * spacing);
            }
        }
        private void OnDestroy()
        {
            if (listeners != null)
                for (var i = 0; i < listeners.Length; i++) if (apps[i].button != null) apps[i].button.onClick.RemoveListener(listeners[i]);
            AppSelected = null;
        }
    }
}
