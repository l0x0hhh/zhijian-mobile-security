// 说明：证件照资料和政策在原文旁取证，减少目录层级并给出清晰的下一步。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Application.Cases;
using Zhijian.Domain.Cases;
using Zhijian.Presentation.Media;
using Zhijian.Presentation.Navigation;
namespace Zhijian.Presentation.Investigation
{
    public static class SourcePageComposer
    {
        public static InvestigationPageData Build(PageComposerContext c)
        {
            var rows = new List<InvestigationRow>();
            if (c.Definition == null) return c.Page("资料暂不可用", "请返回选择其他案件。", null, rows);
            switch (c.Route.Page)
            {
                case PageId.Chat:
                    foreach (var m in c.Definition.Messages)
                        rows.Add(new InvestigationRow(m.Text, m.MediaId != null ? "点击查看媒体" : "点击查看详情与线索",
                            c.Go(m.MediaId != null ? PageId.Media : PageId.Source, m.MediaId ?? m.SourceId),
                            readSourceId: m.Kind == MessageKind.Image ? null : m.SourceId, key: "message:" + m.Id));
                    rows.Add(c.Link("核实对方身份", PageId.Verification));
                    rows.Add(c.Link("整理线索并作出判断", PageId.Evidence));
                    return c.Page("会话记录", "教学模拟 · 页面中的链接和安装均为模拟操作", null, rows);
                case PageId.Media:
                case PageId.Detection:
                    var media = c.Definition.Media.FirstOrDefault(x => x.Id == c.Route.ItemId);
                    if (media == null) return c.Page("未找到媒体", null, null, rows);
                    var detected = c.State != null && c.State.DetectedMediaIds.Contains(media.Id);
                    var request = new MediaRequest(c.Workspace.CaseId, media.Id, (MediaDisplayKind)(int)media.Kind,
                        c.State?.SessionId ?? Guid.Empty, Guid.Empty);
                    if (c.Route.Page == PageId.Media)
                    {
                        rows.Add(new InvestigationRow(media.Kind == MediaKind.Image ? "查看原图" : "从头播放", null, new PageAction(PageActionKind.Play, media.Id)));
                        if (media.Kind != MediaKind.Image) rows.Add(new InvestigationRow("停止播放", null, new PageAction(PageActionKind.Stop)));
                        if (media.DetectionIndicators.Count > 0) rows.Add(c.Link("教学检测", PageId.Detection, media.Id, "预设观察提示，不能鉴定现实媒体真伪。"));
                        foreach (var source in c.Definition.Sources.Where(x => x.Kind == SourceKind.Media && x.MediaId == media.Id)) c.AddClues(rows, source.Id);
                        foreach (var m in c.Definition.Messages.Where(x => x.MediaId == media.Id && media.Kind == MediaKind.Image)) c.AddClues(rows, m.SourceId);
                        return c.Page(media.Kind == MediaKind.Audio ? "语音播放器" : media.Kind == MediaKind.Video ? "会议视频" : "收款示意图",
                            "教学模拟 · 请结合其他来源判断", media.Transcript, rows, media: request);
                    }
                    rows.Add(new InvestigationRow("开始教学检测", "演示使用案件预设观察点，不上传媒体，不计算真实置信度。",
                        new PageAction(PageActionKind.Scan, media.Id), c.CanChange));
                    if (detected)
                    {
                        foreach (var finding in c.Content.Findings.Where(x => x.MediaId == media.Id)) rows.Add(new InvestigationRow(finding.Label, finding.Explanation, key: "finding:" + finding.MediaId + ":" + finding.Indicator));
                        foreach (var source in c.Definition.Sources.Where(x => x.Kind == SourceKind.Detection && x.MediaId == media.Id)) c.AddClues(rows, source.Id);
                    }
                    return c.Page("教学检测", "案件预设演示 · 不是真实鉴定", detected ? "观察提示已生成；可以逐条整理线索。" : "先完整播放音视频，再开始检测。", rows);
                case PageId.Album:
                    if (c.Definition.Kind == CaseKind.PhotoPermission)
                    {
                        rows.Add(new InvestigationRow("重新显示示例照片", "原创虚构人物；不读取设备相册。", new PageAction(PageActionKind.Play, "photo-permission.asset.photo")));
                        var photos = c.Definition.Permissions.FirstOrDefault(x => x.Category == PermissionCategory.Photos);
                        rows.Add(c.Link("返回相册权限", photos == null ? PageId.Permissions : PageId.PermissionDetail, photos?.Id));
                        return c.Page("指定照片示例", "这张图片仅用于解释访问范围。", null, rows,
                            media: new MediaRequest(c.Workspace.CaseId, "photo-permission.asset.photo", MediaDisplayKind.Image, c.State?.SessionId ?? Guid.Empty, Guid.Empty));
                    }
                    foreach (var image in c.Definition.Media.Where(x => x.Kind == MediaKind.Image)) rows.Add(c.Link("查看收款示意图", PageId.Media, image.Id));
                    if (rows.Count == 0) rows.Add(new InvestigationRow("当前案件没有相册媒体", "证件照案例中的指定照片只在应用内演示，不读取设备照片。"));
                    return c.Page("相册", "仅显示教学素材", null, rows);
                case PageId.Files:
                    if (c.Definition.Kind == CaseKind.FaceMeeting)
                    {
                        rows.Add(new InvestigationRow("会议助手安装包（模拟）", "所有操作只改变本次调查状态；不会下载安装真实软件。"));
                        foreach (var pair in new[] { new { Text="模拟下载", Value=1 }, new { Text="模拟下载完成", Value=2 }, new { Text="模拟安装", Value=3 }, new { Text="取消 / 删除 / 移除", Value=0 } })
                            rows.Add(c.Choice(pair.Text, null, new PageAction(PageActionKind.Install, value: pair.Value), (int?)c.State?.InstallState == pair.Value));
                        rows.Add(c.Link("检查申请的权限", PageId.Permissions));
                    }
                    else rows.Add(new InvestigationRow("没有待处理文件"));
                    return c.Page("文件", "教学模拟", null, rows);
                case PageId.AppDetails:
                    return c.Definition.Kind == CaseKind.PhotoPermission ? PhotoAppDetails(c) : SourceIndex(c, SourceKind.AppDetails, "应用详情");
                case PageId.PrivacyPolicy:
                    return c.Definition.Kind == CaseKind.PhotoPermission ? PhotoPolicy(c) : SourceIndex(c, SourceKind.PrivacyPolicy, "隐私政策");
                case PageId.Reference:
                    return c.Definition.Kind == CaseKind.PhotoPermission ? PhotoReference(c) : SourceIndex(c, SourceKind.Background, "参考资料");
                default:
                    if (!c.Content.Sources.TryGetValue(c.Route.ItemId ?? "", out var sourceContent)) return c.Page("资料暂不可用", null, null, rows);
                    c.AddClues(rows, sourceContent.Id);
                    if (c.Definition.Kind == CaseKind.VoiceLoan && sourceContent.Id.EndsWith("helper")) rows.Add(c.Link("检查权限请求", PageId.Permissions));
                    rows.Add(c.Link("应用详情", PageId.AppDetails));
                    if (c.Definition.Kind != CaseKind.PhotoPermission) rows.Add(c.Link("文件与安装包", PageId.Files));
                    rows.Add(c.Link("继续核实", PageId.Verification));
                    rows.Add(c.Link("线索夹", PageId.Evidence));
                    return c.Page(sourceContent.Title, "阅读完整资料后，可整理相关线索。", sourceContent.Body, rows, sourceContent.Id);
            }
        }

        private static InvestigationPageData PhotoAppDetails(PageComposerContext c)
        {
            var rows = new List<InvestigationRow>();
            var details = PhotoSource(c, "source.details");
            AddPhotoSource(c, rows, PhotoSource(c, "source.developer"));
            AddPhotoSource(c, rows, PhotoSource(c, "source.distribution"));
            rows.Add(new InvestigationRow("下一步：阅读隐私政策", action: c.Go(PageId.PrivacyPolicy), tone: RowTone.Selected));
            rows.Add(c.Link("查看访问权限", PageId.Permissions, body: "可先审查权限，再回看资料。"));
            return c.Page("应用详情", "1 / 4 · 查看应用与政策\n你准备制作一张证件照。先看看应用的功能、开发者和来源。",
                details?.Body, rows, details?.Id);
        }

        private static InvestigationPageData PhotoPolicy(PageComposerContext c)
        {
            var rows = new List<InvestigationRow>();
            var policy = PhotoSource(c, "source.policy");
            if (policy == null) return c.Page("政策暂不可用", "请返回应用详情。", null, rows);
            var sections = c.Definition.Sources.Where(x => x.Kind == SourceKind.PrivacyPolicy && x.Id != policy.Id)
                .Where(x => c.Content.Sources.ContainsKey(x.Id)).Select(x => c.Content.Sources[x.Id]).ToArray();
            var displayed = new HashSet<string>(StringComparer.Ordinal);
            var paragraphs = policy.Body.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < paragraphs.Length; index++)
            {
                var paragraph = paragraphs[index].Trim();
                var section = sections.FirstOrDefault(x => string.Equals(x.Body.Trim(), paragraph, StringComparison.Ordinal));
                rows.Add(new InvestigationRow(section?.Title ?? "政策原文", paragraph,
                    readSourceId: section?.Id, key: "policy-paragraph:" + index));
                if (section != null)
                {
                    displayed.Add(section.Id);
                    c.AddClues(rows, section.Id);
                }
            }
            // 未与全文段落匹配的配置仍展示原文，不能因合并页面丢失来源。
            foreach (var section in sections.Where(x => !displayed.Contains(x.Id))) AddPhotoSource(c, rows, section);
            var viewed = sections.Count(x => c.State != null && c.State.ViewedSourceIds.Contains(x.Id));
            // 各条款实际可见且页尾实际可见后，才登记全文；仅打开页面不会完成阅读。
            rows.Add(new InvestigationRow("政策阅读进度", viewed + " / " + sections.Length + " 条款已查看",
                readSourceId: viewed == sections.Length ? policy.Id : null, key: "policy-reading"));
            rows.Add(new InvestigationRow("下一步：判断访问权限", action: c.Go(PageId.Permissions), tone: RowTone.Selected));
            rows.Add(c.Link("回看应用资料", PageId.AppDetails));
            return c.Page("隐私政策", "1 / 4 · 查看应用与政策\n逐段阅读，可在原文旁把依据加入线索夹。", null, rows);
        }

        private static InvestigationPageData PhotoReference(PageComposerContext c)
        {
            var rows = new List<InvestigationRow>();
            var reference = PhotoSource(c, "source.reference");
            rows.Add(new InvestigationRow("返回核实来源", action: c.Go(PageId.Verification), tone: RowTone.Selected));
            return c.Page(reference?.Title ?? "独立来源资料", "3 / 4 · 核实来源\n对照发布主体和分发渠道后，再选择核验方式。",
                reference?.Body, rows, reference?.Id);
        }

        private static SourceContent PhotoSource(PageComposerContext c, string suffix)
            => c.Content.Sources.TryGetValue(c.Definition.Id + "." + suffix, out var source) ? source : null;

        private static void AddPhotoSource(PageComposerContext c, List<InvestigationRow> rows, SourceContent source)
        {
            if (source == null) return;
            rows.Add(new InvestigationRow(source.Title, source.Body, readSourceId: source.Id));
            c.AddClues(rows, source.Id);
        }

        private static InvestigationPageData SourceIndex(PageComposerContext c, SourceKind kind, string title)
        {
            var rows = new List<InvestigationRow>();
            foreach (var s in c.Definition.Sources.Where(x => x.Kind == kind))
                if (c.Content.Sources.TryGetValue(s.Id, out var text)) rows.Add(c.Link(text.Title, PageId.Source, s.Id));
            rows.Add(c.Link("权限管理", PageId.Permissions));
            if (c.Route.Page != PageId.PrivacyPolicy) rows.Add(c.Link("隐私政策", PageId.PrivacyPolicy));
            if (c.Route.Page != PageId.Reference) rows.Add(c.Link("参考资料", PageId.Reference));
            rows.Add(c.Link("线索与判断", PageId.Evidence));
            return c.Page(title, "逐项打开查看；列表展示不算阅读原文。", null, rows);
        }
    }
}
