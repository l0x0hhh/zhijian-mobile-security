// 说明：进入检查工具或更新页面时处理未结束调查，避免遗失训练状态。
using Zhijian.Presentation.Investigation;
namespace Zhijian.Presentation.Navigation
{
    public sealed class CaseAwareNavigator : IPageNavigator
    {
        private readonly IPageHistory pages;
        private readonly InvestigationWorkspace workspace;
        private readonly IConfirmationService dialogs;
        public CaseAwareNavigator(IPageHistory pages, InvestigationWorkspace workspace, IConfirmationService dialogs)
        { this.pages = pages; this.workspace = workspace; this.dialogs = dialogs; }
        public void Home() => Open(new NavigationRequest(PageId.Home));
        public void Open(NavigationRequest request)
        {
            if (ShouldConfirm(request))
            { dialogs.Show(new Confirmation("退出本次调查？", "进行中的线索和选择不会保存。已结算的成绩不受影响。", "退出调查", "继续调查"), yes => { if (yes) { workspace.Leave(); Navigate(request); } }); return; }
            Navigate(request);
        }
        private void Navigate(NavigationRequest request)
        {
            if (request.Page == PageId.Phone) workspace.Enter(request.CaseId);
            else if (LeavesInvestigation(request.Page)) workspace.Leave();
            pages.Open(request);
        }
        public bool Back()
        {
            if (dialogs.CloseTop()) return true;
            if (pages.Previous == null) return false;
            if (ShouldConfirm(pages.Previous))
            { dialogs.Show(new Confirmation("退出本次调查？", "进行中的线索和选择不会保存。", "退出调查", "继续调查"), yes => { if (yes) { workspace.Leave(); pages.Back(); } }); return true; }
            if (LeavesInvestigation(pages.Previous.Page)) workspace.Leave();
            return pages.Back();
        }
        private bool ShouldConfirm(NavigationRequest request)
            => workspace.CanChange && (LeavesInvestigation(request.Page) ||
                (request.Page == PageId.Phone && request.CaseId != workspace.CaseId));
        private static bool LeavesInvestigation(PageId page) => page == PageId.Home || page == PageId.Records ||
            page == PageId.ApkInspection || page == PageId.ApkReports || page == PageId.ApkReportDetail || page == PageId.Updates;
    }
}
