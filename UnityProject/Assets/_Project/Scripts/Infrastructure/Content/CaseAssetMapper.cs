// 说明：将 JSON 深度转换为只读定义，拒绝未知版本、未知枚举与缺失列表。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zhijian.Application.Cases;
using Zhijian.Domain.Cases;

namespace Zhijian.Infrastructure.Content
{
    public static class CaseAssetMapper
    {
        public static CaseContent Map(TextAsset source)
        {
            if (source == null) throw new ArgumentException("案件 JSON 引用缺失。", nameof(source));
            var dto = JsonUtility.FromJson<CaseContentDto>(source.text);
            if (dto == null || dto.schemaVersion != 1) throw new FormatException("未知或缺失的内容 schemaVersion；当前仅支持 1。");
            var sources = Items(dto.sources, "sources");
            var messages = Items(dto.messages, "messages");
            var media = Items(dto.media, "media");
            var evidence = Items(dto.evidence, "evidence");
            var permissions = Items(dto.permissions, "permissions");
            var definition = new CaseDefinition(dto.id, dto.contentVersion, dto.scoringRuleVersion, dto.title,
                Parse<CaseKind>(dto.kind), dto.policyId, Values(dto.requiredSteps, "requiredSteps").Select(Parse<InvestigationStep>),
                sources.Select(item => new SourceDefinition(item.id, Parse<SourceKind>(item.kind), item.requiredForBackground, item.mediaId)),
                messages.Select(item => new MessageDefinition(item.id, item.sourceId, Parse<MessageKind>(item.kind), item.text,
                    item.mediaId, Values(item.evidenceIds, item.id + ".evidenceIds"), item.dispositionId)),
                media.Select(item => new MediaDefinition(item.id, Parse<MediaKind>(item.kind), item.transcript, item.assetLedgerId,
                    Items(item.findings, item.id + ".findings").Select(finding => Parse<DetectionIndicatorKind>(finding.indicator)))),
                evidence.Select(item => new EvidenceDefinition(item.id, item.sourceId, item.title, item.explanation, item.isKey,
                    item.scoreWeight, Values(item.alternativeSourceIds, item.id + ".alternativeSourceIds"), Values(item.requiredSourceIds, item.id + ".requiredSourceIds"))),
                permissions.Select(MapPermission), Items(dto.featureIntents, "featureIntents").Select(item => new FeatureIntentDefinition(item.id, item.title)),
                dto.defaultFeatureIntentId,
                Items(dto.verificationOptions, "verificationOptions").Select(item => new VerificationOption(item.id, item.text, item.reliable, item.feedback)),
                Items(dto.riskOptions, "riskOptions").Select(item => new RiskOption(item.id, item.text, item.isCorrect)),
                Items(dto.dispositionOptions, "dispositionOptions").Select(item => new DispositionOption(item.id, item.text,
                    Parse<DispositionKind>(item.kind), item.isCorrect, item.isImmediateFailure)),
                Values(dto.supportedOutcomes, "supportedOutcomes").Select(Parse<OutcomeKind>));
            var text = sources.Select(item =>
            {
                if (string.IsNullOrWhiteSpace(item.title) || string.IsNullOrWhiteSpace(item.body))
                    throw new FormatException("来源缺少可展示正文：" + item.id);
                return new SourceContent(item.id, item.title, item.body);
            });
            var findings = media.SelectMany(item => Items(item.findings, item.id + ".findings").Select(finding =>
            {
                if (string.IsNullOrWhiteSpace(finding.label) || string.IsNullOrWhiteSpace(finding.explanation))
                    throw new FormatException("教学指标缺少名称或解释：" + item.id);
                return new DetectionFinding(item.id, Parse<DetectionIndicatorKind>(finding.indicator), finding.label, finding.explanation);
            }));
            var effects = new Dictionary<string, DispositionEffect>(StringComparer.Ordinal);
            foreach (var option in dto.dispositionOptions)
            {
                var effect = Parse<DispositionEffect>(option.effect);
                if (effect != DispositionEffect.None && (definition.Kind != CaseKind.FaceMeeting ||
                    (effect == DispositionEffect.RemoveSimulatedApp && (!option.isCorrect || option.kind != "RejectInstallAndReport")) ||
                    (effect == DispositionEffect.InstallAndGrantSensitive && (option.isCorrect || option.kind != "InstallUnknownPackage"))))
                    throw new FormatException("处置模拟动作与案件语义不一致：" + option.id);
                effects.Add(option.id, effect);
            }
            return new CaseContent(definition, dto.summary, text, findings, effects);
        }
        private static PermissionDefinition MapPermission(PermissionDto item)
        {
            return new PermissionDefinition(item.id, item.purpose, Parse<PermissionCategory>(item.category), item.scoreWeight,
                Values(item.scopes, item.id + ".scopes").Select(Parse<PermissionScope>),
                Items(item.intentRules, item.id + ".intentRules").Select(rule => new PermissionIntentRule(rule.intentId,
                    Values(rule.allowedScopes, item.id + ".allowedScopes").Select(Parse<PermissionScope>), rule.acceptsDenied,
                    Items(rule.riskWeights, item.id + ".riskWeights").ToDictionary(pair => Parse<PermissionScope>(pair.scope), pair => pair.weight))));
        }
        private static T Parse<T>(string value) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value) || !Enum.GetNames(typeof(T)).Contains(value))
                throw new FormatException("未知 " + typeof(T).Name + "：" + value);
            return (T)Enum.Parse(typeof(T), value, false);
        }
        private static T[] Items<T>(T[] values, string name) where T : class
        {
            if (values == null || values.Any(item => item == null)) throw new FormatException("列表缺失或含空项：" + name);
            return values;
        }
        private static string[] Values(string[] values, string name)
        {
            if (values == null || values.Any(string.IsNullOrWhiteSpace)) throw new FormatException("标识列表缺失或含空值：" + name);
            return values;
        }
    }
}
