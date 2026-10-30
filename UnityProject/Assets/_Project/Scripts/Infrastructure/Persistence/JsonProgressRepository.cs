// 说明：在调用方指定目录串行读写显式 JSON，以校验临时文件、有效备份及原件归档保护恢复流程。
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zhijian.Application.Ports;
using Zhijian.Application.Progress;

namespace Zhijian.Infrastructure.Persistence
{
    public sealed class JsonProgressRepository : IProgressRepository
    {
        private const string MainName = "progress.json";
        private const string BackupName = "progress.backup.json";
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string directory;
        private readonly string mainPath;
        private readonly string backupPath;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private bool recoveryAllowed;

        public JsonProgressRepository(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("存档必须使用调用方提供的专用绝对目录。", nameof(directory));
            this.directory = Path.GetFullPath(directory);
            mainPath = Path.Combine(this.directory, MainName);
            backupPath = Path.Combine(this.directory, BackupName);
            // 构造和首次加载都不创建目录或空存档。
        }

        public Task<ProgressRepositoryResult> LoadAsync() => InOrderAsync(() =>
        {
            var result = Inspect(Probe(mainPath), Probe(backupPath));
            recoveryAllowed = result.State == ProgressLoadState.RecoveryRequired;
            return result;
        });

        public Task SaveAsync(ProgressArchive archive) => InOrderAsync(() =>
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            var main = Probe(mainPath); var backup = Probe(backupPath);
            var observed = Inspect(main, backup);
            EnsureWritable(observed);
            archive.RequireExtensionOf(observed.Archive);
            Commit(archive, main, backup, false);
            recoveryAllowed = false;
            return true;
        });

        public Task ArchiveAndCreateAsync(ProgressArchive archive) => InOrderAsync(() =>
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            var main = Probe(mainPath); var backup = Probe(backupPath);
            var observed = Inspect(main, backup);
            if (observed.State == ProgressLoadState.Incompatible)
                throw new ProgressWriteBlockedException(observed.State, observed.Message);
            if (observed.State == ProgressLoadState.Failed) throw new IOException(observed.Message);
            if (!recoveryAllowed || observed.State != ProgressLoadState.RecoveryRequired)
                throw new InvalidOperationException("恢复状态已改变；未取得双坏存档确认或原文件已变化，拒绝新建覆盖。");
            // 调用方只能在明确确认后进入此方法；两份原文件均复制并校验后才允许任何替换。
            ArchiveOriginals(main, backup, true);
            Commit(archive, main, backup, true);
            recoveryAllowed = false;
            return true;
        });

        private async Task<T> InOrderAsync<T>(Func<T> operation)
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try { return await Task.Run(operation).ConfigureAwait(false); }
            finally { gate.Release(); }
        }

        private void EnsureWritable(ProgressRepositoryResult observed)
        {
            if (observed.State == ProgressLoadState.RecoveryRequired)
            {
                recoveryAllowed = true;
                throw new ProgressWriteBlockedException(observed.State, observed.Message);
            }
            if (observed.State == ProgressLoadState.Incompatible)
                throw new ProgressWriteBlockedException(observed.State, observed.Message);
            if (observed.State == ProgressLoadState.Failed) throw new IOException(observed.Message);
        }

        private static ProgressRepositoryResult Inspect(FileProbe main, FileProbe backup)
        {
            // 主档或备份中任一较新格式都不得被旧程序覆盖，尤其不能退到有效的旧备份。
            if (main.Kind == ProbeKind.Future || backup.Kind == ProbeKind.Future)
                return Loaded(ProgressLoadState.Incompatible, ProgressArchive.Empty,
                    "发现较新格式存档，已进入只读状态；原文件保持不变，不回退旧备份，也不允许新建覆盖。");
            if (main.Kind == ProbeKind.Valid)
                return Loaded(ProgressLoadState.Ready, main.Archive,
                    backup.Kind == ProbeKind.Corrupt || backup.Kind == ProbeKind.Unreadable
                        ? "已读取主档；备份异常，后续写入前需保留并修复。" : "已读取本地成绩。");
            // 无法读取不是损坏证明，不绕过权限/IO 错误改写原件。
            if (main.Kind == ProbeKind.Unreadable || backup.Kind == ProbeKind.Unreadable)
                return Loaded(ProgressLoadState.Failed, ProgressArchive.Empty,
                    "存档文件暂时无法读取，未覆盖原件；请稍后重新打开应用再读取。");
            if (backup.Kind == ProbeKind.Valid)
                return Loaded(ProgressLoadState.RecoveredFromBackup, backup.Archive,
                    "主档缺失或损坏，已从有效备份读取；原文件未改，下次成功保存时修复主档。");
            if (main.Kind == ProbeKind.Missing && backup.Kind == ProbeKind.Missing)
                return Loaded(ProgressLoadState.Ready, ProgressArchive.Empty, "尚无存档；仅载入空状态，未创建文件。");
            return Loaded(ProgressLoadState.RecoveryRequired, ProgressArchive.Empty,
                "没有有效主档或备份；已保留原件，须明确确认后归档并新建。");
        }

        private static ProgressRepositoryResult Loaded(ProgressLoadState state, ProgressArchive archive, string message)
            => new ProgressRepositoryResult(state, archive, message);

        private static FileProbe Probe(string path)
        {
            try
            {
                return new FileProbe(ProbeKind.Valid, ProgressJsonCodec.Decode(File.ReadAllText(path, Utf8)), string.Empty);
            }
            catch (FileNotFoundException) { return new FileProbe(ProbeKind.Missing, null, string.Empty); }
            catch (DirectoryNotFoundException) { return new FileProbe(ProbeKind.Missing, null, string.Empty); }
            catch (ProgressFutureFormatException error) { return new FileProbe(ProbeKind.Future, null, error.Message); }
            catch (FormatException error) { return new FileProbe(ProbeKind.Corrupt, null, error.Message); }
            catch (DecoderFallbackException error) { return new FileProbe(ProbeKind.Corrupt, null, error.Message); }
            catch (UnauthorizedAccessException error) { return Unreadable(error); }
            catch (IOException error) { return Unreadable(error); }
        }

        private static FileProbe Unreadable(Exception error)
        {
            try { Trace.TraceError("JsonProgressRepository 读取失败: " + error); }
            catch (Exception) { /* 诊断失败不改变只读保护。 */ }
            return new FileProbe(ProbeKind.Unreadable, null, error.Message);
        }

        private void Commit(ProgressArchive archive, FileProbe main, FileProbe backup, bool originalsArchived)
        {
            // 即使主档有效，也不能在备份无法读取时猜测其版本并覆盖。
            if (backup.Kind == ProbeKind.Unreadable) throw new IOException("备份无法读取，未进行替换：" + backup.Message);
            var sameMain = main.Kind == ProbeKind.Valid &&
                ProgressJsonCodec.Encode(main.Archive) == ProgressJsonCodec.Encode(archive);
            if (sameMain && backup.Kind == ProbeKind.Valid) return; // 处理“实际已写入但上次未收到成功”的重试。
            if (!originalsArchived) ArchiveOriginals(main, backup, false);
            Directory.CreateDirectory(directory);

            // 主档有效时先备份旧主档；只有备份有效时绝不把损坏主档写入备份。
            if (main.Kind == ProbeKind.Valid) WriteVerified(backupPath, main.Archive);
            else if (backup.Kind != ProbeKind.Valid) WriteVerified(backupPath, archive);
            var verifiedBackup = Probe(backupPath);
            if (verifiedBackup.Kind != ProbeKind.Valid)
                throw new IOException("备份校验未通过，主档未替换。");
            if (!sameMain) WriteVerified(mainPath, archive);
        }

        private void WriteVerified(string destination, ProgressArchive archive)
        {
            var json = ProgressJsonCodec.Encode(archive);
            var temporary = Path.Combine(directory, "." + Path.GetFileName(destination) + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
            string retired = null;
            try
            {
                var bytes = Utf8.GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                VerifyFile(temporary, json);
                retired = Promote(temporary, destination);
                VerifyFile(destination, json);
                if (retired != null) TryDeleteOwnTemporary(retired);
            }
            finally { TryDeleteOwnTemporary(temporary); }
        }

        private static void VerifyFile(string path, string expectedCanonicalJson)
        {
            var parsed = ProgressJsonCodec.Decode(File.ReadAllText(path, Utf8));
            if (ProgressJsonCodec.Encode(parsed) != expectedCanonicalJson)
                throw new IOException("重新解析的存档与待提交数据不一致。");
        }

        private static string Promote(string temporary, string destination)
        {
            if (!File.Exists(destination))
            {
                File.Move(temporary, destination);
                return null;
            }
            try
            {
                File.Replace(temporary, destination, null);
                return null;
            }
            catch (NotSupportedException)
            {
                // 不支持 Replace 的平台用同目录重命名；主档替换前已经有验证过的备份。
                var retired = destination + ".previous-" + Guid.NewGuid().ToString("N");
                File.Move(destination, retired);
                try { File.Move(temporary, destination); }
                catch (Exception moveError)
                {
                    try { if (!File.Exists(destination)) File.Move(retired, destination); }
                    catch (Exception restoreError)
                    {
                        throw new IOException("安装新存档及恢复原路径均失败；有效备份和前件仍保留。",
                            new AggregateException(moveError, restoreError));
                    }
                    throw;
                }
                return retired;
            }
        }

        private void ArchiveOriginals(FileProbe main, FileProbe backup, bool includeAll)
        {
            var archiveMain = main.Kind == ProbeKind.Corrupt || (includeAll && main.Kind != ProbeKind.Missing);
            var archiveBackup = backup.Kind == ProbeKind.Corrupt || (includeAll && backup.Kind != ProbeKind.Missing);
            if (!archiveMain && !archiveBackup) return;
            var archiveDirectory = Path.Combine(directory, "recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(archiveDirectory);
            if (archiveMain) CopyOriginal(mainPath, Path.Combine(archiveDirectory, MainName));
            if (archiveBackup) CopyOriginal(backupPath, Path.Combine(archiveDirectory, BackupName));
        }

        private static void CopyOriginal(string source, string destination)
        {
            File.Copy(source, destination, false);
            using (var sourceStream = File.OpenRead(source))
            using (var copiedStream = File.OpenRead(destination))
            using (var hasher = SHA256.Create())
            {
                var originalHash = Convert.ToBase64String(hasher.ComputeHash(sourceStream));
                var copiedHash = Convert.ToBase64String(hasher.ComputeHash(copiedStream));
                if (originalHash != copiedHash) throw new IOException("损坏原件归档校验失败，未继续新建。");
            }
        }

        private static void TryDeleteOwnTemporary(string path)
        {
            // 只清理本次调用生成的确切路径；不枚举/清理其他临时文件、归档或用户数据。
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private enum ProbeKind { Missing, Valid, Corrupt, Future, Unreadable }
        private sealed class FileProbe
        {
            public ProbeKind Kind { get; }
            public ProgressArchive Archive { get; }
            public string Message { get; }
            public FileProbe(ProbeKind kind, ProgressArchive archive, string message)
            { Kind = kind; Archive = archive; Message = message; }
        }
    }
}
