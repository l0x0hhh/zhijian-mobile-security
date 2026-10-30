// 说明：公共标题栏、返回和底部导航，页面内容挂载到统一容器。
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zhijian.Presentation.Navigation;

namespace Zhijian.UnityUI.Views
{
    public sealed class PhoneShellView : MonoBehaviour
    {
        [SerializeField] private RectTransform pageHost;
        [SerializeField] private TMP_Text pageTitle;
        [SerializeField] private Button backButton;
        [SerializeField] private Button helpButton;
        [SerializeField] private Button practiceButton;
        [SerializeField] private Button recordsButton;
        [SerializeField] private GameObject practiceIndicator;
        [SerializeField] private GameObject recordsIndicator;
        public RectTransform PageHost => pageHost;
        public event Action BackRequested, HelpRequested, PracticeRequested, RecordsRequested;
        private void Awake()
        {
            backButton.onClick.AddListener(OnBack);
            helpButton.onClick.AddListener(OnHelp);
            practiceButton.onClick.AddListener(OnPractice);
            recordsButton.onClick.AddListener(OnRecords);
        }
        public void RenderRoute(NavigationRequest request, bool canGoBack)
        {
            pageTitle.text = request.Page == PageId.Home ? "智鉴" : request.Page == PageId.Phone ? "调查手机" : "智鉴";
            backButton.gameObject.SetActive(canGoBack);
            practiceIndicator.SetActive(request.Page != PageId.Records && request.Page != PageId.RecordDetail);
            recordsIndicator.SetActive((request.Page == PageId.Records || request.Page == PageId.RecordDetail));
        }
        private void OnBack() => BackRequested?.Invoke();
        private void OnHelp() => HelpRequested?.Invoke();
        private void OnPractice() => PracticeRequested?.Invoke();
        private void OnRecords() => RecordsRequested?.Invoke();
        private void OnDestroy()
        {
            backButton.onClick.RemoveListener(OnBack); helpButton.onClick.RemoveListener(OnHelp);
            practiceButton.onClick.RemoveListener(OnPractice); recordsButton.onClick.RemoveListener(OnRecords);
        }
    }
}
