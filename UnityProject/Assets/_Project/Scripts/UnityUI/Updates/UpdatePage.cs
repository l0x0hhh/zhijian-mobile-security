// 说明：用户主动检查公开 GitHub Release，只显示可信仓库的版本并通过浏览器打开发布页。
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Zhijian.Presentation.Investigation;
using Zhijian.Presentation.Navigation;
using Zhijian.UnityUI.Views.Investigation;

namespace Zhijian.UnityUI.Updates
{
    public sealed class UpdatePage : MonoBehaviour, IDisposable
    {
        private const string Api = "https://api.github.com/repos/l0x0hhh/zhijian-mobile-security/releases/latest";
        private const string ReleasePage = "https://github.com/l0x0hhh/zhijian-mobile-security/releases/latest";
        [Serializable] private sealed class Release { public string tag_name; }
        private InvestigationPageView view;
        private Coroutine request;
        private UnityWebRequest active;
        private bool disposed, hasUpdate;
        private string status = "点击检查，获取公开发布的最新版本。";
        public void Initialize(InvestigationPageView value) { view = value; }
        public void Bind(NavigationRequest route) { view.ActionRequested += OnAction; Render(); }
        private void OnAction(PageAction action)
        {
            if (disposed || action == null) return;
            if (action.Kind == PageActionKind.CheckUpdates && request == null)
            { hasUpdate = false; status = "正在检查更新…"; request = StartCoroutine(Check()); Render(); }
            else if (action.Kind == PageActionKind.OpenUpdateRelease && hasUpdate) UnityEngine.Application.OpenURL(ReleasePage);
        }
        private IEnumerator Check()
        {
            using (var response = UnityWebRequest.Get(Api))
            {
                active = response; response.timeout = 10;
                response.SetRequestHeader("Accept", "application/vnd.github+json");
                yield return response.SendWebRequest();
                if (disposed) yield break;
                if (response.responseCode == 404) status = "暂无公开版本，或仓库尚未公开。";
                else if (response.result != UnityWebRequest.Result.Success) status = "暂时无法检查更新，请检查网络后重试。";
                else
                {
                    Release release = null;
                    try { release = JsonUtility.FromJson<Release>(response.downloadHandler.text); } catch (Exception) { }
                    if (release == null || !TryVersion(release.tag_name, out var latest) || !TryVersion(UnityEngine.Application.version, out var current))
                        status = "无法比较版本，请检查发布标签和当前版本格式。";
                    else
                    {
                        hasUpdate = latest.CompareTo(current) > 0;
                        status = hasUpdate ? "发现新版本：" + release.tag_name + "。" : "当前版本已是最新公开版本（" + release.tag_name + "）。";
                    }
                }
                active = null; request = null; Render();
            }
        }
        private static bool TryVersion(string value, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(value) || value.Length > 40) return false;
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            if (!Version.TryParse(value, out var parsed)) return false;
            version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
            return true;
        }
        private void Render()
        {
            var rows = new List<InvestigationRow> {
                new InvestigationRow("当前版本", UnityEngine.Application.version, key: "current"),
                new InvestigationRow(request == null ? "检查更新" : "检查中…", "仅在点击时联网查询公开 Release。",
                    new PageAction(PageActionKind.CheckUpdates), request == null, key: "check")
            };
            if (hasUpdate) rows.Add(new InvestigationRow("打开发布页面", "在浏览器查看更新说明和下载方式。",
                new PageAction(PageActionKind.OpenUpdateRelease), key: "release"));
            view.Render(new InvestigationPageData("updates", "检查更新", "查看公开发布的版本。", status,
                "当前安装包不会自动下载或安装更新。仓库公开并发布正式 Release 后可进行版本比较。", rows));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (view != null) view.ActionRequested -= OnAction;
            if (active != null) active.Abort();
            if (request != null) StopCoroutine(request);
            active?.Dispose(); active = null; request = null;
        }
        private void OnDestroy() => Dispose();
    }
}
