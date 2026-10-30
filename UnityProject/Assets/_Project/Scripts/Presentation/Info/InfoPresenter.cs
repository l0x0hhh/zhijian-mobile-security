// 说明：提供帮助、主题设置和明确的未开放说明，不生成虚构进度或成绩。
using System;
using Zhijian.Application.Cases;
using Zhijian.Presentation.Navigation;

namespace Zhijian.Presentation.Info
{
    public interface IThemePreferences
    {
        bool IsDark { get; }
        void SetDark(bool value);
    }

    public sealed class InfoViewData
    {
        public string Title { get; }
        public string Lead { get; }
        public string Body { get; }
        public string ActionLabel { get; }
        public InfoViewData(string title, string lead, string body, string actionLabel)
        { Title = title; Lead = lead; Body = body; ActionLabel = actionLabel; }
    }

    public interface IInfoView
    {
        event Action ActionRequested;
        void Render(InfoViewData data);
    }

    public sealed class InfoPresenter : IDisposable
    {
        private readonly IInfoView view;
        private readonly IPageNavigator navigator;
        private readonly IThemePreferences theme;
        private readonly CaseSelectionService cases;
        private NavigationRequest request;
        private bool bound;
        public InfoPresenter(IInfoView view, IPageNavigator navigator, IThemePreferences theme, CaseSelectionService cases)
        { this.view = view; this.navigator = navigator; this.theme = theme; this.cases = cases; }

        public void Bind(NavigationRequest route)
        {
            if (bound) throw new InvalidOperationException("Info presenter is already bound.");
            request = route;
            bound = true;
            view.ActionRequested += OnAction;
            Render();
        }

        private void Render()
        {
            var caseName = cases.TryGetCase(request.CaseId, out var entry) ? entry.Title : "安全练习";
            InfoViewData data;
            switch (request.Page)
            {
                case PageId.Settings:
                    data = new InfoViewData("设置", "外观", "当前使用" + (theme.IsDark ? "夜间" : "日间") +
                        "配色。本次选择在关闭应用后重置。\n\n所有情境采用应用内模拟，无需读取您的通讯录、短信、相册或录音。",
                        theme.IsDark ? "切换为日间" : "切换为夜间"); break;
                case PageId.Records:
                    data = new InfoViewData("练习记录", "暂无练习记录", "完成情境后，这里将展示成绩和复盘。\n\n当前版本支持浏览调查入口，完整练习尚未开放。", "返回练习"); break;
                case PageId.Help:
                    data = new InfoViewData("关于智鉴", "使用说明", "智鉴是一款移动安全训练应用，围绕语音冒充、换脸会议和过度授权展开。\n\n您将在虚拟手机中查看材料、收集线索、核验来源并选择处置。所有检测与危险操作均为教学模拟。\n\n从情境入口开始调查；仅标记为正式练习的案件计分。资料页可整理线索，权限页逐项判断，最后在“调查结论”提交。保存状态以记录页提示为准。", "返回上一页"); break;
                case PageId.Chat:
                    data = Preview("聊天", caseName, request.CaseId == "photo-permission" ?
                        "此情境从应用信息和权限审查开始，暂无聊天材料。" : "聊天内容暂未开放。正式练习中可查看文字、语音与视频，并从消息中收集线索。"); break;
                case PageId.Album:
                    data = Preview("相册", "暂无情境图片", "这里展示情境预置图片，不读取手机真实相册。"); break;
                case PageId.SecurityCenter:
                    data = Preview("安全中心", "核验与权限审查", "查看应用请求的访问范围，核实身份或来源。完整调查功能暂未开放。"); break;
                case PageId.Files:
                    data = Preview("文件管理", "暂无情境文件", "这里展示模拟安装包和来源信息，不安装真实应用。"); break;
                case PageId.Evidence:
                    data = Preview("已收集线索", "暂无线索", "正式练习中收集的线索将保存在这里。本次预览不产生调查记录。"); break;
                default: throw new ArgumentOutOfRangeException(nameof(request.Page));
            }
            view.Render(data);
        }

        private static InfoViewData Preview(string title, string lead, string body)
            => new InfoViewData(title, lead, body, "返回调查手机");

        private void OnAction()
        {
            if (request.Page == PageId.Settings) { theme.SetDark(!theme.IsDark); Render(); }
            else if (request.Page == PageId.Records) navigator.Home();
            else navigator.Back();
        }

        public void Dispose()
        {
            if (!bound) return;
            bound = false;
            view.ActionRequested -= OnAction;
        }
    }
}
