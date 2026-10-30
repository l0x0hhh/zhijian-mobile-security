// 说明：追加更新检查操作，保持原有页面操作编号和稳定行标识。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Presentation.Media;
using Zhijian.Presentation.Navigation;
namespace Zhijian.Presentation.Investigation
{
    public enum PageActionKind { Navigate, Collect, Intent, Permission, Verify, Risk, Disposition, Install, Submit, Play, Stop, Scan, RetrySave, NewSave, InspectApk, SelectApkPurpose, SaveApkReport, ToggleApkSection, ReloadApkReports, CheckUpdates, OpenUpdateRelease }
    public enum AccessChoice { Denied, Full, SelectedPhotos, AllPhotos }
    public enum RowTone { Plain, Soft, Selected }
    public sealed class PageAction
    {
        public PageActionKind Kind { get; }
        public string ItemId { get; }
        public PageId Page { get; }
        public AccessChoice Access { get; }
        public int Value { get; }
        public PageAction(PageActionKind kind, string itemId = null, PageId page = PageId.Home, AccessChoice access = AccessChoice.Denied, int value = 0)
        { Kind = kind; ItemId = itemId; Page = page; Access = access; Value = value; }
    }
    public sealed class InvestigationRow
    {
        public string Key { get; }
        public string Title { get; }
        public string Body { get; }
        public PageAction Action { get; }
        public string SecondaryText { get; }
        public PageAction SecondaryAction { get; }
        public string ReadSourceId { get; }
        public bool Enabled { get; }
        public RowTone Tone { get; }
        public InvestigationRow(string title, string body = null, PageAction action = null, bool enabled = true,
            RowTone tone = RowTone.Plain, string secondaryText = null, PageAction secondaryAction = null, string readSourceId = null, string key = null)
        {
            Key = key ?? (action != null ? "action:" + action.Kind + ":" + action.Page + ":" + action.ItemId + ":" + action.Access + ":" + action.Value :
                readSourceId != null ? "source:" + readSourceId : "text:" + title);
            if (string.IsNullOrWhiteSpace(Key)) throw new ArgumentException("页面行须有稳定标识。", nameof(key));
            Title = title; Body = body; Action = action; Enabled = enabled; Tone = tone;
            SecondaryText = secondaryText; SecondaryAction = secondaryAction; ReadSourceId = readSourceId;
        }
    }
    public sealed class InvestigationPageData
    {
        public string Key { get; }
        public string Title { get; }
        public string Lead { get; }
        public string Status { get; }
        public string Body { get; }
        public string ReadSourceId { get; }
        public MediaRequest Media { get; }
        public IReadOnlyList<InvestigationRow> Rows { get; }
        public InvestigationPageData(string key, string title, string lead, string status, string body,
            IEnumerable<InvestigationRow> rows, string readSourceId = null, MediaRequest media = null)
        {
            var copy = (rows ?? throw new ArgumentNullException(nameof(rows))).ToArray();
            if (copy.Any(row => row == null) || copy.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("同一页面的行标识不可为空或重复。", nameof(rows));
            Key = key; Title = title; Lead = lead; Status = status; Body = body;
            ReadSourceId = readSourceId; Media = media; Rows = Array.AsReadOnly(copy);
        }
    }
    public interface IInvestigationPageView
    {
        event Action<PageAction> ActionRequested;
        event Action<string> SourceRead;
        IMediaPlayback Playback { get; }
        ITeachingDetection Detection { get; }
        void Render(InvestigationPageData data);
    }
}
