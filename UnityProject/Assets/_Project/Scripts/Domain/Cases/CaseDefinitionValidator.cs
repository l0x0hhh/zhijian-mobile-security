// 说明：聚合案件及多来源证据的引用错误，阻止不可达或计分不完整的内容进入会话。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zhijian.Domain.Outcomes;

namespace Zhijian.Domain.Cases
{
    public sealed class ConfigurationError
    {
        public string Code { get; }
        public string ItemId { get; }
        public string Message { get; }
        internal ConfigurationError(string code, string itemId, string message)
        {
            Code = code; ItemId = itemId; Message = message;
        }
    }

    public static class CaseDefinitionValidator
    {
        public static IReadOnlyList<ConfigurationError> Validate(CaseDefinition definition, OutcomeResolver resolver)
        {
            var check = new Checks();
            if (definition == null)
            {
                check.Require(false, "case.null", null, "案件定义不能为空。");
                return check.Result();
            }
            check.Id(definition.Id, "case");
            check.Require(!string.IsNullOrWhiteSpace(definition.Title), "case.title", definition.Id, "案件标题不能为空。");
            check.Require(definition.ContentVersion > 0 && definition.ScoringRuleVersion > 0,
                "case.version", definition.Id, "内容版本与评分规则版本必须为正整数。");
            check.Require(Enum.IsDefined(typeof(CaseKind), definition.Kind), "case.kind", definition.Id, "案件类型无效。");
            check.Require(resolver != null && resolver.IsRegistered(definition.PolicyId, definition.Kind),
                "case.policy", definition.PolicyId, "案件策略未注册或策略与案件类型不匹配。");
            var sources = check.Items(definition.Sources, item => item.Id, "source");
            var messages = check.Items(definition.Messages, item => item.Id, "message");
            var media = check.Items(definition.Media, item => item.Id, "media");
            var evidence = check.Items(definition.Evidence, item => item.Id, "evidence");
            var permissions = check.Items(definition.Permissions, item => item.Id, "permission");
            var intents = check.Items(definition.FeatureIntents, item => item.Id, "intent");
            var verifications = check.Items(definition.VerificationOptions, item => item.Id, "verification");
            var risks = check.Items(definition.RiskOptions, item => item.Id, "risk");
            var dispositions = check.Items(definition.DispositionOptions, item => item.Id, "disposition");
            CheckSteps(check, definition, sources);
            CheckSources(check, sources, messages, media);
            CheckMessages(check, messages, sources, media, evidence, dispositions);
            CheckMedia(check, definition.Kind, media, sources, messages);
            check.Require(evidence.Count(item => item.IsKey) == 4 && evidence.Where(item => item.IsKey).Sum(item => (long)item.ScoreWeight) == 40,
                "evidence.key-count", definition.Id, "必须恰好四条关键证据，合计 40 分。");
            foreach (var item in evidence)
            {
                check.Require(item.ScoreWeight == (item.IsKey ? 10 : 0), "evidence.weight", item.Id, "关键证据每条 10 分，补充证据为 0 分。");
                check.Require(!string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Explanation),
                    "evidence.text", item.Id, "证据标题与解释不能为空。");
                check.Require(item.SourceIds.Count > 0 && item.SourceIds.Distinct(StringComparer.Ordinal).Count() == item.SourceIds.Count,
                    "evidence.source-duplicate", item.Id, "取证入口须至少一个，且不能重复。");
                check.Require(item.RequiredSourceIds.Distinct(StringComparer.Ordinal).Count() == item.RequiredSourceIds.Count,
                    "evidence.required-source-duplicate", item.Id, "必须查看的资料不能重复。");
                foreach (var sourceId in item.SourceIds.Concat(item.RequiredSourceIds))
                    check.Require(!string.IsNullOrWhiteSpace(sourceId) && sources.Any(source => source.Id == sourceId),
                        "evidence.source", item.Id, "证据入口或必须查看的资料不存在。");
                foreach (var source in sources.Where(value => item.SourceIds.Contains(value.Id) && value.Kind == SourceKind.Message))
                    check.Require(messages.Any(message => message.SourceId == source.Id && message.EvidenceIds.Contains(item.Id)),
                        "evidence.message-entry", item.Id, "消息来源证据必须在对应消息上提供取证入口。");
            }
            CheckPermissions(check, definition, permissions, intents);
            foreach (var intent in intents)
                check.Require(!string.IsNullOrWhiteSpace(intent.Title), "intent.title", intent.Id, "用途名称不能为空。");
            foreach (var option in verifications)
                check.Require(!string.IsNullOrWhiteSpace(option.Text) && !string.IsNullOrWhiteSpace(option.Feedback),
                    "verification.text", option.Id, "核验选项需提供名称与反馈。");
            check.Require(verifications.Any(item => item.Reliable) && verifications.Any(item => !item.Reliable),
                "verification.alternatives", definition.Id, "必须同时提供可靠与不可靠核验选项。");
            check.Require(risks.Any(item => item.IsCorrect), "risk.correct-option", definition.Id, "至少提供一个正确风险分类。");
            foreach (var option in risks)
                check.Require(!string.IsNullOrWhiteSpace(option.Text), "risk.text", option.Id, "风险分类文字不能为空。");
            CheckDispositions(check, definition.Kind, dispositions);
            CheckOutcomes(check, definition);
            return check.Result();
        }

        public static IReadOnlyList<ConfigurationError> ValidateCatalog(IEnumerable<CaseDefinition> definitions, OutcomeResolver resolver)
        {
            var errors = new List<ConfigurationError>();
            if (definitions == null)
                errors.Add(new ConfigurationError("catalog.null", null, "案件目录不能为空。"));
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var count = 0;
                foreach (var definition in definitions)
                {
                    count++;
                    if (definition != null && !seen.Add(definition.Id))
                        errors.Add(new ConfigurationError("catalog.duplicate-case-id", definition.Id, "案件目录包含重复 caseId。"));
                    errors.AddRange(Validate(definition, resolver));
                }
                if (count == 0) errors.Add(new ConfigurationError("catalog.empty", null, "案件目录至少需要一个案件。"));
            }
            return new ReadOnlyCollection<ConfigurationError>(errors.ToArray());
        }
        private static void CheckSteps(Checks check, CaseDefinition definition, List<SourceDefinition> sources)
        {
            var steps = definition.RequiredSteps;
            check.Require(steps.Distinct().Count() == steps.Count, "steps.duplicate", definition.Id, "必做步骤不能重复。");
            foreach (var step in steps)
                check.Require(Enum.IsDefined(typeof(InvestigationStep), step), "steps.unknown", definition.Id, "必做步骤包含未知值。");
            var common = new[] { InvestigationStep.BackgroundViewed, InvestigationStep.AnyEvidenceCollected,
                InvestigationStep.PermissionsReviewed, InvestigationStep.VerificationSelected,
                InvestigationStep.RiskSelected, InvestigationStep.DispositionSelected };
            foreach (var step in common)
                check.Require(steps.Contains(step), "steps.required", step.ToString(), "缺少普通提交必要步骤。");
            check.Require(sources.Any(item => item.RequiredForBackground), "background.missing", definition.Id, "至少一个可阅读来源须标记为必读背景。");
            if (definition.Kind == CaseKind.PhotoPermission)
            {
                check.Require(!steps.Contains(InvestigationStep.MediaPlayed) && !steps.Contains(InvestigationStep.MediaDetected),
                    "steps.photo-media", definition.Id, "证件照案件不应要求播放或检测媒体。");
                check.Require(steps.Contains(InvestigationStep.FeatureIntentSelected), "steps.photo-intent", definition.Id, "证件照案件需要主动选择功能用途。");
                check.Require(sources.Any(item => item.Kind == SourceKind.AppDetails && item.RequiredForBackground) &&
                    sources.Any(item => item.Kind == SourceKind.PrivacyPolicy && item.RequiredForBackground),
                    "background.photo", definition.Id, "证件照案件须查看应用详情与隐私政策。");
            }
            else if (definition.Kind == CaseKind.VoiceLoan || definition.Kind == CaseKind.FaceMeeting)
            {
                check.Require(steps.Contains(InvestigationStep.MediaPlayed) && steps.Contains(InvestigationStep.MediaDetected),
                    "steps.media", definition.Id, "语音与会议案件必须包含媒体播放与检测步骤。");
                check.Require(sources.Any(item => item.RequiredForBackground && (item.Kind == SourceKind.Background || item.Kind == SourceKind.Message)),
                    "background.chat", definition.Id, "语音与会议案件须有可查看的聊天或背景入口。");
            }
        }

        private static void CheckSources(Checks check, List<SourceDefinition> sources, List<MessageDefinition> messages, List<MediaDefinition> media)
        {
            foreach (var source in sources)
            {
                check.Require(Enum.IsDefined(typeof(SourceKind), source.Kind), "source.kind", source.Id, "来源类型无效。");
                var mediaSource = source.Kind == SourceKind.Media || source.Kind == SourceKind.Detection;
                if (mediaSource)
                {
                    var target = media.FirstOrDefault(item => item.Id == source.MediaId);
                    check.Require(target != null, "source.media", source.Id, "媒体来源引用不存在的媒体。");
                    if (target != null && source.Kind == SourceKind.Detection)
                        check.Require(target.DetectionIndicators.Count > 0, "source.detection", source.Id, "检测来源必须有模拟检测配置。");
                }
                else
                    check.Require(string.IsNullOrEmpty(source.MediaId), "source.unexpected-media", source.Id, "普通来源不应附带媒体引用。");
                if (source.Kind == SourceKind.Message)
                    check.Require(messages.Any(item => item.SourceId == source.Id), "source.message-unreachable", source.Id, "消息来源没有对应消息入口。");
                if (source.RequiredForBackground)
                    check.Require(!mediaSource && source.Kind != SourceKind.Verification,
                        "source.background-gated", source.Id, "必读背景须为直接可阅读来源，不能依赖媒体或核验完成。");
            }
        }

        private static void CheckMessages(Checks check, List<MessageDefinition> messages, List<SourceDefinition> sources,
            List<MediaDefinition> media, List<EvidenceDefinition> evidence, List<DispositionOption> dispositions)
        {
            foreach (var message in messages)
            {
                check.Require(Enum.IsDefined(typeof(MessageKind), message.Kind), "message.kind", message.Id, "消息类型无效。");
                check.Require(!string.IsNullOrWhiteSpace(message.Text), "message.text", message.Id, "消息需有可读文字或媒体说明。");
                check.Require(sources.Any(item => item.Id == message.SourceId && item.Kind == SourceKind.Message),
                    "message.source", message.Id, "消息必须引用消息类型的来源。");
                var target = media.FirstOrDefault(item => item.Id == message.MediaId);
                if (!string.IsNullOrEmpty(message.MediaId))
                    check.Require(target != null, "message.media", message.Id, "消息引用的媒体不存在。");
                if (message.Kind == MessageKind.Voice || message.Kind == MessageKind.Video || message.Kind == MessageKind.Image)
                {
                    var kind = message.Kind == MessageKind.Voice ? MediaKind.Audio : message.Kind == MessageKind.Video ? MediaKind.Video : MediaKind.Image;
                    check.Require(target != null && target.Kind == kind, "message.media-kind", message.Id, "媒体消息缺少匹配类型的媒体。");
                }
                check.Require(message.EvidenceIds.Distinct(StringComparer.Ordinal).Count() == message.EvidenceIds.Count,
                    "message.evidence-duplicate", message.Id, "同一消息的证据引用不能重复。");
                foreach (var evidenceId in message.EvidenceIds)
                {
                    var item = evidence.FirstOrDefault(value => value.Id == evidenceId);
                    check.Require(!string.IsNullOrWhiteSpace(evidenceId) && item != null, "message.evidence", message.Id, "消息引用的证据不存在。");
                    if (item != null)
                        check.Require(item.SourceIds.Contains(message.SourceId) || sources.Any(source => item.SourceIds.Contains(source.Id) &&
                            source.MediaId == message.MediaId && !string.IsNullOrEmpty(message.MediaId)),
                            "message.evidence-source", message.Id, "证据入口须关联该消息或其媒体与检测来源。");
                }
                if (!string.IsNullOrEmpty(message.DispositionId))
                    check.Require(dispositions.Any(item => item.Id == message.DispositionId), "message.disposition", message.Id, "消息引用的处置动作不存在。");
            }
        }

        private static void CheckMedia(Checks check, CaseKind kind, List<MediaDefinition> media,
            List<SourceDefinition> sources, List<MessageDefinition> messages)
        {
            foreach (var item in media)
            {
                check.Require(Enum.IsDefined(typeof(MediaKind), item.Kind), "media.kind", item.Id, "媒体类型无效。");
                check.Require(!string.IsNullOrWhiteSpace(item.AssetLedgerId), "media.ledger", item.Id, "媒体须填写素材台账标识。");
                if (item.Kind == MediaKind.Audio || item.Kind == MediaKind.Video)
                    check.Require(!string.IsNullOrWhiteSpace(item.Transcript), "media.transcript", item.Id, "音视频须有文字稿或字幕。");
                check.Require(sources.Any(source => source.MediaId == item.Id) || messages.Any(message => message.MediaId == item.Id),
                    "media.unreachable", item.Id, "媒体没有可达来源或消息入口。");
                check.Require(item.DetectionIndicators.Distinct().Count() == item.DetectionIndicators.Count,
                    "media.indicator-duplicate", item.Id, "检测指标不可重复。");
                foreach (var indicator in item.DetectionIndicators)
                    check.Require(Enum.IsDefined(typeof(DetectionIndicatorKind), indicator), "media.indicator", item.Id, "检测指标类型无效。");
                if (item.DetectionIndicators.Count > 0)
                {
                    check.Require(item.Kind != MediaKind.Image, "media.image-detection", item.Id, "当前三案不启用静态图片检测。");
                    check.Require(item.DetectionIndicators.Count >= 2 && item.DetectionIndicators.Count <= 4,
                        "media.indicator-count", item.Id, "教学模拟检测须配置 2 至 4 项指标。");
                }
            }
            if (kind == CaseKind.VoiceLoan)
            {
                check.Require(messages.Any(message => message.Kind == MessageKind.Voice), "voice.message", null, "语音借款案件须有语音消息入口。");
                check.Require(media.Any(item => item.Kind == MediaKind.Audio && item.DetectionIndicators.Count >= 2 && item.DetectionIndicators.Count <= 4),
                    "voice.detection", null, "语音借款案件须提供可检测音频。");
            }
            if (kind == CaseKind.FaceMeeting)
            {
                var required = new[] { DetectionIndicatorKind.AudioVideoSync, DetectionIndicatorKind.FaceEdges, DetectionIndicatorKind.Lighting };
                check.Require(messages.Any(message => message.Kind == MessageKind.Video), "face.message", null, "会议案件须有视频消息入口。");
                check.Require(media.Any(item => item.Kind == MediaKind.Video && required.All(value => item.DetectionIndicators.Contains(value))),
                    "face.detection", null, "会议视频必须包含音画同步、面部边缘与光照一致性指标。");
            }
            if (kind == CaseKind.PhotoPermission)
            {
                check.Require(messages.Count == 0, "photo.chat", null, "证件照案件不配置无关聊天。");
                check.Require(media.All(item => item.Kind == MediaKind.Image && item.DetectionIndicators.Count == 0),
                    "photo.media", null, "证件照案件只允许预置图片，不启用音视频与模拟检测。");
            }
        }

        private static void CheckPermissions(Checks check, CaseDefinition definition, List<PermissionDefinition> permissions, List<FeatureIntentDefinition> intents)
        {
            check.Require(permissions.Count == 7 && permissions.Select(item => item.Category).Distinct().Count() == 7 &&
                permissions.All(item => Enum.IsDefined(typeof(PermissionCategory), item.Category)),
                "permission.categories", definition.Id, "必须恰好覆盖七类访问，且每类只配置一次。");
            check.Require(permissions.Sum(item => (long)item.ScoreWeight) == 30, "permission.total", definition.Id, "权限权重合计须为 30 分。");
            check.Require(intents.Count > 0, "intent.missing", definition.Id, "至少配置一个功能用途或固定上下文。");
            if (definition.Kind == CaseKind.PhotoPermission)
            {
                var expected = new[] { FeatureIntentIds.TakePhoto, FeatureIntentIds.SelectedPhoto, FeatureIntentIds.Abandon };
                check.Require(intents.Count == 3 && expected.All(id => intents.Any(item => item.Id == id)),
                    "intent.photo-values", definition.Id, "证件照用途必须为拍摄、指定照片与放弃功能三个稳定标识。");
                check.Require(definition.DefaultFeatureIntentId == null, "intent.photo-default", definition.Id, "证件照用途不得默认替用户选择。");
            }
            else
                check.Require(intents.Any(item => item.Id == definition.DefaultFeatureIntentId),
                    "intent.default", definition.Id, "非证件照案件须配置存在的默认用途上下文。");
            foreach (var permission in permissions)
            {
                check.Require(!string.IsNullOrWhiteSpace(permission.Purpose), "permission.purpose", permission.Id, "每项访问须说明用途。");
                check.Require(permission.ScoreWeight >= 0 && permission.ScoreWeight <= 30, "permission.weight", permission.Id, "权限权重应在 0 至 30 内。");
                var expectedScopes = permission.Category == PermissionCategory.Photos
                    ? new[] { PermissionScope.SelectedPhotos, PermissionScope.AllPhotos } : new[] { PermissionScope.Full };
                check.Require(permission.Scopes.Count == expectedScopes.Length && expectedScopes.All(scope => permission.Scopes.Contains(scope)),
                    "permission.scopes", permission.Id, "相册需区分指定照片与全部相册，其余访问仅使用 Full 范围。");
                var rules = check.Items(permission.IntentRules, item => item.IntentId, "permission.intent-rule");
                check.Require(rules.Count == intents.Count && intents.All(intent => rules.Count(rule => rule.IntentId == intent.Id) == 1),
                    "permission.intent-coverage", permission.Id, "每个用途须恰好配置一条权限规则。");
                foreach (var rule in rules)
                {
                    check.Require(intents.Any(item => item.Id == rule.IntentId), "permission.intent-reference", permission.Id, "权限引用了不存在的用途。");
                    check.Require(rule.AllowedScopes.Distinct().Count() == rule.AllowedScopes.Count && rule.AllowedScopes.All(scope => permission.Scopes.Contains(scope)),
                        "permission.accepted-scopes", permission.Id, "预期授权范围须合法且不可重复。");
                    check.Require(rule.AcceptsDenied || rule.AllowedScopes.Count > 0, "permission.no-answer", permission.Id, "每项用途规则须至少存在一个满分答案。");
                    check.Require(rule.RiskWeights.Count == permission.Scopes.Count && permission.Scopes.All(scope => rule.RiskWeights.ContainsKey(scope)),
                        "permission.risk-coverage", permission.Id, "风险表须覆盖每个可选授权范围且不含多余范围。");
                    foreach (var pair in rule.RiskWeights)
                        check.Require(pair.Value >= 0 && pair.Value <= 100 && permission.Scopes.Contains(pair.Key) &&
                            (rule.AllowedScopes.Contains(pair.Key) ? pair.Value == 0 : pair.Value > 0),
                            "permission.risk-weight", permission.Id, "风险须在 0 至 100 内；必要范围为 0，越权范围须大于 0。");
                    if (definition.Kind == CaseKind.FaceMeeting && (permission.Category == PermissionCategory.Sms || permission.Category == PermissionCategory.Contacts))
                        check.Require(rule.AcceptsDenied && rule.AllowedScopes.Count == 0,
                            "permission.face-sensitive", permission.Id, "会议应用的短信和通讯录在所有用途下都应拒绝。");
                    if (definition.Kind == CaseKind.PhotoPermission)
                    {
                        var necessaryCamera = permission.Category == PermissionCategory.Camera && rule.IntentId == FeatureIntentIds.TakePhoto;
                        var necessaryPhoto = permission.Category == PermissionCategory.Photos && rule.IntentId == FeatureIntentIds.SelectedPhoto;
                        var expected = necessaryCamera ? PermissionScope.Full : PermissionScope.SelectedPhotos;
                        check.Require(necessaryCamera || necessaryPhoto
                            ? !rule.AcceptsDenied && rule.AllowedScopes.Count == 1 && rule.AllowedScopes[0] == expected
                            : rule.AcceptsDenied && rule.AllowedScopes.Count == 0,
                            "permission.photo-minimal", permission.Id, "证件照须按三用途选择摄像头、指定照片或全拒绝，不能默认两者都需授权。");
                    }
                }
            }
        }

        private static void CheckDispositions(Checks check, CaseKind kind, List<DispositionOption> dispositions)
        {
            var correctKind = kind == CaseKind.VoiceLoan ? DispositionKind.RefuseTransferAndPreserve :
                kind == CaseKind.FaceMeeting ? DispositionKind.RejectInstallAndReport : DispositionKind.RefuseUntrustedApp;
            check.Require(dispositions.Any(item => item.IsCorrect && item.Kind == correctKind), "disposition.safe", null, "缺少符合本案语义的正确安全处置。");
            foreach (var option in dispositions)
            {
                check.Require(Enum.IsDefined(typeof(DispositionKind), option.Kind) && !string.IsNullOrWhiteSpace(option.Text),
                    "disposition.definition", option.Id, "处置类型须有效且文字不能为空。");
                check.Require(!option.IsCorrect || option.Kind == correctKind, "disposition.correct-kind", option.Id, "正确处置与本案安全路线不匹配。");
                check.Require(option.IsImmediateFailure == (kind == CaseKind.VoiceLoan && option.Kind == DispositionKind.TransferImmediately),
                    "disposition.immediate", option.Id, "仅语音案件的立即转账使用即时失败标记。");
                check.Require(option.Kind != DispositionKind.TransferImmediately || kind == CaseKind.VoiceLoan,
                    "disposition.transfer-case", option.Id, "立即转账只适用于语音借款案件。");
                check.Require(!option.IsCorrect || !option.IsImmediateFailure, "disposition.correct-danger", option.Id, "即时危险处置不能标为正确。");
            }
            if (kind == CaseKind.VoiceLoan)
            {
                var required = new[] { DispositionKind.RefuseTransferAndPreserve, DispositionKind.TransferImmediately,
                    DispositionKind.QuestionCurrentAccount, DispositionKind.FollowUntrustedLink };
                check.Require(dispositions.Count >= 4 && required.All(value => dispositions.Any(item => item.Kind == value)),
                    "voice.dispositions", null, "语音案件至少提供拒绝并留证、立即转账、追问当前账号与按链接核验四种处置。");
            }
            else
                check.Require(dispositions.Any(item => !item.IsCorrect), "disposition.wrong-option", null, "须提供错误处置供教学复盘。");
        }

        private static void CheckOutcomes(Checks check, CaseDefinition definition)
        {
            check.Require(definition.SupportedOutcomes.Distinct().Count() == definition.SupportedOutcomes.Count,
                "outcome.duplicate", definition.Id, "结局声明不能重复。");
            foreach (var outcome in definition.SupportedOutcomes)
                check.Require(Enum.IsDefined(typeof(OutcomeKind), outcome), "outcome.unknown", definition.Id, "结局包含未知值。");
            var specific = definition.Kind == CaseKind.VoiceLoan ? OutcomeKind.ScamFailure :
                definition.Kind == CaseKind.FaceMeeting ? OutcomeKind.MalwareFailure : OutcomeKind.PrivacyFailure;
            check.Require(definition.SupportedOutcomes.Contains(OutcomeKind.Safe) && definition.SupportedOutcomes.Contains(specific) &&
                definition.SupportedOutcomes.Contains(OutcomeKind.JudgmentFailure), "outcome.required", definition.Id,
                "结局须覆盖安全、本案特定失败及错误核验、分类或处置的判断失败。");
        }

        private sealed class Checks
        {
            private readonly List<ConfigurationError> errors = new List<ConfigurationError>();
            internal void Require(bool condition, string code, string itemId, string message)
            {
                if (!condition) errors.Add(new ConfigurationError(code, itemId, message));
            }
            internal void Id(string id, string category)
            {
                Require(!string.IsNullOrWhiteSpace(id) && id == id.Trim() && !id.Any(char.IsWhiteSpace),
                    category + ".id", id, "稳定标识不能为空或含空白。");
            }
            internal List<T> Items<T>(IEnumerable<T> input, Func<T, string> id, string category) where T : class
            {
                var items = input.ToList();
                Require(items.All(item => item != null), category + ".null-item", null, "配置集合包含空项目。");
                var present = items.Where(item => item != null).ToList();
                foreach (var item in present) Id(id(item), category);
                Require(present.GroupBy(id, StringComparer.Ordinal).All(group => group.Count() == 1),
                    category + ".duplicate-id", null, "同类配置标识必须唯一。");
                return present;
            }
            internal IReadOnlyList<ConfigurationError> Result()
            {
                return new ReadOnlyCollection<ConfigurationError>(errors.ToArray());
            }
        }
    }
}
