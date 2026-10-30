// 说明：提供与引擎无关、深度只读的案件内容定义。
using System;
using System.Collections.Generic;

namespace Zhijian.Domain.Cases
{
    public enum CaseKind { VoiceLoan, FaceMeeting, PhotoPermission }
    public enum InvestigationStep { BackgroundViewed, AnyEvidenceCollected, MediaPlayed, MediaDetected, PermissionsReviewed, VerificationSelected, RiskSelected, DispositionSelected, FeatureIntentSelected }
    public enum SourceKind { Background, Message, AppDetails, PrivacyPolicy, PermissionDeclaration, Verification, Media, Detection }
    public enum MessageKind { Text, Image, Voice, Video, Link, Package }
    public enum MediaKind { Audio, Video, Image }
    public enum DetectionIndicatorKind { Prosody, PauseContinuity, BackgroundNoise, AudioVideoSync, FaceEdges, Lighting }
    public enum OutcomeKind { Safe, ScamFailure, MalwareFailure, PrivacyFailure, JudgmentFailure }
    public enum DispositionKind { RefuseTransferAndPreserve, TransferImmediately, QuestionCurrentAccount, FollowUntrustedLink, RejectInstallAndReport, InstallUnknownPackage, RefuseUntrustedApp, ContinueUsingApp }

    public static class FeatureIntentIds
    {
        public const string TakePhoto = "take-photo";
        public const string SelectedPhoto = "selected-photo";
        public const string Abandon = "abandon";
    }

    public sealed class CaseDefinition
    {
        public CaseDefinition(string id, int contentVersion, int scoringRuleVersion, string title, CaseKind kind, string policyId,
            IEnumerable<InvestigationStep> requiredSteps, IEnumerable<SourceDefinition> sources,
            IEnumerable<MessageDefinition> messages, IEnumerable<MediaDefinition> media,
            IEnumerable<EvidenceDefinition> evidence, IEnumerable<PermissionDefinition> permissions,
            IEnumerable<FeatureIntentDefinition> featureIntents, string defaultFeatureIntentId,
            IEnumerable<VerificationOption> verificationOptions, IEnumerable<RiskOption> riskOptions,
            IEnumerable<DispositionOption> dispositionOptions, IEnumerable<OutcomeKind> supportedOutcomes)
        {
            Id = id;
            ContentVersion = contentVersion;
            ScoringRuleVersion = scoringRuleVersion;
            Title = title;
            Kind = kind;
            PolicyId = policyId;
            RequiredSteps = DefinitionCopies.Values(requiredSteps);
            Sources = DefinitionCopies.Definitions(sources, value => value.Copy());
            Messages = DefinitionCopies.Definitions(messages, value => value.Copy());
            Media = DefinitionCopies.Definitions(media, value => value.Copy());
            Evidence = DefinitionCopies.Definitions(evidence, value => value.Copy());
            Permissions = DefinitionCopies.Definitions(permissions, value => value.Copy());
            FeatureIntents = DefinitionCopies.Definitions(featureIntents, value => value.Copy());
            DefaultFeatureIntentId = defaultFeatureIntentId;
            VerificationOptions = DefinitionCopies.Definitions(verificationOptions, value => value.Copy());
            RiskOptions = DefinitionCopies.Definitions(riskOptions, value => value.Copy());
            DispositionOptions = DefinitionCopies.Definitions(dispositionOptions, value => value.Copy());
            SupportedOutcomes = DefinitionCopies.Values(supportedOutcomes);
        }

        public string Id { get; }
        public int ContentVersion { get; }
        public int ScoringRuleVersion { get; }
        public string Title { get; }
        public CaseKind Kind { get; }
        public string PolicyId { get; }
        public IReadOnlyList<InvestigationStep> RequiredSteps { get; }
        public IReadOnlyList<SourceDefinition> Sources { get; }
        public IReadOnlyList<MessageDefinition> Messages { get; }
        public IReadOnlyList<MediaDefinition> Media { get; }
        public IReadOnlyList<EvidenceDefinition> Evidence { get; }
        public IReadOnlyList<PermissionDefinition> Permissions { get; }
        public IReadOnlyList<FeatureIntentDefinition> FeatureIntents { get; }
        public string DefaultFeatureIntentId { get; }
        public IReadOnlyList<VerificationOption> VerificationOptions { get; }
        public IReadOnlyList<RiskOption> RiskOptions { get; }
        public IReadOnlyList<DispositionOption> DispositionOptions { get; }
        public IReadOnlyList<OutcomeKind> SupportedOutcomes { get; }
    }

    public sealed class SourceDefinition
    {
        public SourceDefinition(string id, SourceKind kind, bool requiredForBackground, string mediaId = null)
        {
            Id = id;
            Kind = kind;
            RequiredForBackground = requiredForBackground;
            MediaId = mediaId;
        }

        public string Id { get; }
        public SourceKind Kind { get; }
        public bool RequiredForBackground { get; }
        public string MediaId { get; }
        internal SourceDefinition Copy() { return new SourceDefinition(Id, Kind, RequiredForBackground, MediaId); }
    }

    public sealed class MessageDefinition
    {
        public MessageDefinition(string id, string sourceId, MessageKind kind, string text, string mediaId, IEnumerable<string> evidenceIds, string dispositionId = null)
        {
            Id = id;
            SourceId = sourceId;
            Kind = kind;
            Text = text;
            MediaId = mediaId;
            EvidenceIds = DefinitionCopies.Values(evidenceIds);
            DispositionId = dispositionId;
        }

        public string Id { get; }
        public string SourceId { get; }
        public MessageKind Kind { get; }
        public string Text { get; }
        public string MediaId { get; }
        public IReadOnlyList<string> EvidenceIds { get; }
        public string DispositionId { get; }
        internal MessageDefinition Copy() { return new MessageDefinition(Id, SourceId, Kind, Text, MediaId, EvidenceIds, DispositionId); }
    }

    public sealed class MediaDefinition
    {
        public MediaDefinition(string id, MediaKind kind, string transcript, string assetLedgerId, IEnumerable<DetectionIndicatorKind> detectionIndicators)
        {
            Id = id;
            Kind = kind;
            Transcript = transcript;
            AssetLedgerId = assetLedgerId;
            DetectionIndicators = DefinitionCopies.Values(detectionIndicators);
        }

        public string Id { get; }
        public MediaKind Kind { get; }
        public string Transcript { get; }
        public string AssetLedgerId { get; }
        public IReadOnlyList<DetectionIndicatorKind> DetectionIndicators { get; }
        internal MediaDefinition Copy() { return new MediaDefinition(Id, Kind, Transcript, AssetLedgerId, DetectionIndicators); }
    }

    public sealed class FeatureIntentDefinition
    {
        public FeatureIntentDefinition(string id, string title) { Id = id; Title = title; }
        public string Id { get; }
        public string Title { get; }
        internal FeatureIntentDefinition Copy() { return new FeatureIntentDefinition(Id, Title); }
    }

    public sealed class VerificationOption
    {
        public VerificationOption(string id, string text, bool reliable, string feedback)
        {
            Id = id;
            Text = text;
            Reliable = reliable;
            Feedback = feedback;
        }

        public string Id { get; }
        public string Text { get; }
        public bool Reliable { get; }
        public string Feedback { get; }
        internal VerificationOption Copy() { return new VerificationOption(Id, Text, Reliable, Feedback); }
    }

    public sealed class RiskOption
    {
        public RiskOption(string id, string text, bool isCorrect) { Id = id; Text = text; IsCorrect = isCorrect; }
        public string Id { get; }
        public string Text { get; }
        public bool IsCorrect { get; }
        internal RiskOption Copy() { return new RiskOption(Id, Text, IsCorrect); }
    }

    public sealed class DispositionOption
    {
        public DispositionOption(string id, string text, DispositionKind kind, bool isCorrect, bool isImmediateFailure)
        {
            Id = id;
            Text = text;
            Kind = kind;
            IsCorrect = isCorrect;
            IsImmediateFailure = isImmediateFailure;
        }

        public string Id { get; }
        public string Text { get; }
        public DispositionKind Kind { get; }
        public bool IsCorrect { get; }
        public bool IsImmediateFailure { get; }
        internal DispositionOption Copy() { return new DispositionOption(Id, Text, Kind, IsCorrect, IsImmediateFailure); }
    }

    internal static class DefinitionCopies
    {
        internal static IReadOnlyList<T> Values<T>(IEnumerable<T> source)
        {
            return (source == null ? new List<T>() : new List<T>(source)).AsReadOnly();
        }

        internal static IReadOnlyList<T> Definitions<T>(IEnumerable<T> source, Func<T, T> copy) where T : class
        {
            var result = new List<T>();
            if (source != null)
                foreach (var value in source)
                    result.Add(value == null ? null : copy(value));
            return result.AsReadOnly();
        }
    }
}
