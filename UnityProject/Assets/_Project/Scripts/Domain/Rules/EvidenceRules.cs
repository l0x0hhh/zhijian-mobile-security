// 说明：先核对必须查看的资料，再允许任一有效入口取证；重复入口仍由同一 ID 去重。
using System;
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Sessions;

namespace Zhijian.Domain.Rules
{
    public static class EvidenceRules
    {
        public static bool CanCollect(SessionState state, string evidenceId, out string reasonId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var evidence = state.Definition.Evidence.FirstOrDefault(item => item.Id == evidenceId);
            if (evidence == null) { reasonId = "evidence.unknown"; return false; }
            if (!evidence.RequiredSourceIds.All(id => IsAvailable(state, id)))
            { reasonId = "evidence.required-source-unavailable"; return false; }
            var available = evidence.SourceIds.Any(id => IsAvailable(state, id));
            reasonId = available ? "evidence.source-available" : "evidence.source-unavailable";
            return available;
        }
        private static bool IsAvailable(SessionState state, string sourceId)
        {
            var source = state.Definition.Sources.FirstOrDefault(item => item.Id == sourceId);
            if (source == null || !state.ViewedSourceIds.Contains(source.Id)) return false;
            if (source.Kind == SourceKind.Media) return state.PlayedMediaIds.Contains(source.MediaId);
            if (source.Kind == SourceKind.Detection) return state.DetectedMediaIds.Contains(source.MediaId);
            if (source.Kind == SourceKind.Verification) return state.VerificationOptionId != null;
            return true;
        }
    }
}
