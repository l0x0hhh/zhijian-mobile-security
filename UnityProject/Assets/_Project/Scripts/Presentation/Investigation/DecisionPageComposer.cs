// 说明：证件照案按用途、权限、核验和提交组织操作，显示完成进度并在相关原文旁取证。
using System.Collections.Generic;
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Sessions;
using Zhijian.Presentation.Navigation;
namespace Zhijian.Presentation.Investigation
{
    public static class DecisionPageComposer
    {
        public static InvestigationPageData Build(PageComposerContext c)
        {
            var rows = new List<InvestigationRow>();
            if (c.Definition == null) return c.Page("案件暂不可用", null, null, rows);
            var photo = c.Definition.Kind == CaseKind.PhotoPermission;
            if (photo && c.Route.Page == PageId.Permissions) return PhotoPermissions(c);
            if (photo && c.Route.Page == PageId.PermissionDetail) return PhotoPermissionDetail(c);
            if (photo && c.Route.Page == PageId.Evidence) return PhotoEvidence(c);
            switch (c.Route.Page)
            {
                case PageId.SecurityCenter:
                    foreach (var m in c.Definition.Media.Where(x => x.DetectionIndicators.Count > 0)) rows.Add(c.Link("检测音视频", PageId.Detection, m.Id));
                    rows.Add(c.Link("权限管理", PageId.Permissions));
                    rows.Add(c.Link("核实来源", PageId.Verification));
                    rows.Add(c.Link("参考资料", PageId.Reference));
                    return c.Page("安全中心", "从来源、内容和权限交叉检查。", null, rows);
                case PageId.Permissions:
                    foreach (var p in c.Definition.Permissions)
                    {
                        PermissionDecision decision = null;
                        c.State?.PermissionDecisions.TryGetValue(p.Id, out decision);
                        rows.Add(c.Link(PageComposerContext.PermissionTitle(p.Category) + " · " + PageComposerContext.DecisionText(decision), PageId.PermissionDetail, p.Id, p.Purpose));
                    }
                    foreach (var s in c.Definition.Sources.Where(x => x.Kind == SourceKind.PermissionDeclaration))
                        if (c.Content.Sources.TryGetValue(s.Id, out var text)) rows.Add(c.Link(text.Title, PageId.Source, s.Id));
                    rows.Add(c.Link("应用详情", PageId.AppDetails)); rows.Add(c.Link("隐私政策", PageId.PrivacyPolicy));
                    rows.Add(c.Link("核实来源", PageId.Verification)); rows.Add(c.Link("整理线索", PageId.Evidence));
                    return c.Page("权限管理", "所有授权只作用于虚拟手机，不申请设备权限。", c.State == null ? null : "当前隐私风险：" + c.State.CurrentPrivacyRisk, rows);
                case PageId.PermissionDetail:
                    var permission = c.Definition.Permissions.FirstOrDefault(x => x.Id == c.Route.ItemId);
                    if (permission == null) return c.Page("未找到权限", null, null, rows);
                    PermissionDecision current = null;
                    c.State?.PermissionDecisions.TryGetValue(permission.Id, out current);
                    rows.Add(c.Choice("拒绝", null, new PageAction(PageActionKind.Permission, permission.Id), current?.State == PermissionDecisionState.Denied));
                    foreach (var scope in permission.Scopes.Where(x => x != PermissionScope.None))
                    {
                        var choice = scope == PermissionScope.SelectedPhotos ? AccessChoice.SelectedPhotos : scope == PermissionScope.AllPhotos ? AccessChoice.AllPhotos : AccessChoice.Full;
                        var title = scope == PermissionScope.SelectedPhotos ? "仅指定的教学照片" : scope == PermissionScope.AllPhotos ? "全部相册" : "允许";
                        rows.Add(c.Choice(title, null, new PageAction(PageActionKind.Permission, permission.Id, access: choice), current?.State == PermissionDecisionState.Allowed && current.Scope == scope));
                    }
                    rows.Add(c.Link("查看权限声明", PageId.Permissions));
                    return c.Page(PageComposerContext.PermissionTitle(permission.Category), permission.Purpose, "先选用途，再逐项判断所需范围。", rows);
                case PageId.Verification:
                    if (photo) rows.Add(c.Link("查看独立来源资料", PageId.Reference));
                    foreach (var option in c.Definition.VerificationOptions)
                    {
                        var selected = c.State?.VerificationOptionId == option.Id;
                        rows.Add(new InvestigationRow((selected ? "已选 · " : "") + option.Text, selected ? option.Feedback : null,
                            new PageAction(PageActionKind.Verify, option.Id), c.CanChange, selected ? RowTone.Selected : RowTone.Plain,
                            readSourceId: selected ? c.Definition.Sources.FirstOrDefault(x => x.Kind == SourceKind.Verification)?.Id : null));
                    }
                    if (!photo) rows.Add(c.Link("参考资料", PageId.Reference));
                    rows.Add(new InvestigationRow(photo ? "下一步：整理线索" : "线索夹", action: c.Go(PageId.Evidence), tone: RowTone.Selected));
                    return c.Page("核实来源", photo ? "3 / 4 · 核实来源\n查看独立资料，再选择核验方式；选择后可看到反馈。" : "选择一种核验方式，查看相应反馈。", null, rows);

                case PageId.Conclusion:
                    rows.Add(new InvestigationRow("风险判断", "根据已查看的材料选择。"));
                    foreach (var option in c.Definition.RiskOptions) rows.Add(c.Choice(option.Text, null, new PageAction(PageActionKind.Risk, option.Id), c.State?.RiskOptionId == option.Id));
                    rows.Add(new InvestigationRow("下一步怎么做"));
                    foreach (var option in c.Definition.DispositionOptions) rows.Add(c.Choice(option.Text, null, new PageAction(PageActionKind.Disposition, option.Id), c.State?.DispositionOptionId == option.Id));
                    rows.Add(new InvestigationRow("提交调查", "提交后冻结本次结果。", new PageAction(PageActionKind.Submit), c.CanChange, RowTone.Selected));
                    if (photo) rows.Add(c.Link("回看线索与完成进度", PageId.Evidence));
                    return c.Page("调查结论", photo ? "4 / 4 · 作出判断\n根据已查看的材料选择风险与处置，然后提交。" : "危险操作也会在模拟中产生相应结果。", null, rows);
                default:
                    var collected = c.Definition.Evidence.Where(x => c.Collected(x.Id)).ToArray();
                    foreach (var e in collected) rows.Add(new InvestigationRow(e.Title, e.Explanation, tone: RowTone.Soft, key: "evidence:" + e.Id));
                    rows.Add(new InvestigationRow("已收集 " + collected.Length + " 条线索", "未发现的线索不提前公布答案。可返回资料详情，查看可加入的依据。", key: "evidence-count"));
                    rows.Add(c.Link("会话记录", PageId.Chat));
                    rows.Add(c.Link("核实来源", PageId.Verification)); rows.Add(c.Link("权限管理", PageId.Permissions));
                    rows.Add(c.Link("作出判断", PageId.Conclusion));
                    return c.Page("线索夹", "保留依据，再作判断。", null, rows);
            }
        }

        private static InvestigationPageData PhotoPermissions(PageComposerContext c)
        {
            var rows = new List<InvestigationRow>();
            c.Content.Sources.TryGetValue(c.Definition.Id + ".source.permissions", out var declaration);
            rows.Add(new InvestigationRow("这次想怎么做", key: "permission-intent-heading"));
            foreach (var intent in c.Definition.FeatureIntents)
                rows.Add(c.Choice(intent.Title, null, new PageAction(PageActionKind.Intent, intent.Id), c.State?.FeatureIntentId == intent.Id));
            rows.Add(new InvestigationRow("逐项选择访问范围", "点开一项，查看用途并作出选择。", key: "permission-list-heading"));
            foreach (var permission in c.Definition.Permissions)
            {
                PermissionDecision decision = null;
                c.State?.PermissionDecisions.TryGetValue(permission.Id, out decision);
                rows.Add(c.Link(PageComposerContext.PermissionTitle(permission.Category) + " · " + PageComposerContext.DecisionText(decision),
                    PageId.PermissionDetail, permission.Id, permission.Purpose));
            }
            if (declaration != null) c.AddClues(rows, declaration.Id);
            rows.Add(new InvestigationRow("下一步：核实来源", action: c.Go(PageId.Verification), tone: RowTone.Selected));
            rows.Add(c.Link("回看应用与政策", PageId.AppDetails));
            var progress = "已判断 " + ReviewedPermissions(c) + " / " + c.Definition.Permissions.Count + " 项";
            if (c.State != null) progress += " · 当前隐私风险：" + c.State.CurrentPrivacyRisk;
            return c.Page("访问权限", "2 / 4 · 判断权限\n先选用途，再逐项判断。这里的选择只影响本次练习。",
                (declaration == null ? "" : declaration.Body + "\n\n") + progress, rows, declaration?.Id);
        }

        private static InvestigationPageData PhotoPermissionDetail(PageComposerContext c)
        {
            var rows = new List<InvestigationRow>();
            var permission = c.Definition.Permissions.FirstOrDefault(x => x.Id == c.Route.ItemId);
            if (permission == null) return c.Page("未找到权限", null, null, rows);
            // 当前案件声明与权限沿用相同末段 ID，只引用内容中实际存在的原文。
            c.Content.Sources.TryGetValue(permission.Id.Replace(".permission.", ".source.permission."), out var declaration);
            var intent = c.Definition.FeatureIntents.FirstOrDefault(x => x.Id == c.State?.FeatureIntentId);
            PermissionDecision current = null;
            c.State?.PermissionDecisions.TryGetValue(permission.Id, out current);
            if (intent == null)
                rows.Add(c.Link("先选择本次用途", PageId.Permissions));
            else
            {
                rows.Add(c.Choice("拒绝", null, new PageAction(PageActionKind.Permission, permission.Id), current?.State == PermissionDecisionState.Denied));
                foreach (var scope in permission.Scopes.Where(x => x != PermissionScope.None))
                {
                    var choice = scope == PermissionScope.SelectedPhotos ? AccessChoice.SelectedPhotos : scope == PermissionScope.AllPhotos ? AccessChoice.AllPhotos : AccessChoice.Full;
                    var title = scope == PermissionScope.SelectedPhotos ? "仅指定的教学照片" : scope == PermissionScope.AllPhotos ? "全部相册" : "允许";
                    rows.Add(c.Choice(title, null, new PageAction(PageActionKind.Permission, permission.Id, access: choice),
                        current?.State == PermissionDecisionState.Allowed && current.Scope == scope));
                }
            }
            if (permission.Category == PermissionCategory.Photos) rows.Add(c.Link("查看指定照片示例", PageId.Album));
            if (declaration != null)
            {
                c.AddClues(rows, declaration.Id);
                if (permission.Category == PermissionCategory.Contacts || permission.Category == PermissionCategory.Sms)
                    c.AddClues(rows, c.Definition.Id + ".source.permissions");
            }
            if (intent != null) rows.Add(new InvestigationRow("返回权限列表", action: c.Go(PageId.Permissions), tone: RowTone.Selected));
            var purpose = intent == null ? "请先选择本次用途。" : "本次用途：" + intent.Title;
            return c.Page(PageComposerContext.PermissionTitle(permission.Category), "2 / 4 · 判断权限\n" + purpose,
                permission.Purpose + (declaration == null ? "" : "\n\n" + declaration.Body), rows, declaration?.Id);
        }

        private static InvestigationPageData PhotoEvidence(PageComposerContext c)
        {
            var rows = new List<InvestigationRow>();
            var collected = c.Definition.Evidence.Where(x => c.Collected(x.Id)).ToArray();
            foreach (var evidence in collected)
                rows.Add(new InvestigationRow(evidence.Title, evidence.Explanation, tone: RowTone.Soft, key: "evidence:" + evidence.Id));
            rows.Add(new InvestigationRow("已收集 " + collected.Length + " 条线索",
                "可回看原文补充依据，也可在完成必做操作后按现有线索提交。", key: "evidence-count"));
            rows.Add(new InvestigationRow("下一步：作出判断并提交", action: c.Go(PageId.Conclusion), tone: RowTone.Selected));
            rows.Add(new InvestigationRow("需要补充时", "可自由回看，已有选择和线索会保留。", key: "review-heading"));
            rows.Add(c.Link("回看应用与政策", PageId.AppDetails));
            rows.Add(c.Link("回看权限选择", PageId.Permissions));
            rows.Add(c.Link("回看核验方式", PageId.Verification));
            var required = c.Definition.Sources.Where(x => x.RequiredForBackground).ToArray();
            var read = required.Count(x => c.State != null && c.State.ViewedSourceIds.Contains(x.Id));
            var intent = c.Definition.FeatureIntents.FirstOrDefault(x => x.Id == c.State?.FeatureIntentId);
            var progress = "背景资料：" + read + " / " + required.Length + " 已查看\n本次用途：" + (intent?.Title ?? "未选择") +
                "\n权限：" + ReviewedPermissions(c) + " / " + c.Definition.Permissions.Count + " 已判断\n核验方式：" +
                (c.State?.VerificationOptionId == null ? "未选择" : "已选择");
            return c.Page("线索与提交", "4 / 4 · 整理线索并作出判断", progress, rows);
        }

        private static int ReviewedPermissions(PageComposerContext c)
            => c.State == null ? 0 : c.Definition.Permissions.Count(x =>
                c.State.PermissionDecisions.TryGetValue(x.Id, out var decision) && decision.State != PermissionDecisionState.Unreviewed);
    }
}
