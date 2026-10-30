// 说明：首页提供 APK 检查、报告与更新入口，训练案例仍来自原有目录。
using System;
using System.Collections.Generic;
using Zhijian.Application.Cases;
using Zhijian.Domain.Cases;
using Zhijian.Presentation.Navigation;

namespace Zhijian.Presentation.Home
{
    public sealed class CaseSelectionPresenter : IDisposable
    {
        private readonly ICaseSelectionView view;
        private readonly CaseSelectionService service;
        private readonly IPageNavigator navigator;
        private bool bound;
        public CaseSelectionPresenter(ICaseSelectionView view, CaseSelectionService service, IPageNavigator navigator)
        { this.view = view; this.service = service; this.navigator = navigator; }

        public void Bind()
        {
            if (bound) return;
            bound = true;
            view.CaseSelected += OnSelected;
            var rows = new List<CaseRowData>();
            rows.Add(new CaseRowData("tool:apk", "检查陌生 APK", "选择安装包，查看真实权限声明与核实建议。", "本地工具"));
            rows.Add(new CaseRowData("tool:apk-reports", "检查报告", "回看已保存的 APK 检查，与练习成绩分别记录。", "本地记录"));
            rows.Add(new CaseRowData("tool:updates", "检查更新", "查看是否有公开的新版本。", "版本信息"));
            foreach (var entry in service.Cases)
                rows.Add(new CaseRowData(entry.Id, entry.Title, entry.Summary,
                    entry.Availability == CaseAvailability.Preview ? "可预览" : "正式练习"));
            view.Render(new CaseSelectionViewData(rows.AsReadOnly()));
        }

        private void OnSelected(string id)
        {
            if (id == "tool:apk") { navigator.Open(new NavigationRequest(PageId.ApkInspection)); return; }
            if (id == "tool:apk-reports") { navigator.Open(new NavigationRequest(PageId.ApkReports)); return; }
            if (id == "tool:updates") { navigator.Open(new NavigationRequest(PageId.Updates)); return; }
            if (service.TryGetCase(id, out _)) navigator.Open(new NavigationRequest(PageId.Phone, id));
        }

        public void Dispose()
        {
            if (!bound) return;
            bound = false;
            view.CaseSelected -= OnSelected;
        }
    }
}
