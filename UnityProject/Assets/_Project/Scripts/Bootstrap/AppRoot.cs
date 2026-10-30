// 说明：装配 APK 检查、更新页面和确认弹层，复用现有主题与页面生命周期。
using System;
using UnityEngine;
using Zhijian.Application.Cases;
using Zhijian.Application.Investigation;
using Zhijian.Application.Ports;
using Zhijian.Application.Inspection;
using Zhijian.Infrastructure.Inspection;
using Zhijian.Presentation.Inspection;
using Zhijian.Domain.Outcomes;
using Zhijian.Domain.Results;
using Zhijian.Infrastructure.Content;
using Zhijian.Presentation.Home;
using Zhijian.Presentation.Phone;
using Zhijian.Presentation.Info;
using Zhijian.Presentation.Investigation;
using Zhijian.Presentation.Media;
using Zhijian.Presentation.Navigation;
using Zhijian.UnityUI.Navigation;
using Zhijian.UnityUI.Theme;
using Zhijian.UnityUI.Views;
using Zhijian.UnityUI.Views.Home;
using Zhijian.UnityUI.Views.Phone;
using Zhijian.UnityUI.Views.Info;
using Zhijian.UnityUI.Views.Investigation;
using Zhijian.UnityUI.Dialogs;
using Zhijian.UnityUI.Media;
using Zhijian.UnityUI.Updates;
namespace Zhijian.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour
    {
        [SerializeField] private CaseCatalogAsset catalog;
        [SerializeField] private ThemeAsset themeAsset;
        [SerializeField] private PhoneShellView shellPrefab;
        [SerializeField] private CaseSelectionView homePrefab;
        [SerializeField] private PhoneView phonePrefab;
        [SerializeField] private InfoView infoPrefab;
        [SerializeField] private InvestigationPageView investigationPrefab;
        [SerializeField] private ConfirmationView confirmationPrefab;
        [SerializeField] private Sprite samplePhoto;
        private CaseSelectionService cases;
        private InvestigationService investigation;
        private InvestigationWorkspace workspace;
        private IProgressService progress;
        private AndroidApkInspector apkInspector;
        private IApkReportRepository apkReports;
        private ApkInspectionWorkspace apkWorkspace;
        private PhoneShellView shell;
        private PageRouter router;
        private IPageNavigator navigator;
        private UnityDialogService dialogs;
        private ThemeApplier theme;
        private bool disposed;
        private void Awake()
        {
            try
            {
                var outcomes=new OutcomeResolver(new ICaseOutcomePolicy[] { new VoiceLoanOutcomePolicy(), new FaceMeetingOutcomePolicy(), new PhotoPermissionOutcomePolicy() });
                var repository=new CaseRepository(catalog, outcomes);
                cases=new CaseSelectionService(repository);
                investigation=new InvestigationService(repository, outcomes, error => Debug.LogException(error, this));
                workspace=new InvestigationWorkspace(repository, investigation);
                progress=CreateProgress();
                apkInspector=gameObject.AddComponent<AndroidApkInspector>();
                apkReports=new JsonApkReportRepository(System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Zhijian", "ApkReports"));
                apkWorkspace=new ApkInspectionWorkspace();
                theme=new ThemeApplier(themeAsset);
                shell=Instantiate(shellPrefab, transform, false);
                dialogs=new UnityDialogService(confirmationPrefab, shell.transform, root => theme.Apply(root));
                var factory=new PageFactory(); router=new PageRouter(factory, new ImmediatePageTransition());
                navigator=new CaseAwareNavigator(router, workspace, dialogs);
                factory.Register(PageId.Home, CreateHome); factory.Register(PageId.Phone, CreatePhone);
                foreach (PageId page in Enum.GetValues(typeof(PageId)))
                    if (page != PageId.Home && page != PageId.Phone)
                        factory.Register(page, page == PageId.Settings || page == PageId.Help ? (Func<IPageHandle>)CreateInfo :
                            page == PageId.ApkInspection || page == PageId.ApkReports || page == PageId.ApkReportDetail ? CreateApkInspection :
                            page == PageId.Updates ? CreateUpdates : CreateInvestigation);
                shell.BackRequested += OnBack; shell.HelpRequested += OnHelp;
                shell.PracticeRequested += OnPractice; shell.RecordsRequested += OnRecords;
                theme.Changed += ApplyTheme; router.Changed += OnRouteChanged;
                workspace.ResultProduced += OnResult;
                navigator.Home(); InitializeProgress();
            }
            catch (Exception error)
            { Debug.LogError("智鉴启动失败，请检查 Main 场景中的资源与预制体引用。\n"+error, this); enabled=false; }
        }
        private IProgressService CreateProgress()
        {
            // 存档窗口交付后仅在此装配具体实现，页面一直依赖冻结接口。
            return new Zhijian.Presentation.Progress.ResultPersistenceCoordinator(
                new Zhijian.Application.Progress.ProgressService(new Zhijian.Infrastructure.Persistence.JsonProgressRepository(
                System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Zhijian", "Progress"))));
        }
        private async void InitializeProgress()
        { if (progress != null) try { await progress.InitializeAsync(); } catch (Exception error) { if (!disposed) Debug.LogException(error, this); } }
        private IPageHandle CreateHome()
        {
            var view=Instantiate(homePrefab, shell.PageHost, false);
            var presenter=new CaseSelectionPresenter(view, cases, navigator);
            return new PageHandle(view.gameObject, presenter, _ => presenter.Bind());
        }
        private IPageHandle CreatePhone()
        {
            var view=Instantiate(phonePrefab, shell.PageHost, false);
            var presenter=new PhonePresenter(view, cases, navigator);
            return new PageHandle(view.gameObject, presenter, route => presenter.Bind(route.CaseId));
        }
        private IPageHandle CreateInfo()
        {
            var view=Instantiate(infoPrefab, shell.PageHost, false);
            var presenter=new InfoPresenter(view, navigator, theme, cases);
            return new PageHandle(view.gameObject, presenter, presenter.Bind);
        }
        private IPageHandle CreateInvestigation()
        {
            var view=Instantiate(investigationPrefab, shell.PageHost, false);
            view.Initialize(ResolveMedia); view.ContentChanged += ApplyViewTheme;
            var presenter=new InvestigationPagePresenter(view, workspace, navigator, dialogs, progress);
            return new PageHandle(view.gameObject, presenter, presenter.Bind, view.SetPresentationReady);
        }
        private UnityMediaAsset ResolveMedia(MediaRequest request)
        {
            if (request.CaseId == "photo-permission" && request.MediaId == "photo-permission.asset.photo" && samplePhoto != null)
                return new UnityMediaAsset(null, null, samplePhoto);
            if (catalog.TryGetAsset(request.CaseId, out var asset) && asset.TryGetMedia(request.MediaId, out var audio, out var video, out var image))
                return new UnityMediaAsset(audio, video, image);
            return null;
        }
        private IPageHandle CreateApkInspection()
        {
            var view=Instantiate(investigationPrefab, shell.PageHost, false);
            view.Initialize(ResolveMedia); view.ContentChanged += ApplyViewTheme;
            var presenter=new ApkInspectionPresenter(view, navigator, apkInspector, apkReports, apkWorkspace, dialogs);
            return new PageHandle(view.gameObject, presenter, presenter.Bind, view.SetPresentationReady);
        }
        private IPageHandle CreateUpdates()
        {
            var view=Instantiate(investigationPrefab, shell.PageHost, false);
            view.Initialize(ResolveMedia); view.ContentChanged += ApplyViewTheme;
            var presenter=view.gameObject.AddComponent<UpdatePage>(); presenter.Initialize(view);
            return new PageHandle(view.gameObject, presenter, presenter.Bind, view.SetPresentationReady);
        }
        private async void OnResult(CaseResult result)
        {
            navigator.Open(new NavigationRequest(PageId.Result, result.CaseId, result.ResultId.ToString()));
            if (progress != null) try { await progress.RecordAsync(result); } catch (Exception error) { if (!disposed) Debug.LogException(error, this); }
        }
        private void OnRouteChanged(NavigationRequest route, bool canGoBack) { shell.RenderRoute(route, canGoBack); ApplyTheme(); }
        private void ApplyViewTheme(GameObject root) { if (!disposed && root != null) theme.Apply(root); }
        private void ApplyTheme() { if (shell != null) theme.Apply(shell.gameObject); }
        private void OnBack() => navigator.Back();
        private void OnHelp() => navigator.Open(new NavigationRequest(PageId.Help, router.Current?.CaseId));
        private void OnPractice() => navigator.Home();
        private void OnRecords() => navigator.Open(new NavigationRequest(PageId.Records));
        private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) navigator?.Back(); }
        private void OnDestroy()
        {
            disposed=true;
            if (shell != null) { shell.BackRequested -= OnBack; shell.HelpRequested -= OnHelp; shell.PracticeRequested -= OnPractice; shell.RecordsRequested -= OnRecords; }
            if (theme != null) theme.Changed -= ApplyTheme;
            if (workspace != null) workspace.ResultProduced -= OnResult;
            dialogs?.Dispose(); router?.Dispose(); apkInspector?.Dispose(); workspace?.Dispose(); investigation?.Dispose(); progress?.Dispose(); theme?.Dispose();
        }
    }
}
