// 说明：区分应用分发拒绝与领域操作结果，不用异常代替普通缺项反馈。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Application.Cases;
using Zhijian.Domain.Sessions;

namespace Zhijian.Application.Investigation
{
    public enum DispatchStatus { Completed, Started, ContentBlocked, UnknownCase, ActiveSession, NoSession, StaleSession, Busy, InvalidCommand, Disposed }
    public sealed class CommandResult
    {
        public DispatchStatus Status { get; }
        public SessionCommandResult DomainResult { get; }
        public SessionSnapshot Snapshot { get; }
        public IReadOnlyList<ContentProblem> Problems { get; }
        public bool ProducedResult { get; }
        internal CommandResult(DispatchStatus status, SessionSnapshot snapshot, SessionCommandResult domainResult = null,
            IEnumerable<ContentProblem> problems = null, bool producedResult = false)
        {
            Status = status; Snapshot = snapshot; DomainResult = domainResult; ProducedResult = producedResult;
            Problems = Array.AsReadOnly((problems ?? Enumerable.Empty<ContentProblem>()).ToArray());
        }
    }
}
