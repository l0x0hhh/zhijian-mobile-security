// 说明：选关界面专用数据，不将领域类型暴露给 Unity View。
using System.Collections.Generic;

namespace Zhijian.Presentation.Home
{
    public sealed class CaseRowData
    {
        public string Id { get; }
        public string Title { get; }
        public string Summary { get; }
        public string Status { get; }
        public CaseRowData(string id, string title, string summary, string status)
        { Id = id; Title = title; Summary = summary; Status = status; }
    }

    public sealed class CaseSelectionViewData
    {
        public IReadOnlyList<CaseRowData> Cases { get; }
        public CaseSelectionViewData(IReadOnlyList<CaseRowData> cases) { Cases = cases; }
    }
}
