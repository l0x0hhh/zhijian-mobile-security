// 说明：把页面操作转为带会话标识的命令；离页取消任务，确认提交使用原状态版本。
using System;
using System.Linq;
using System.Threading.Tasks;
using Zhijian.Application.Investigation;
using Zhijian.Application.Ports;
using Zhijian.Application.Progress;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Sessions;
using Zhijian.Presentation.Media;
using Zhijian.Presentation.Navigation;
namespace Zhijian.Presentation.Investigation
{
    public sealed class InvestigationPagePresenter : IDisposable
    {
        private readonly IInvestigationPageView view;
        private readonly InvestigationWorkspace workspace;
        private readonly IPageNavigator navigator;
        private readonly IConfirmationService dialogs;
        private readonly IProgressService progress;
        private NavigationRequest route;
        private MediaRequest playing, scanning;
        private IDisposable confirmation;
        private string notice;
        private bool bound, disposed;
        public InvestigationPagePresenter(IInvestigationPageView view, InvestigationWorkspace workspace, IPageNavigator navigator,
            IConfirmationService dialogs, IProgressService progress)
        { this.view=view; this.workspace=workspace; this.navigator=navigator; this.dialogs=dialogs; this.progress=progress; }
        public void Bind(NavigationRequest request)
        {
            if (bound) throw new InvalidOperationException("页面不能重复绑定。");
            bound=true; route=request;
            view.ActionRequested += OnAction; view.SourceRead += OnRead;
            view.Playback.Changed += OnPlayback; view.Detection.Changed += OnDetection;
            workspace.Changed += Render;
            if (progress != null) progress.Changed += OnProgress;
            Render();
            if (route.Page == PageId.Media && workspace.Content?.Definition.Media.FirstOrDefault(x => x.Id == route.ItemId)?.Kind == MediaKind.Image)
                StartMedia(route.ItemId, false);
            if (route.Page == PageId.Album && workspace.CaseId == "photo-permission") StartMedia("photo-permission.asset.photo", false);
        }
        private void OnProgress(ProgressSnapshot snapshot) => Render();
        private void Render()
        {
            if (disposed || route == null) return;
            var c = new PageComposerContext(workspace, route, progress, notice);
            if (route.Page == PageId.Result || route.Page == PageId.Records || route.Page == PageId.RecordDetail) view.Render(ResultsPageComposer.Build(c));
            else if (route.Page == PageId.SecurityCenter || route.Page == PageId.Permissions || route.Page == PageId.PermissionDetail ||
                route.Page == PageId.Verification || route.Page == PageId.Evidence || route.Page == PageId.Conclusion) view.Render(DecisionPageComposer.Build(c));
            else view.Render(SourcePageComposer.Build(c));
        }
        private void OnRead(string sourceId)
        {
            if (disposed || !workspace.CanChange || workspace.State.ViewedSourceIds.Contains(sourceId)) return;
            var source = workspace.Content.Definition.Sources.FirstOrDefault(x => x.Id == sourceId);
            if (source == null || source.Kind == SourceKind.Media || source.Kind == SourceKind.Detection) return;
            Execute(InvestigationCommandKind.ViewSource, sourceId);
        }
        private CommandResult Execute(InvestigationCommandKind kind, string id = null, PermissionDecision permission = null, int install = 0)
        {
            if (disposed || !workspace.CanChange) return null;
            var reply = workspace.Execute(new InvestigationCommand(workspace.State.SessionId, kind, id, permission, (SimulatedInstallState)install));
            if (!disposed && reply.DomainResult?.Status == SessionCommandStatus.InvalidAction)
            { notice = reply.DomainResult.ReasonId == "permission.select-intent-first" ? "请先在权限管理页选择本次用途。" : "暂不能执行，请先查看相关资料并完成前置操作。"; Render(); }
            return reply;
        }
        private void OnAction(PageAction action)
        {
            if (disposed || action == null) return;
            notice = null;
            switch (action.Kind)
            {
                case PageActionKind.Navigate:
                    navigator.Open(new NavigationRequest(action.Page,
                        action.Page == PageId.Records ? null : action.Page == PageId.Phone ? action.ItemId ?? workspace.CaseId : route.CaseId, action.ItemId)); break;
                case PageActionKind.Play: StartMedia(action.ItemId, false); break;
                case PageActionKind.Stop: view.Playback.Stop(); break;
                case PageActionKind.Scan: StartMedia(action.ItemId, true); break;
                case PageActionKind.Collect: Execute(InvestigationCommandKind.CollectEvidence, action.ItemId); break;
                case PageActionKind.Intent: Execute(InvestigationCommandKind.SelectFeatureIntent, action.ItemId); break;
                case PageActionKind.Permission:
                    var decision = action.Access == AccessChoice.Denied ? PermissionDecision.Denied : PermissionDecision.Allow(
                        action.Access == AccessChoice.SelectedPhotos ? PermissionScope.SelectedPhotos : action.Access == AccessChoice.AllPhotos ? PermissionScope.AllPhotos : PermissionScope.Full);
                    Execute(InvestigationCommandKind.SetPermission, action.ItemId, decision); break;
                case PageActionKind.Verify: Execute(InvestigationCommandKind.SelectVerification, action.ItemId); break;
                case PageActionKind.Risk: Execute(InvestigationCommandKind.SelectRisk, action.ItemId); break;
                case PageActionKind.Disposition:
                    var option = workspace.Content?.Definition.DispositionOptions.FirstOrDefault(x => x.Id == action.ItemId);
                    if (option != null && (option.IsImmediateFailure || option.Kind == DispositionKind.InstallUnknownPackage))
                        Confirm(new Confirmation("执行这项模拟操作？", option.Text + "\n操作可能直接结束本次调查。", "确认执行"), () => Execute(InvestigationCommandKind.SelectDisposition, action.ItemId));
                    else Execute(InvestigationCommandKind.SelectDisposition, action.ItemId);
                    break;
                case PageActionKind.Install: Execute(InvestigationCommandKind.SetInstallation, install: action.Value); break;
                case PageActionKind.Submit: Submit(false, workspace.State?.Revision ?? -1); break;
                case PageActionKind.RetrySave: _ = SaveAction(false); break;
                case PageActionKind.NewSave:
                    Confirm(new Confirmation("保留损坏原件后新建？", "将归档损坏文件，使用可验证的内存成绩建立新存档。不兼容的新版本存档不会被覆盖。", "保留并新建"), () => { _ = SaveAction(true); }); break;
            }
        }
        private void Confirm(Confirmation data, Action accepted)
        {
            confirmation?.Dispose();
            confirmation = dialogs.Show(data, yes => { if (yes && !disposed) accepted(); });
        }
        private void Submit(bool confirmed, long revision)
        {
            if (disposed || !workspace.CanChange) return;
            var reply = workspace.Execute(new InvestigationCommand(workspace.State.SessionId, InvestigationCommandKind.Submit,
                expectedRevision: revision, confirmMissingEvidence: confirmed));
            if (disposed || reply.DomainResult == null) return;
            var result = reply.DomainResult;
            if (result.Status == SessionCommandStatus.ConfirmationRequired)
                Confirm(new Confirmation("仍有线索未整理", "尚缺 " + result.MissingKeyEvidenceCount + " 条关键线索。继续提交会按现有调查结算。", "仍然提交", "继续调查"), () => Submit(true, result.Revision));
            else if (result.Status == SessionCommandStatus.MissingRequirements)
            { notice = "提交前还需要：" + string.Join("、", result.MissingRequirements.Select(Requirement)); Render(); }
            else if (result.Status == SessionCommandStatus.StaleRevision)
            { notice = "调查状态已更新，请重新检查并点击提交。"; Render(); }
        }
        private static string Requirement(string id)
        {
            switch (id) { case "background-viewed": return "完整阅读背景资料"; case "any-evidence-collected": return "收集至少一条线索";
                case "media-played": return "完整播放音视频"; case "media-detected": return "完成教学检测"; case "permissions-reviewed": return "逐项判断权限";
                case "verification-selected": return "选择核验方式"; case "risk-selected": return "选择风险判断"; case "disposition-selected": return "选择处置";
                case "feature-intent-selected": return "选择本次用途"; default: return "返回案件检查调查状态"; }
        }
        private void StartMedia(string id, bool detection)
        {
            var media = workspace.Content?.Definition.Media.FirstOrDefault(x => x.Id == id);
            if (media == null)
            {
                if (!detection && workspace.CaseId == "photo-permission" && id == "photo-permission.asset.photo")
                { view.Playback.Stop(); playing=new MediaRequest(workspace.CaseId, id, MediaDisplayKind.Image, Guid.Empty, Guid.NewGuid()); view.Playback.Play(playing); }
                return;
            }
            if (detection && !workspace.CanChange) { notice="预览模式不运行计分检测。"; Render(); return; }
            if (detection) view.Detection.Cancel(); else view.Playback.Stop();
            var request = new MediaRequest(workspace.CaseId, id, (MediaDisplayKind)(int)media.Kind, workspace.State?.SessionId ?? Guid.Empty, Guid.NewGuid());
            if (workspace.CanChange && media.Kind != MediaKind.Image)
            {
                var reply = workspace.Execute(new InvestigationCommand(request.SessionId, detection ? InvestigationCommandKind.BeginDetection : InvestigationCommandKind.BeginPlayback, id, requestId: request.RequestId));
                if (reply.DomainResult?.Status != SessionCommandStatus.Accepted)
                { notice="请先完整播放音视频，再开始教学检测；已有任务时请先停止。"; Render(); return; }
            }
            if (disposed) return;
            if (detection) { scanning=request; view.Detection.Begin(request); }
            else { playing=request; view.Playback.Play(request); }
        }
        private void OnPlayback(PlaybackUpdate update) => OnMedia(update, false);
        private void OnDetection(PlaybackUpdate update) => OnMedia(update, true);
        private void OnMedia(PlaybackUpdate update, bool detection)
        {
            var expected = detection ? scanning : playing;
            if (expected == null || update.Request.RequestId != expected.RequestId || update.Request.SessionId != expected.SessionId) return;
            var terminal = update.Phase == PlaybackPhase.Completed || update.Phase == PlaybackPhase.Failed || update.Phase == PlaybackPhase.Cancelled;
            if (terminal)
            {
                if (detection) scanning=null; else playing=null;
                if (workspace.CanChange && workspace.State.SessionId == expected.SessionId)
                {
                    if (expected.Kind == MediaDisplayKind.Image)
                    {
                        if (update.Phase == PlaybackPhase.Completed)
                            foreach (var m in workspace.Content.Definition.Messages.Where(x => x.MediaId == expected.MediaId)) OnRead(m.SourceId);
                    }
                    else
                    {
                        var cancel = update.Phase == PlaybackPhase.Cancelled;
                        var kind = detection ? (cancel ? InvestigationCommandKind.CancelDetection : InvestigationCommandKind.CompleteDetection) :
                            (cancel ? InvestigationCommandKind.CancelPlayback : InvestigationCommandKind.CompletePlayback);
                        workspace.Execute(new InvestigationCommand(expected.SessionId, kind, expected.MediaId, requestId: expected.RequestId, succeeded: update.Phase == PlaybackPhase.Completed));
                    }
                }
            }
            if (!disposed) { notice=update.Message; Render(); }
        }
        private async Task SaveAction(bool create)
        {
            if (progress == null) return;
            try { if (create) await progress.StartNewSaveAsync(); else await progress.RetryPendingAsync(); }
            catch (Exception) { if (!disposed) notice="保存未完成，请保留当前应用并稍后重试。"; }
            if (!disposed) Render();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed=true;
            confirmation?.Dispose();
            view.Playback.Stop(); view.Detection.Cancel();
            view.ActionRequested -= OnAction; view.SourceRead -= OnRead;
            view.Playback.Changed -= OnPlayback; view.Detection.Changed -= OnDetection;
            workspace.Changed -= Render; if (progress != null) progress.Changed -= OnProgress;
        }
    }
}
