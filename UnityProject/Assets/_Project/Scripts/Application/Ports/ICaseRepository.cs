// 说明：应用层通过此接口读取只读情境目录，隔离内容资产来源。
using System.Collections.Generic;
using Zhijian.Domain.Cases;

namespace Zhijian.Application.Ports
{
    public interface ICaseRepository
    {
        IReadOnlyList<CaseSummary> GetSummaries();
    }
}
