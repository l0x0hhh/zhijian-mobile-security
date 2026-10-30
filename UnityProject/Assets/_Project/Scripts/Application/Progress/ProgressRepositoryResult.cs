// 说明：携带仓储加载状态与阻止覆盖的恢复原因，普通写入异常仍由服务保留为待保存状态。
using System;

namespace Zhijian.Application.Progress
{
    public sealed class ProgressRepositoryResult
    {
        public ProgressLoadState State { get; }
        public ProgressArchive Archive { get; }
        public string Message { get; }

        public ProgressRepositoryResult(ProgressLoadState state, ProgressArchive archive, string message)
        {
            if (state != ProgressLoadState.Ready && state != ProgressLoadState.RecoveredFromBackup &&
                state != ProgressLoadState.RecoveryRequired && state != ProgressLoadState.Incompatible &&
                state != ProgressLoadState.Failed)
                throw new ArgumentOutOfRangeException(nameof(state));
            State = state;
            Archive = archive ?? throw new ArgumentNullException(nameof(archive));
            Message = message ?? string.Empty;
        }
    }

    public sealed class ProgressWriteBlockedException : Exception
    {
        public ProgressLoadState LoadState { get; }

        public ProgressWriteBlockedException(ProgressLoadState loadState, string message) : base(message)
        {
            if (loadState != ProgressLoadState.RecoveryRequired && loadState != ProgressLoadState.Incompatible)
                throw new ArgumentOutOfRangeException(nameof(loadState));
            LoadState = loadState;
        }
    }
}
