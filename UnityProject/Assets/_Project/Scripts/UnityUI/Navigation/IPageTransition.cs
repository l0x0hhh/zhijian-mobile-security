// 说明：视觉过渡只借用页面；路由独占停用与释放职责，取消句柄不拥有页面资源。
using System;
using Zhijian.Presentation.Navigation;

namespace Zhijian.UnityUI.Navigation
{
    public enum PageTransitionKind { Replace, Push, Pop }

    public interface IPageTransition
    {
        // 完成回调可同步或异步；取消后到达的旧回调由路由忽略。
        IDisposable Begin(IPageHandle outgoing, IPageHandle incoming, PageTransitionKind kind, Action completed);
    }
}
