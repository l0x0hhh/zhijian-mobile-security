// 说明：先呈现检查摘要与重点权限，技术信息按需展开；更换未保存报告前提醒，保持取消与回调隔离。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Zhijian.Application.Inspection;
using Zhijian.Presentation.Investigation;
using Zhijian.Presentation.Navigation;

namespace Zhijian.Presentation.Inspection
{
    public sealed class ApkInspectionWorkspace
    {
        public ApkInspectionReport Report { get; private set; }
        public ApkPurpose Purpose { get; private set; }
        public Guid SavedId { get; set; }
        public void SetFacts(ApkPackageFacts facts)
        {
            var report = ApkInspectionService.Analyze(facts, ApkPurpose.Unspecified);
            Purpose = ApkPurpose.Unspecified; Report = report;
        }
        public void SetPurpose(ApkPurpose purpose)
        {
            if (!Enum.IsDefined(typeof(ApkPurpose), purpose)) return;
            if (purpose == Purpose) return;
            Purpose = purpose;
            if (Report != null) Report = ApkInspectionService.Analyze(Report.Package, purpose);
        }
    }
    public sealed class ApkInspectionPresenter : IDisposable
    {
        private readonly IInvestigationPageView view;
        private readonly IPageNavigator navigator;
        private readonly IApkInspector inspector;
        private readonly IApkReportRepository repository;
        private readonly ApkInspectionWorkspace workspace;
        private readonly IConfirmationService dialogs;
        private readonly HashSet<string> expanded = new HashSet<string>(StringComparer.Ordinal);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private IDisposable confirmation;
        private NavigationRequest route;
        private ApkReportHistory history;
        private bool disposed, busy, bound, confirming;
        private string notice;
        public ApkInspectionPresenter(IInvestigationPageView view, IPageNavigator navigator, IApkInspector inspector,
            IApkReportRepository repository, ApkInspectionWorkspace workspace, IConfirmationService dialogs)
        { this.view = view; this.navigator = navigator; this.inspector = inspector; this.repository = repository; this.workspace = workspace;
            this.dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs)); }
        public void Bind(NavigationRequest request)
        {
            if (bound) throw new InvalidOperationException("页面已绑定。");
            bound = true; route = request; view.ActionRequested += OnAction;
            if (route.Page == PageId.ApkInspection && workspace.Report?.Purpose == ApkPurpose.Unspecified) expanded.Add("purpose");
            if (route.Page != PageId.ApkInspection)
            {
                LoadHistory();
            }
            Render();
        }
        private void OnAction(PageAction action)
        {
            if (disposed || action == null || busy || confirming) return;
            if (action.Kind == PageActionKind.Navigate)
            { navigator.Open(new NavigationRequest(action.Page, itemId: action.ItemId)); return; }
            if (action.Kind == PageActionKind.ToggleApkSection)
            {
                if (action.ItemId != "purpose" && action.ItemId != "other" && action.ItemId != "technical" &&
                    action.ItemId != "scope" && action.ItemId != "more-review") return;
                if (!expanded.Remove(action.ItemId)) expanded.Add(action.ItemId);
                Render(); return;
            }
            if (action.Kind == PageActionKind.ReloadApkReports && route.Page != PageId.ApkInspection)
            { LoadHistory(); Render(); return; }
            if (route.Page != PageId.ApkInspection) return;
            if (action.Kind == PageActionKind.InspectApk) RequestSelection();
            else if (action.Kind == PageActionKind.SelectApkPurpose)
            { workspace.SetPurpose((ApkPurpose)action.Value); expanded.Remove("purpose"); notice = null; Render(); }
            else if (action.Kind == PageActionKind.SaveApkReport && workspace.Report != null)
            {
                try { repository.Save(workspace.Report); workspace.SavedId = workspace.Report.Id; notice = "报告已保存，可在“检查报告”中回看。"; }
                catch (Exception) { notice = "保存失败，当前结果仍在本次运行内，可再次点击保存；尚不能保证重启后保留。"; }
                Render();
            }
        }
        private void LoadHistory()
        {
            try { history = repository.LoadRecent(); notice = null; }
            catch (Exception) { history = null; notice = "报告暂时无法读取，请检查存储状态后重试。"; }
        }
        private void RequestSelection()
        {
            if (workspace.Report == null || workspace.SavedId == workspace.Report.Id) { SelectApk(); return; }
            confirming = true;
            var completed = false;
            var lease = dialogs.Show(new Confirmation("更换检查文件？",
                "当前报告尚未保存。新文件检查成功后会替换当前结果；取消选择或检查失败会保留它。要先保存，请返回点击“保存报告”。",
                "继续选择", "返回保存"), yes =>
            {
                if (completed || disposed) return;
                completed = true; confirming = false; confirmation = null;
                if (yes) SelectApk();
            });
            if (completed || disposed) lease?.Dispose(); else confirmation = lease;
        }
        private async void SelectApk()
        {
            if (busy || disposed) return;
            busy = true; notice = "请选择一个 APK，随后在本机读取；不需要安装该应用。"; Render();
            try
            {
                var facts = await inspector.SelectAndInspectAsync(lifetime.Token);
                if (disposed) return;
                workspace.SetFacts(facts); expanded.Clear(); expanded.Add("purpose");
                notice = "读取完成。选择用途后核实重点项，再保存报告。";
            }
            catch (OperationCanceledException) { if (!disposed) notice = "已取消，未产生新结果。" + RetainedMessage(); }
            catch (Exception error) { if (!disposed) notice = "检查失败：" + Plain(error.Message) + RetainedMessage(); }
            finally { busy = false; if (!disposed) Render(); }
        }
        private string RetainedMessage() => workspace.Report == null ? string.Empty : " 上次检查结果仍保留。";
        private void Render()
        {
            if (disposed || route == null) return;
            var rows = new List<InvestigationRow>();
            string title, lead, body;
            if (route.Page == PageId.ApkInspection)
            {
                title = workspace.Report == null ? "检查陌生 APK" : "检查结果";
                lead = workspace.Report == null ? "选择安装包，查看它声明了哪些权限。" : Plain(workspace.Report.Package.Label);
                body = workspace.Report == null ? "选择收到或下载的安装包（.apk）。\n仅在本机读取，不安装所选应用；最大 256 MiB。" : null;
                if (!inspector.IsSupported) body = "请在 Android 设备上选择 APK；当前平台可查看已保存报告。";
                if (workspace.Report != null)
                    AddReport(rows, workspace.Report, true);
                else
                {
                    rows.Add(SelectFileRow());
                    rows.Add(Link("查看已保存报告", PageId.ApkReports));
                    AddScope(rows);
                }
            }
            else if (route.Page == PageId.ApkReports)
            {
                title = "检查报告"; lead = "最近 20 份本地报告，与练习成绩分别保存。";
                body = history?.Warning;
                rows.Add(Link("检查另一个 APK", PageId.ApkInspection));
                if (history != null)
                    foreach (var report in history.Reports)
                        rows.Add(new InvestigationRow(Plain(report.Package.Label), Plain(report.Package.PackageName) + "\n" +
                            report.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " · " +
                            report.Findings.Count(x => x.NeedsReview) + " 项需重点核实",
                            new PageAction(PageActionKind.Navigate, report.Id.ToString("D"), PageId.ApkReportDetail), key: report.Id.ToString("D")));
                if (history != null && history.Reports.Count == 0) rows.Add(new InvestigationRow("暂无已保存报告", "完成检查后点击保存。"));
                if (history == null) rows.Add(RetryHistoryRow());
            }
            else
            {
                title = "报告详情"; lead = "检查时保存的事实与解释，不随规则更新自动改写。"; body = history?.Warning;
                var report = history?.Reports.FirstOrDefault(x => x.Id.ToString("D") == route.ItemId);
                if (report == null)
                { rows.Add(new InvestigationRow("报告不可用", "文件可能无法读取，或不在最近报告范围内。")); rows.Add(RetryHistoryRow()); }
                else AddReport(rows, report, false);
                rows.Add(Link("返回检查报告", PageId.ApkReports));
            }
            var status = notice;
            if (status == null && route.Page == PageId.ApkInspection && workspace.Report != null)
                status = workspace.SavedId == workspace.Report.Id ? "报告已保存" : "当前报告尚未保存";
            var contentIdentity = route.Page == PageId.ApkInspection ? workspace.Report?.Id.ToString("N") ?? "empty" : route.ItemId;
            view.Render(new InvestigationPageData("apk:" + route.Page + ":" + contentIdentity, title, lead, status, body, rows));
        }
        private void AddReport(List<InvestigationRow> rows, ApkInspectionReport report, bool editable)
        {
            var f = report.Package;
            var review = report.Findings.Where(x => x.NeedsReview).ToArray();
            rows.Add(new InvestigationRow(review.Length == 0 ? "当前规则未列出重点核实项" : "优先核实 " + review.Length + " 项权限",
                "共声明 " + f.Permissions.Count + " 项权限。" + (report.Purpose == ApkPurpose.Unspecified ? " 尚未确定用途，当前为通用提示。" :
                " 当前用途：" + PurposeLabel(report.Purpose) + "。") + "\n" +
                (review.Length > 0 ? "建议先核实这些权限的用途，再决定是否安装。" : "请核实下载渠道与开发者说明。") + " 未提示不代表安全。", key: "summary"));
            rows.Add(new InvestigationRow("所选文件", Plain(f.FileName) + "\n" + Plain(f.PackageName) + " · " +
                (f.FileBytes / 1048576d).ToString("0.00", CultureInfo.InvariantCulture) + " MiB", key: "facts"));
            if (editable)
            {
                rows.Add(Toggle(expanded.Contains("purpose") ? "这个应用准备用来做什么？" : "更改用途 · " + PurposeLabel(report.Purpose), "purpose",
                    "用途会影响需要重点核实的事项。"));
                if (expanded.Contains("purpose"))
                    foreach (var purpose in new[] { ApkPurpose.PhotoEditing, ApkPurpose.Communication, ApkPurpose.Other, ApkPurpose.Unspecified })
                        rows.Add(new InvestigationRow(PurposeLabel(purpose), action: new PageAction(PageActionKind.SelectApkPurpose, value: (int)purpose),
                            enabled: !busy, tone: report.Purpose == purpose ? RowTone.Selected : RowTone.Plain, key: "purpose:" + purpose));
                rows.Add(new InvestigationRow(workspace.SavedId == report.Id ? "本报告已保存" : "保存报告",
                    "保存后可从首页“检查报告”回看。", new PageAction(PageActionKind.SaveApkReport), !busy && workspace.SavedId != report.Id, key: "save"));
            }
            foreach (var finding in review.Take(expanded.Contains("more-review") ? review.Length : 5)) AddFinding(rows, finding);
            if (review.Length > 5) rows.Add(Toggle(expanded.Contains("more-review") ? "收起部分重点项" : "查看其余 " + (review.Length - 5) + " 项重点权限", "more-review"));
            var others = report.Findings.Where(x => !x.NeedsReview).ToArray();
            if (others.Length > 0)
            {
                rows.Add(Toggle((expanded.Contains("other") ? "收起" : "查看") + "其余 " + others.Length + " 项声明", "other",
                    "包含可能相关或规则未覆盖的项目，不表示无需核实。"));
                if (expanded.Contains("other")) foreach (var finding in others) AddFinding(rows, finding);
            }
            if (editable) { rows.Add(SelectFileRow()); rows.Add(Link("查看已保存报告", PageId.ApkReports)); }
            rows.Add(Toggle((expanded.Contains("technical") ? "收起" : "查看") + "技术信息", "technical", "版本、API、文件摘要和签名证书指纹。"));
            if (expanded.Contains("technical"))
            {
                rows.Add(new InvestigationRow("版本与记录", Plain(f.VersionName) + "（" + f.VersionCode + "）\n最低 API：" +
                    (f.MinimumSdk == 0 ? "未提供" : f.MinimumSdk.ToString()) + "；目标 API：" + f.TargetSdk +
                    "\n检查时间：" + report.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n规则：" + Plain(report.RuleVersion), key: "version"));
                rows.Add(new InvestigationRow("文件 SHA-256", f.Sha256 + "\n用于比对文件，不能证明来源可靠。", key: "hash"));
                rows.Add(new InvestigationRow("签名证书指纹", (f.SignerSha256.Count == 0 ? "未取得证书信息。" : string.Join("\n", f.SignerSha256)) +
                    "\n未进行安装级签名完整性验证，也未核验开发者身份。", key: "signers"));
            }
            AddScope(rows);
        }
        private InvestigationRow SelectFileRow() => new InvestigationRow(busy ? "正在选择或检查…" : workspace.Report == null ? "选择安装包（.apk）" : "检查另一个 APK",
            action: new PageAction(PageActionKind.InspectApk), enabled: !busy && inspector.IsSupported, key: "pick");
        private static InvestigationRow RetryHistoryRow() => new InvestigationRow("重新读取报告", action: new PageAction(PageActionKind.ReloadApkReports), key: "reload");
        private InvestigationRow Toggle(string label, string section, string body = null) => new InvestigationRow(label, body,
            new PageAction(PageActionKind.ToggleApkSection, section), !busy, key: "toggle:" + section);
        private static void AddFinding(List<InvestigationRow> rows, ApkFinding finding) => rows.Add(new InvestigationRow(
            (finding.NeedsReview ? "需核实 · " : string.Empty) + Plain(finding.Title), Plain(finding.Permission) + "\n" + Plain(finding.Explanation),
            tone: finding.NeedsReview ? RowTone.Soft : RowTone.Plain, key: "permission:" + finding.Permission));
        private void AddScope(List<InvestigationRow> rows)
        {
            rows.Add(Toggle((expanded.Contains("scope") ? "收起" : "了解") + "检查范围", "scope"));
            if (expanded.Contains("scope")) rows.Add(new InvestigationRow("本次能确认什么", "读取文件中的权限声明与基本信息，不能确认已获授权、实际数据访问行为或恶意代码。\n文件仅临时复制到本机缓存；正常结束后清理，强制结束可能留下缓存副本。支持单个 APK，不支持 APKS / XAPK 分包集合。", key: "scope"));
        }
        private static InvestigationRow Link(string label, PageId page) => new InvestigationRow(label,
            action: new PageAction(PageActionKind.Navigate, page: page), key: "link:" + page);
        private static string PurposeLabel(ApkPurpose purpose) => purpose == ApkPurpose.PhotoEditing ? "制作或编辑照片" :
            purpose == ApkPurpose.Communication ? "聊天与通话" : purpose == ApkPurpose.Other ? "其他用途" : "尚未确定用途";
        private static string Plain(string value) => string.IsNullOrEmpty(value) ? "（未提供）" :
            new string(value.Take(2500).Where(c => !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format).ToArray()).Replace('<', '＜').Replace('>', '＞');
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (bound) view.ActionRequested -= OnAction;
            confirmation?.Dispose(); confirmation = null;
            lifetime.Cancel(); lifetime.Dispose();
        }
    }
}
