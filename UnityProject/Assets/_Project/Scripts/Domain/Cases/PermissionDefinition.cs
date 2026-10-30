// 说明：定义权限三态、访问范围及各用途的最小授权规则。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Zhijian.Domain.Cases
{
    public enum PermissionCategory { Camera, Photos, Microphone, Contacts, Sms, Location, BackgroundActivity }
    public enum PermissionScope { None, Full, SelectedPhotos, AllPhotos }
    public enum PermissionDecisionState { Unreviewed, Denied, Allowed }

    public sealed class PermissionDecision : IEquatable<PermissionDecision>
    {
        private PermissionDecision(PermissionDecisionState state, PermissionScope scope)
        {
            State = state;
            Scope = scope;
        }

        public static PermissionDecision Unreviewed { get; } = new PermissionDecision(PermissionDecisionState.Unreviewed, PermissionScope.None);
        public static PermissionDecision Denied { get; } = new PermissionDecision(PermissionDecisionState.Denied, PermissionScope.None);
        public PermissionDecisionState State { get; }
        public PermissionScope Scope { get; }

        public static PermissionDecision Allow(PermissionScope scope)
        {
            if (scope == PermissionScope.None || !Enum.IsDefined(typeof(PermissionScope), scope))
                throw new ArgumentOutOfRangeException(nameof(scope), "允许权限必须指定有效的非空访问范围。");
            return new PermissionDecision(PermissionDecisionState.Allowed, scope);
        }

        public bool Equals(PermissionDecision other)
        {
            return other != null && State == other.State && Scope == other.Scope;
        }

        public override bool Equals(object obj) { return Equals(obj as PermissionDecision); }
        public override int GetHashCode() { return ((int)State * 397) ^ (int)Scope; }
    }

    public sealed class PermissionIntentRule
    {
        public PermissionIntentRule(string intentId, IEnumerable<PermissionScope> allowedScopes, bool acceptsDenied, IDictionary<PermissionScope, int> riskWeights)
        {
            IntentId = intentId;
            AllowedScopes = DefinitionCopies.Values(allowedScopes);
            AcceptsDenied = acceptsDenied;
            var copiedWeights = riskWeights == null
                ? new Dictionary<PermissionScope, int>()
                : new Dictionary<PermissionScope, int>(riskWeights);
            RiskWeights = new ReadOnlyDictionary<PermissionScope, int>(copiedWeights);
        }

        public string IntentId { get; }
        public IReadOnlyList<PermissionScope> AllowedScopes { get; }
        public bool AcceptsDenied { get; }
        public IReadOnlyDictionary<PermissionScope, int> RiskWeights { get; }

        internal PermissionIntentRule Copy()
        {
            var copiedWeights = new Dictionary<PermissionScope, int>();
            foreach (var pair in RiskWeights) copiedWeights.Add(pair.Key, pair.Value);
            return new PermissionIntentRule(IntentId, AllowedScopes, AcceptsDenied, copiedWeights);
        }
    }

    public sealed class PermissionDefinition
    {
        public PermissionDefinition(string id, string purpose, PermissionCategory category, int scoreWeight, IEnumerable<PermissionScope> scopes, IEnumerable<PermissionIntentRule> intentRules)
        {
            Id = id;
            Purpose = purpose;
            Category = category;
            ScoreWeight = scoreWeight;
            Scopes = DefinitionCopies.Values(scopes);
            IntentRules = DefinitionCopies.Definitions(intentRules, value => value.Copy());
        }

        public string Id { get; }
        public string Purpose { get; }
        public PermissionCategory Category { get; }
        public int ScoreWeight { get; }
        public IReadOnlyList<PermissionScope> Scopes { get; }
        public IReadOnlyList<PermissionIntentRule> IntentRules { get; }

        internal PermissionDefinition Copy()
        {
            return new PermissionDefinition(Id, Purpose, Category, ScoreWeight, Scopes, IntentRules);
        }
    }
}
