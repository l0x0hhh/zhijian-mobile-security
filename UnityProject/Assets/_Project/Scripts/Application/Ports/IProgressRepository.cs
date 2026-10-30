// 说明：隔离存档加载、完整快照提交及用户确认后的损坏存档新建，不暴露 JSON 或文件系统。
using System.Threading.Tasks;
using Zhijian.Application.Progress;

namespace Zhijian.Application.Ports
{
    public interface IProgressRepository
    {
        Task<ProgressRepositoryResult> LoadAsync();
        Task SaveAsync(ProgressArchive archive);
        // 只允许在加载结果为 RecoveryRequired 且调用方已经取得用户确认后调用。
        Task ArchiveAndCreateAsync(ProgressArchive archive);
    }
}
