// 说明：追加更新检查路由，保留既有页面编号和呈现生命周期。
using System;

namespace Zhijian.Presentation.Navigation
{
    public enum PageId { Home, Phone, Chat, Album, SecurityCenter, Files, Settings, Evidence, Records, Help, Source, Media, Detection, Permissions, Verification, Conclusion, Result, AppDetails, PrivacyPolicy, Reference, PermissionDetail, RecordDetail, ApkInspection, ApkReports, ApkReportDetail, Updates }

    public sealed class NavigationRequest
    {
        public PageId Page { get; }
        public string CaseId { get; }
        public string ItemId { get; }
        public NavigationRequest(PageId page, string caseId = null, string itemId = null)
        {
            if (!Enum.IsDefined(typeof(PageId), page)) throw new ArgumentOutOfRangeException(nameof(page));
            Page = page;
            CaseId = caseId; ItemId = itemId;
        }
    }

    public interface IPageNavigator
    {
        void Open(NavigationRequest request);
        void Home();
        bool Back();
    }

    public interface IPageHandle : IDisposable
    {
        void Show(NavigationRequest request);
        void SetPresentationReady(bool ready);
        void Deactivate();
    }
}
