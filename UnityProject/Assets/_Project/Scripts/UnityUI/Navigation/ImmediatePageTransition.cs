// 说明：当前立即完成视觉切换；后续动效通过同一接口替换，不延迟调查或媒体退出。
using System;
using Zhijian.Presentation.Navigation;

namespace Zhijian.UnityUI.Navigation
{
    public sealed class ImmediatePageTransition : IPageTransition
    {
        public IDisposable Begin(IPageHandle outgoing, IPageHandle incoming, PageTransitionKind kind, Action completed)
        {
            completed();
            return null;
        }
    }
}
