// 说明：不可变的情境目录数据，不包含 Unity 对象或本次调查进度。
using System;

namespace Zhijian.Domain.Cases
{
    public enum CaseAvailability { Preview = 0, Available = 1 }

    public sealed class CaseSummary
    {
        public string Id { get; }
        public string Title { get; }
        public string Summary { get; }
        public CaseAvailability Availability { get; }

        public CaseSummary(string id, string title, string summary, CaseAvailability availability)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Case id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Case title is required.", nameof(title));
            if (!Enum.IsDefined(typeof(CaseAvailability), availability)) throw new ArgumentOutOfRangeException(nameof(availability));
            Id = id.Trim();
            Title = title.Trim();
            Summary = summary ?? string.Empty;
            Availability = availability;
        }
    }
}
