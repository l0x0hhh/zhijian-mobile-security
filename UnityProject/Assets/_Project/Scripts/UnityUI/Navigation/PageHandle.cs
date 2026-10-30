// 说明：业务停用立即解除订阅和媒体任务，画面可以独立保留；最终释放幂等且关闭输入。
using System;
using UnityEngine;
using Zhijian.Presentation.Navigation;

namespace Zhijian.UnityUI.Navigation
{
    public sealed class PageHandle : IPageHandle
    {
        private GameObject root;
        private readonly IDisposable presenter;
        private readonly Action<NavigationRequest> show;
        private readonly Action<bool> presentationChanged;
        private readonly CanvasGroup input;
        private bool shown, deactivated, disposed;

        // 仅供 Unity 视觉适配器借用；销毁仍由 PageHandle 负责。
        public GameObject Root => root;

        public PageHandle(GameObject root, IDisposable presenter, Action<NavigationRequest> show,
            Action<bool> presentationChanged = null)
        {
            this.root = root != null ? root : throw new ArgumentNullException(nameof(root));
            this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            this.show = show ?? throw new ArgumentNullException(nameof(show));
            this.presentationChanged = presentationChanged;
            input = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            SetPresentationReady(false);
        }

        public void Show(NavigationRequest request)
        {
            if (disposed || deactivated) throw new ObjectDisposedException(nameof(PageHandle));
            if (shown) throw new InvalidOperationException("页面只能绑定一次。");
            shown = true;
            show(request);
        }

        public void SetPresentationReady(bool ready)
        {
            var available = ready && !deactivated && !disposed;
            if (input != null)
            {
                input.interactable = available;
                input.blocksRaycasts = available;
            }
            presentationChanged?.Invoke(available);
        }

        public void Deactivate()
        {
            if (deactivated) return;
            deactivated = true;
            try { SetPresentationReady(false); }
            finally { presenter.Dispose(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { Deactivate(); }
            finally
            {
                if (root != null)
                {
                    root.SetActive(false);
                    UnityEngine.Object.Destroy(root);
                    root = null;
                }
            }
        }
    }
}
