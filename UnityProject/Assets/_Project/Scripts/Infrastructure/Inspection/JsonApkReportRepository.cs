// 说明：检查报告独立存储，原子新增并保留既有文件；损坏或不兼容报告只提示跳过。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Zhijian.Application.Inspection;

namespace Zhijian.Infrastructure.Inspection
{
    public sealed class JsonApkReportRepository : IApkReportRepository
    {
        private const int MaximumBytes = 524288;
        private readonly string directory;
        public JsonApkReportRepository(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory)) throw new ArgumentException("报告目录必须为绝对路径。", nameof(directory));
            this.directory = Path.GetFullPath(directory);
        }
        public void Save(ApkInspectionReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var bytes = new UTF8Encoding(false).GetBytes(ApkReportJson.Encode(report));
            if (bytes.Length > MaximumBytes) throw new IOException("报告内容过大，尚未保存。");
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, report.Id.ToString("D") + ".json");
            if (File.Exists(target))
            {
                if (new FileInfo(target).Length <= MaximumBytes && File.ReadAllBytes(target).SequenceEqual(bytes)) return;
                throw new IOException("同编号报告已存在且内容不同，原文件已保留。");
            }
            var temporary = Path.Combine(directory, report.Id.ToString("D") + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public ApkReportHistory LoadRecent()
        {
            var reports = new List<ApkInspectionReport>();
            var skipped = 0;
            if (!Directory.Exists(directory)) return new ApkReportHistory(reports);
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*.json").OrderByDescending(x => x.LastWriteTimeUtc).Take(100))
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), "D", out var fileId)) { skipped++; continue; }
                try
                {
                    if (file.Length > MaximumBytes) throw new FormatException();
                    var report = ApkReportJson.Decode(File.ReadAllText(file.FullName, Encoding.UTF8));
                    if (report.Id != fileId) throw new FormatException();
                    reports.Add(report);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is FormatException)
                { skipped++; }
            }
            return new ApkReportHistory(reports.OrderByDescending(x => x.CreatedUtc).Take(20),
                skipped == 0 ? null : "部分报告损坏、无法读取或版本不兼容，原文件已保留。");
        }
    }
}
