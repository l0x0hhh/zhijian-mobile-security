// 说明：将选中情境映射到虚拟手机入口，证件照案例改用应用详情入口。
using System;
using Zhijian.Application.Cases;
using Zhijian.Presentation.Navigation;

namespace Zhijian.Presentation.Phone
{
    public sealed class PhoneViewData
    {
        public string Title { get; }
        public string Summary { get; }
        public bool IsPhoto { get; }
        public PhoneViewData(string title, string summary, bool isPhoto = false) { Title = title; Summary = summary; IsPhoto = isPhoto; }
    }

    public interface IPhoneView
    {
        event Action<PageId> AppSelected;
        void Render(PhoneViewData data);
    }

    public sealed class PhonePresenter : IDisposable
    {
        private readonly IPhoneView view;
        private readonly CaseSelectionService service;
        private readonly IPageNavigator navigator;
        private string caseId;
        private bool bound;
        public PhonePresenter(IPhoneView view, CaseSelectionService service, IPageNavigator navigator)
        { this.view = view; this.service = service; this.navigator = navigator; }
        public void Bind(string id)
        {
            if (bound) throw new InvalidOperationException("Phone presenter is already bound.");
            if (!service.TryGetCase(id, out var entry)) throw new ArgumentException("Unknown case id.", nameof(id));
            caseId = entry.Id;
            bound = true;
            view.AppSelected += OnAppSelected;
            view.Render(new PhoneViewData(entry.Title, entry.Summary, id == "photo-permission"));
        }
        private void OnAppSelected(PageId page)
        {
            if (page == PageId.Chat || page == PageId.Album || page == PageId.SecurityCenter ||
                page == PageId.Files || page == PageId.Settings || page == PageId.Evidence)
                navigator.Open(new NavigationRequest(page == PageId.Chat && caseId == "photo-permission" ? PageId.AppDetails : page, caseId));
        }
        public void Dispose()
        {
            if (!bound) return;
            bound = false;
            view.AppSelected -= OnAppSelected;
        }
    }
}
