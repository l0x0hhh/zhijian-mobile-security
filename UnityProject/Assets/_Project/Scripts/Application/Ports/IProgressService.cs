// 说明：固定页面与存档服务之间的只读状态及异步命令，具体文件读写由存档窗口实现。
using System;
using System.Threading.Tasks;
using Zhijian.Application.Progress;
using Zhijian.Domain.Results;

namespace Zhijian.Application.Ports
{
    public interface IProgressService : IDisposable
    {
        ProgressSnapshot Current { get; }
        event Action<ProgressSnapshot> Changed;
        Task InitializeAsync();
        Task<ProgressWriteResult> RecordAsync(CaseResult result);
        Task RetryPendingAsync();
        Task StartNewSaveAsync();
    }
}
