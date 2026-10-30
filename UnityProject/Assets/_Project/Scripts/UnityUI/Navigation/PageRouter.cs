// 说明：相同案件与条目的旧页面回退复用导航位置，避免交叉跳转重复入栈；保留过渡生命周期。
using System;
using System.Collections.Generic;
using Zhijian.Presentation.Navigation;

namespace Zhijian.UnityUI.Navigation
{
    public sealed class PageRouter : IPageHistory, IDisposable
    {
        private sealed class VisualTransition
        {
            public IPageHandle Outgoing;
            public IDisposable Cancellation;
            public bool Finished;
        }

        private readonly PageFactory factory;
        private readonly IPageTransition transitions;
        private readonly List<NavigationRequest> history = new List<NavigationRequest>();
        private IPageHandle active;
        private VisualTransition pending;
        private bool navigating, disposed;
        public NavigationRequest Current => history.Count == 0 ? null : history[history.Count - 1];
        public NavigationRequest Previous => history.Count < 2 ? null : history[history.Count - 2];
        public event Action<NavigationRequest, bool> Changed;

        public PageRouter(PageFactory factory, IPageTransition transitions)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        public void Home() => Open(new NavigationRequest(PageId.Home));
        public void Open(NavigationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (disposed || navigating) return;
            if (SameRoute(Current, request)) return;
            var next = new List<NavigationRequest>(history);
            var replace = request.Page == PageId.Home || request.Page == PageId.Phone || request.Page == PageId.Records || request.Page == PageId.Result;
            if (!replace)
            {
                var existing = next.FindLastIndex(item => SameRoute(item, request));
                if (existing >= 0)
                {
                    next.RemoveRange(existing + 1, next.Count - existing - 1);
                    Navigate(next, PageTransitionKind.Pop);
                    return;
                }
            }
            if (replace) next.Clear();
            if (replace && request.Page != PageId.Home) next.Add(new NavigationRequest(PageId.Home));
            if (next.Count >= 16) next.RemoveAt(1);
            next.Add(request);
            Navigate(next, replace ? PageTransitionKind.Replace : PageTransitionKind.Push);
        }

        private static bool SameRoute(NavigationRequest left, NavigationRequest right)
            => left != null && left.Page == right.Page && left.CaseId == right.CaseId && left.ItemId == right.ItemId;

        public bool Back()
        {
            if (disposed || navigating || history.Count <= 1) return false;
            var next = new List<NavigationRequest>(history);
            next.RemoveAt(next.Count - 1);
            Navigate(next, PageTransitionKind.Pop);
            return true;
        }

        private void Navigate(List<NavigationRequest> next, PageTransitionKind kind)
        {
            navigating = true;
            IPageHandle replacement = null;
            try
            {
                CancelVisualTransition();
                active?.SetPresentationReady(false);
                var request = next[next.Count - 1];
                replacement = factory.Create(request.Page);
                replacement.SetPresentationReady(false);
                replacement.Show(request);

                // 先终止旧订阅/播放，再允许任何异步视觉过渡开始。
                var outgoing = active;
                try { outgoing?.Deactivate(); }
                catch
                {
                    active = null;
                    try { outgoing?.Dispose(); }
                    finally { history.Clear(); }
                    throw;
                }
                active = replacement;
                replacement = null;
                history.Clear(); history.AddRange(next);
                var visual = new VisualTransition { Outgoing = outgoing };
                pending = visual;
                try
                {
                    Changed?.Invoke(request, history.Count > 1);
                    var cancellation = transitions.Begin(outgoing, active, kind, () => CompleteVisualTransition(visual));
                    if (visual.Finished) cancellation?.Dispose();
                    else visual.Cancellation = cancellation;
                }
                catch
                {
                    CompleteVisualTransition(visual);
                    throw;
                }
            }
            finally
            {
                try { replacement?.Dispose(); }
                finally
                {
                    navigating = false;
                    // 创建或绑定失败时旧页面仍可使用；成功时只有过渡完成才就绪。
                    if (!disposed && pending == null) active?.SetPresentationReady(true);
                }
            }
        }

        private void CompleteVisualTransition(VisualTransition visual)
        {
            if (disposed || visual.Finished || !ReferenceEquals(pending, visual)) return;
            visual.Finished = true;
            pending = null;
            try { visual.Cancellation?.Dispose(); }
            finally
            {
                try { visual.Outgoing?.Dispose(); }
                finally { if (!disposed) active?.SetPresentationReady(true); }
            }
        }

        private void CancelVisualTransition()
        {
            var visual = pending;
            if (visual == null) return;
            pending = null;
            visual.Finished = true;
            // 先失效旧回调，再取消视觉任务；退出中的页面只由路由释放一次。
            try { visual.Cancellation?.Dispose(); }
            finally { visual.Outgoing?.Dispose(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { CancelVisualTransition(); }
            finally
            {
                try { active?.Dispose(); }
                finally { active = null; history.Clear(); Changed = null; }
            }
        }
    }
}
