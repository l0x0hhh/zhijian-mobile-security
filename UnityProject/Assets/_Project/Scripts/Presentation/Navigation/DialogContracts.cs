// 说明：确认弹层只表达显示和选择，不携带引擎对象或隐式业务动作。
using System;
namespace Zhijian.Presentation.Navigation
{
    public sealed class Confirmation
    {
        public string Title { get; }
        public string Body { get; }
        public string ConfirmText { get; }
        public string CancelText { get; }
        public Confirmation(string title, string body, string confirmText, string cancelText = "取消")
        { Title = title; Body = body; ConfirmText = confirmText; CancelText = cancelText; }
    }
    public interface IConfirmationService
    {
        IDisposable Show(Confirmation data, Action<bool> completed);
        bool CloseTop();
    }
    public interface IPageHistory : IPageNavigator
    {
        NavigationRequest Current { get; }
        NavigationRequest Previous { get; }
    }
}
