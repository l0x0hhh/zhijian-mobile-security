// 说明：语音借款任意调查阶段立即转账均触发诈骗失败。
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Sessions;

namespace Zhijian.Domain.Outcomes
{
    public sealed class VoiceLoanOutcomePolicy : ICaseOutcomePolicy
    {
        public string PolicyId => "voice-loan";
        public CaseKind CaseKind => Zhijian.Domain.Cases.CaseKind.VoiceLoan;

        public OutcomeDecision EvaluateImmediate(SessionState state)
        {
            var disposition = state.Definition.DispositionOptions.FirstOrDefault(item => item.Id == state.DispositionOptionId);
            if (disposition == null || (disposition.Kind != DispositionKind.TransferImmediately && !disposition.IsImmediateFailure)) return null;
            return new OutcomeDecision(OutcomeKind.ScamFailure, "voice.immediate-transfer", disposition.Id,
                "未经安全处置即转账，已进入诈骗结局；已有正确分项仍保留。",
                "停止转账，通过原有联系方式联系本人，并保存对话与收款证据。");
        }

        public OutcomeDecision EvaluateFinal(SessionState state)
        {
            return EvaluateImmediate(state) ?? OutcomeResolver.EvaluateCommonFinal(state);
        }
    }
}
