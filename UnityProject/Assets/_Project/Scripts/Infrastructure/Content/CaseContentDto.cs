// 说明：JSON 内容传输结构只存字段，枚举使用稳定名称，由映射器严格解析。
using System;

namespace Zhijian.Infrastructure.Content
{
    [Serializable] public sealed class CaseContentDto
    {
        public int schemaVersion;
        public string id, title, summary, kind, policyId, defaultFeatureIntentId;
        public int contentVersion, scoringRuleVersion;
        public string[] requiredSteps, supportedOutcomes;
        public SourceDto[] sources;
        public MessageDto[] messages;
        public MediaDto[] media;
        public EvidenceDto[] evidence;
        public PermissionDto[] permissions;
        public IntentDto[] featureIntents;
        public VerificationDto[] verificationOptions;
        public RiskDto[] riskOptions;
        public DispositionDto[] dispositionOptions;
    }
    [Serializable] public sealed class SourceDto { public string id, kind, mediaId, title, body; public bool requiredForBackground; }
    [Serializable] public sealed class MessageDto { public string id, sourceId, kind, text, mediaId, dispositionId, sender; public string[] evidenceIds; }
    [Serializable] public sealed class MediaDto { public string id, kind, transcript, assetLedgerId; public FindingDto[] findings; }
    [Serializable] public sealed class FindingDto { public string indicator, label, explanation; }
    [Serializable] public sealed class EvidenceDto { public string id, sourceId, title, explanation; public bool isKey; public int scoreWeight; public string[] alternativeSourceIds, requiredSourceIds; }
    [Serializable] public sealed class PermissionDto { public string id, purpose, category; public int scoreWeight; public string[] scopes; public PermissionRuleDto[] intentRules; }
    [Serializable] public sealed class PermissionRuleDto { public string intentId; public string[] allowedScopes; public bool acceptsDenied; public RiskWeightDto[] riskWeights; }
    [Serializable] public sealed class RiskWeightDto { public string scope; public int weight; }
    [Serializable] public sealed class IntentDto { public string id, title; }
    [Serializable] public sealed class VerificationDto { public string id, text, feedback; public bool reliable; }
    [Serializable] public sealed class RiskDto { public string id, text; public bool isCorrect; }
    [Serializable] public sealed class DispositionDto { public string id, text, kind, effect; public bool isCorrect, isImmediateFailure; }
}
