// 说明：组合器提供稳定操作和证据行标识，状态更新保持同一行且不重复列出证据。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Application.Cases;
using Zhijian.Application.Ports;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Rules;
using Zhijian.Domain.Sessions;
using Zhijian.Presentation.Navigation;
namespace Zhijian.Presentation.Investigation
{
    public sealed class PageComposerContext
    {
        public InvestigationWorkspace Workspace { get; }
        public NavigationRequest Route { get; }
        public IProgressService Progress { get; }
        public string Notice { get; }
        public CaseContent Content => Workspace.Content;
        public CaseDefinition Definition => Content?.Definition;
        public SessionState State => Workspace.State;
        public bool CanChange => Workspace.CanChange;
        public string Status => !string.IsNullOrEmpty(Notice) ? Notice : State == null ? "交互预览 · 不计分、不保存" : State.Phase == SessionPhase.Resolved ? "本次调查已结案" : "调查进行中";
        public PageComposerContext(InvestigationWorkspace workspace, NavigationRequest route, IProgressService progress, string notice)
        { Workspace = workspace; Route = route; Progress = progress; Notice = notice; }
        public PageAction Go(PageId page, string itemId = null) => new PageAction(PageActionKind.Navigate, itemId, page);
        public InvestigationRow Link(string title, PageId page, string itemId = null, string body = null)
            => new InvestigationRow(title + "  ›", body, Go(page, itemId));
        public InvestigationRow Choice(string title, string body, PageAction action, bool selected = false)
            => new InvestigationRow((selected ? "已选 · " : "") + title, body, action, CanChange, selected ? RowTone.Selected : RowTone.Plain);
        public bool Collected(string id) => State != null && State.CollectedEvidenceIds.Contains(id);
        public void AddClues(List<InvestigationRow> rows, string sourceId)
        {
            foreach (var evidence in Definition.Evidence.Where(item => item.SourceIds.Contains(sourceId)))
            {
                var rowKey = "evidence:" + evidence.Id;
                if (rows.Any(row => row.Key == rowKey)) continue;
                var available = State != null && EvidenceRules.CanCollect(State, evidence.Id, out _);
                if (Collected(evidence.Id)) rows.Add(new InvestigationRow("已收集 · " + evidence.Title, evidence.Explanation, key: rowKey));
                else rows.Add(new InvestigationRow(available ? "加入线索 · " + evidence.Title : "加入线索", available ? "这条依据可以加入线索夹。" : "请先查看这条依据及相关资料。", new PageAction(PageActionKind.Collect, evidence.Id), CanChange && available, key: rowKey));
            }
        }
        public InvestigationPageData Page(string title, string lead, string body, IEnumerable<InvestigationRow> rows, string readSourceId = null,
            Zhijian.Presentation.Media.MediaRequest media = null)
            => new InvestigationPageData(Route.Page + ":" + Route.CaseId + ":" + Route.ItemId, title, lead, Status, body, rows, readSourceId, media);
        public static string PermissionTitle(PermissionCategory kind)
        {
            switch (kind)
            { case PermissionCategory.Camera: return "摄像头"; case PermissionCategory.Photos: return "相册";
              case PermissionCategory.Microphone: return "麦克风"; case PermissionCategory.Contacts: return "通讯录";
              case PermissionCategory.Sms: return "短信"; case PermissionCategory.Location: return "定位"; default: return "后台运行"; }
        }
        public static string DecisionText(PermissionDecision value)
            => value == null || value.State == PermissionDecisionState.Unreviewed ? "待判断" : value.State == PermissionDecisionState.Denied ? "拒绝" :
                value.Scope == PermissionScope.SelectedPhotos ? "仅指定照片" : value.Scope == PermissionScope.AllPhotos ? "全部相册" : "允许";
    }
}
