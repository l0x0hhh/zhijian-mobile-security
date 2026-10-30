// 说明：页面通过显式工厂注册，组装委托由 Bootstrap 提供。
using System;
using System.Collections.Generic;
using Zhijian.Presentation.Navigation;

namespace Zhijian.UnityUI.Navigation
{
    public sealed class PageFactory
    {
        private readonly Dictionary<PageId, Func<IPageHandle>> factories = new Dictionary<PageId, Func<IPageHandle>>();
        public void Register(PageId page, Func<IPageHandle> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            factories.Add(page, factory);
        }
        public IPageHandle Create(PageId page)
        {
            if (!factories.TryGetValue(page, out var factory)) throw new InvalidOperationException("Page is not registered: " + page);
            return factory();
        }
    }
}
