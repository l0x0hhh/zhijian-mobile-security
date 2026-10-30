// 说明：验证检查摘要、分区展开、未保存提醒、用途重置、报告重试及原有取消保存边界。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zhijian.Application.Inspection;
using Zhijian.Presentation.Inspection;
using Zhijian.Presentation.Investigation;
using Zhijian.Presentation.Media;
using Zhijian.Presentation.Navigation;

internal static class ApkInspectionChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
    private static ApkPackageFacts Facts(string[] permissions) => new ApkPackageFacts("test.apk", "测试应用", "test.package", "1", 1, 22, 33,
        2048, new string('a', 64), permissions, Array.Empty<string>());
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
    public static void Main()
    {
        var requested = new[] { "android.permission.READ_SMS", "android.permission.CAMERA", "custom.UNKNOWN", "android.permission.CAMERA" };
        var facts = Facts(requested);
        requested[0] = "tampered";
        Check(facts.Permissions.Count == 3 && facts.Permissions.Contains("android.permission.READ_SMS"), "Facts must clone and deduplicate permissions.");
        var report = ApkInspectionService.Analyze(facts, ApkPurpose.PhotoEditing);
        Check(report.Findings.Single(x => x.Permission.EndsWith("READ_SMS")).NeedsReview, "Photo use must flag SMS for review.");
        Check(!report.Findings.Single(x => x.Permission.EndsWith("CAMERA")).NeedsReview, "Camera can be related to photo use.");
        Check(report.Findings.Any(x => x.Permission == "custom.UNKNOWN" && x.Explanation.Contains("未覆盖")), "Unknown permission must remain visible.");
        Check(ApkInspectionService.Analyze(Facts(Array.Empty<string>()), ApkPurpose.Other).Findings.Count == 0, "No declared permissions must remain zero, not invented.");
        Throws<FormatException>(() => new ApkPackageFacts("a", "a", "a", "1", 1, 22, 33, 10, "invalid", null, null));
        Throws<FormatException>(() => new ApkPackageFacts("a", "a", "a", "1", 1, 22, 33, 268435457, new string('a', 64), null, null));
        Throws<FormatException>(() => new ApkInspectionReport(Guid.NewGuid(), DateTime.UtcNow, "1", ApkPurpose.Other, facts, new[] { report.Findings[0], report.Findings[0], report.Findings[0] }));
        var workspace = new ApkInspectionWorkspace(); workspace.SetFacts(facts);
        var prior = workspace.Report; workspace.SavedId = prior.Id;
        workspace.SetPurpose(ApkPurpose.PhotoEditing);
        Check(workspace.Report.Id != prior.Id && workspace.SavedId != workspace.Report.Id && prior.Purpose == ApkPurpose.Unspecified,
            "Changing purpose must preserve the old report and require saving the new one.");
        var view = new View(); var inspector = new Inspector(); var repository = new Repository();
        var presenter = new ApkInspectionPresenter(view, new Navigator(), inspector, repository, workspace, new Dialogs());
        presenter.Bind(new NavigationRequest(PageId.ApkInspection));
        repository.Fail = true; view.Fire(new PageAction(PageActionKind.SaveApkReport));
        Check(view.Last.Status.Contains("保存失败") && workspace.SavedId != workspace.Report.Id, "Failed save must not claim success.");
        repository.Fail = false; view.Fire(new PageAction(PageActionKind.SaveApkReport));
        Check(repository.Saved == workspace.Report && workspace.SavedId == workspace.Report.Id, "Successful save must refer to exact report.");
        view.Fire(new PageAction(PageActionKind.InspectApk)); view.Fire(new PageAction(PageActionKind.InspectApk));
        Check(inspector.Calls == 1, "Repeated click must not start a second selection.");
        var retained = workspace.Report;
        presenter.Dispose();
        Check(inspector.Token.IsCancellationRequested, "Leaving must cancel the pending native request.");
        inspector.Completion.SetResult(Facts(new[] { "android.permission.READ_CONTACTS" }));
        Check(workspace.Report == retained, "Late completion must not replace the report after leaving.");
        var cancelledView = new View(); var cancelledInspector = new Inspector();
        var cancelled = new ApkInspectionPresenter(cancelledView, new Navigator(), cancelledInspector, repository, workspace, new Dialogs());
        cancelled.Bind(new NavigationRequest(PageId.ApkInspection)); cancelledView.Fire(new PageAction(PageActionKind.InspectApk));
        cancelledInspector.Completion.SetCanceled();
        Check(cancelledView.Last.Status.Contains("已取消") && workspace.Report == retained, "Cancelled picker must preserve current report.");
        cancelled.Dispose();
        var successfulView = new View(); var successfulInspector = new Inspector();
        var successful = new ApkInspectionPresenter(successfulView, new Navigator(), successfulInspector, repository, workspace, new Dialogs());
        successful.Bind(new NavigationRequest(PageId.ApkInspection)); successfulView.Fire(new PageAction(PageActionKind.InspectApk));
        successfulInspector.Completion.SetResult(Facts(new[] { "android.permission.READ_SMS" }));
        Check(workspace.Report.Package.Permissions.Count == 1 && workspace.Report.Id != retained.Id, "Completed selection must use returned facts.");
        Check(workspace.Purpose == ApkPurpose.Unspecified, "A new APK must not inherit the previous app's purpose.");
        successful.Dispose();
        var emptyView = new View();
        using (var empty = new ApkInspectionPresenter(emptyView, new Navigator(), new Inspector(), repository, new ApkInspectionWorkspace(), new Dialogs()))
        {
            empty.Bind(new NavigationRequest(PageId.ApkInspection));
            Check(!emptyView.Last.Rows.Any(x => x.Action?.Kind == PageActionKind.SelectApkPurpose), "First use must ask for a file before presenting purpose choices.");
        }
        workspace.SetFacts(Facts(new[] { "android.permission.READ_SMS", "android.permission.CAMERA", "custom.UNKNOWN" }));
        var overview = new View();
        using (var overviewPresenter = new ApkInspectionPresenter(overview, new Navigator(), new Inspector(), repository, workspace, new Dialogs()))
        {
            overviewPresenter.Bind(new NavigationRequest(PageId.ApkInspection));
            Check(overview.Last.Rows[0].Key == "summary" && !overview.Last.Rows.Any(x => x.Key == "hash"), "Summary must precede technical identifiers.");
            Check(overview.Last.Rows.ToList().FindIndex(x => x.Key == "save") < overview.Last.Rows.ToList().FindIndex(x => x.Key.StartsWith("permission:")), "Save must remain above long permission lists.");
            var pageKey = overview.Last.Key;
            overview.Fire(new PageAction(PageActionKind.ToggleApkSection, "technical"));
            Check(overview.Last.Key == pageKey && overview.Last.Rows.Any(x => x.Key == "hash"), "Expanding details must retain content identity for scrolling.");
            overview.Fire(new PageAction(PageActionKind.ToggleApkSection, "other"));
            Check(overview.Last.Rows.Any(x => x.Key == "permission:custom.UNKNOWN"), "Uncovered permissions must remain accessible in expanded declarations.");
            overview.Fire(new PageAction(PageActionKind.SelectApkPurpose, value: (int)ApkPurpose.PhotoEditing));
            Check(!overview.Last.Rows.Any(x => x.Action?.Kind == PageActionKind.SelectApkPurpose) && overview.Last.Status.Contains("尚未保存"), "Purpose selection must close choices and accurately mark the new report unsaved.");
        }
        var draft = workspace.Report;
        var confirmView = new View(); var confirmInspector = new Inspector(); var dialogs = new Dialogs { AutoApprove = false };
        using (var guarded = new ApkInspectionPresenter(confirmView, new Navigator(), confirmInspector, repository, workspace, dialogs))
        {
            guarded.Bind(new NavigationRequest(PageId.ApkInspection));
            confirmView.Fire(new PageAction(PageActionKind.InspectApk)); confirmView.Fire(new PageAction(PageActionKind.InspectApk));
            Check(dialogs.Shown == 1 && confirmInspector.Calls == 0, "Unsaved report must require one confirmation before opening a picker.");
            dialogs.Callback(false);
            Check(confirmInspector.Calls == 0 && workspace.Report == draft, "Returning to save must preserve the report.");
            confirmView.Fire(new PageAction(PageActionKind.InspectApk)); dialogs.Callback(true); dialogs.Callback(true);
            Check(confirmInspector.Calls == 1, "A duplicated modal completion must start only one request.");
            confirmInspector.Completion.SetCanceled();
            Check(confirmView.Last.Status.Contains("仍保留") && workspace.Report == draft, "Cancelled replacement must explain that the prior result remains.");
        }
        var disposedView = new View(); var disposedInspector = new Inspector(); var disposedDialogs = new Dialogs { AutoApprove = false };
        var disposedPresenter = new ApkInspectionPresenter(disposedView, new Navigator(), disposedInspector, repository, workspace, disposedDialogs);
        disposedPresenter.Bind(new NavigationRequest(PageId.ApkInspection)); disposedView.Fire(new PageAction(PageActionKind.InspectApk));
        disposedPresenter.Dispose(); disposedDialogs.Callback(true);
        Check(disposedDialogs.LeaseDisposed && disposedInspector.Calls == 0, "Leaving while a confirmation is open must dispose it and ignore delayed answers.");
        var historyView = new View(); var historyRepository = new Repository { FailLoad = true };
        using (var historyPresenter = new ApkInspectionPresenter(historyView, new Navigator(), new Inspector(), historyRepository, workspace, new Dialogs()))
        {
            historyPresenter.Bind(new NavigationRequest(PageId.ApkReports));
            Check(historyView.Last.Rows.Any(x => x.Action?.Kind == PageActionKind.ReloadApkReports), "History load failure must expose retry.");
            historyRepository.FailLoad = false; historyView.Fire(new PageAction(PageActionKind.ReloadApkReports));
            Check(historyView.Last.Status == null && historyView.Last.Rows.Any(x => x.Title == "暂无已保存报告"), "Successful retry must clear the error.");
        }
        var external = new ApkInspectionWorkspace();
        external.SetFacts(new ApkPackageFacts("test.apk", "<b>label</b>\u202e\nspoof", "test.package", "1", 1, 22, 33, 10,
            new string('b', 64), Array.Empty<string>(), Array.Empty<string>()));
        var externalView = new View();
        using (var plain = new ApkInspectionPresenter(externalView, new Navigator(), new Inspector(), repository, external, new Dialogs()))
        {
            plain.Bind(new NavigationRequest(PageId.ApkInspection));
            Check(!externalView.Last.Lead.Contains("<") && !externalView.Last.Lead.Contains("\u202e") && !externalView.Last.Lead.Contains("\n"), "External names must not inject markup, direction changes or lines.");
            Check(externalView.Last.Rows[0].Body.Contains("不代表安全"), "No priority findings must not be presented as a safety guarantee.");
        }
        Console.WriteLine("PASS " + checks + " checks; Android and file persistence runtime not exercised.");
    }
    private sealed class View : IInvestigationPageView
    {
        public event Action<PageAction> ActionRequested;
        public event Action<string> SourceRead { add { } remove { } }
        public IMediaPlayback Playback => null;
        public ITeachingDetection Detection => null;
        public InvestigationPageData Last;
        public void Render(InvestigationPageData data) { Last = data; }
        public void Fire(PageAction action) => ActionRequested?.Invoke(action);
    }
    private sealed class Navigator : IPageNavigator
    { public void Open(NavigationRequest request) { } public void Home() { } public bool Back() => false; }
    private sealed class Inspector : IApkInspector
    {
        public bool IsSupported => true;
        public int Calls; public CancellationToken Token;
        public TaskCompletionSource<ApkPackageFacts> Completion = new TaskCompletionSource<ApkPackageFacts>();
        public Task<ApkPackageFacts> SelectAndInspectAsync(CancellationToken token) { Calls++; Token = token; return Completion.Task; }
    }
    private sealed class Repository : IApkReportRepository
    {
        public bool Fail, FailLoad; public ApkInspectionReport Saved;
        public void Save(ApkInspectionReport report) { if (Fail) throw new System.IO.IOException(); Saved = report; }
        public ApkReportHistory LoadRecent()
        { if (FailLoad) throw new System.IO.IOException(); return new ApkReportHistory(Saved == null ? Array.Empty<ApkInspectionReport>() : new[] { Saved }); }
    }
    private sealed class Dialogs : IConfirmationService
    {
        public bool AutoApprove = true, LeaseDisposed;
        public int Shown;
        public Action<bool> Callback;
        public IDisposable Show(Confirmation data, Action<bool> completed)
        { Shown++; Callback = completed; if (AutoApprove) completed(true); return new Lease(this); }
        public bool CloseTop() { Callback?.Invoke(false); return true; }
        private sealed class Lease : IDisposable
        {
            private readonly Dialogs owner;
            public Lease(Dialogs owner) { this.owner = owner; }
            public void Dispose() { owner.LeaseDisposed = true; }
        }
    }
}
