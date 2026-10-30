// 说明：通过只读加载结果访问完整案件，资源与发布状态由外部适配器提供。
using System.Collections.Generic;
using Zhijian.Application.Cases;

namespace Zhijian.Application.Ports
{
    public interface IInvestigationCaseRepository
    {
        IReadOnlyList<string> CaseIds { get; }
        bool TryGetCase(string caseId, out CaseLoadResult result);
    }
}
