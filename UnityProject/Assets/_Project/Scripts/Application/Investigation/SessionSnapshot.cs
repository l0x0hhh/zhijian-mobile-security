// 说明：返回事务结束后的只读快照与同一套评分，完整定义只供可信 Presenter 转换。
using Zhijian.Domain.Results;
using Zhijian.Domain.Rules;
using Zhijian.Domain.Sessions;

namespace Zhijian.Application.Investigation
{
    public sealed class SessionSnapshot
    {
        public SessionState State { get; }
        public ScoreBreakdown Score { get; }
        internal SessionSnapshot(SessionState state)
        { State = state; Score = state.Result == null ? ScoringRules.Calculate(state) : state.Result.Score; }
    }
}
