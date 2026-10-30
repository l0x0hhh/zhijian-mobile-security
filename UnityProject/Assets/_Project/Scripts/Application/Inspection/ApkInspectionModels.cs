// 说明：真实 APK 的只读事实、用途与报告；权限声明不表示授权或实际行为。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Zhijian.Application.Inspection
{
    public enum ApkPurpose { Unspecified, PhotoEditing, Communication, Other }
    public sealed class ApkPackageFacts
    {
        public string FileName { get; }
        public string Label { get; }
        public string PackageName { get; }
        public string VersionName { get; }
        public long VersionCode { get; }
        public int MinimumSdk { get; }
        public int TargetSdk { get; }
        public long FileBytes { get; }
        public string Sha256 { get; }
        public IReadOnlyList<string> Permissions { get; }
        public IReadOnlyList<string> SignerSha256 { get; }
        public ApkPackageFacts(string fileName, string label, string packageName, string versionName,
            long versionCode, int minimumSdk, int targetSdk, long fileBytes, string sha256,
            IEnumerable<string> permissions, IEnumerable<string> signerSha256)
        {
            if (string.IsNullOrWhiteSpace(packageName) || packageName.Length > 512) throw new FormatException("无法读取有效包名。");
            if (fileBytes <= 0 || fileBytes > 268435456 || !IsHash(sha256)) throw new FormatException("文件大小或摘要无效。");
            if (versionCode < 0 || minimumSdk < 0 || targetSdk < 0) throw new FormatException("APK 版本信息无效。");
            var requested = (permissions ?? Array.Empty<string>()).ToArray();
            var signers = (signerSha256 ?? Array.Empty<string>()).ToArray();
            if (requested.Length > 512 || requested.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 512) ||
                signers.Length > 32 || signers.Any(x => !IsHash(x))) throw new FormatException("权限或证书信息超出支持范围。");
            FileName = Limited(fileName, 240); Label = Limited(label, 160); PackageName = packageName;
            VersionName = Limited(versionName, 160); VersionCode = versionCode; MinimumSdk = minimumSdk;
            TargetSdk = targetSdk; FileBytes = fileBytes; Sha256 = sha256.ToLowerInvariant();
            Permissions = Array.AsReadOnly(requested.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
            SignerSha256 = Array.AsReadOnly(signers.Select(x => x.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray());
        }
        private static string Limited(string value, int length) => string.IsNullOrEmpty(value) ? string.Empty : value.Substring(0, Math.Min(value.Length, length));
        private static bool IsHash(string value) => value != null && value.Length == 64 && value.All(x =>
            (x >= '0' && x <= '9') || (x >= 'a' && x <= 'f') || (x >= 'A' && x <= 'F'));
    }
    public sealed class ApkFinding
    {
        public string Permission { get; }
        public string Title { get; }
        public string Explanation { get; }
        public bool NeedsReview { get; }
        public ApkFinding(string permission, string title, string explanation, bool needsReview)
        { Permission = permission; Title = title; Explanation = explanation; NeedsReview = needsReview; }
    }
    public sealed class ApkInspectionReport
    {
        public Guid Id { get; }
        public DateTime CreatedUtc { get; }
        public string RuleVersion { get; }
        public ApkPurpose Purpose { get; }
        public ApkPackageFacts Package { get; }
        public IReadOnlyList<ApkFinding> Findings { get; }
        public ApkInspectionReport(Guid id, DateTime createdUtc, string ruleVersion, ApkPurpose purpose,
            ApkPackageFacts package, IEnumerable<ApkFinding> findings)
        {
            if (id == Guid.Empty || createdUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(ruleVersion) || ruleVersion.Length > 100 ||
                !Enum.IsDefined(typeof(ApkPurpose), purpose)) throw new FormatException("检查报告信息无效。");
            Id = id; CreatedUtc = createdUtc; RuleVersion = ruleVersion; Purpose = purpose;
            Package = package ?? throw new ArgumentNullException(nameof(package));
            var copy = (findings ?? throw new ArgumentNullException(nameof(findings))).ToArray();
            if (copy.Length != package.Permissions.Count || copy.Any(x => x == null || string.IsNullOrWhiteSpace(x.Permission) ||
                !package.Permissions.Contains(x.Permission) || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 200 ||
                string.IsNullOrWhiteSpace(x.Explanation) || x.Explanation.Length > 2000) ||
                copy.Select(x => x.Permission).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new FormatException("检查条目与权限事实不匹配。");
            Findings = Array.AsReadOnly(copy);
        }
    }
    public sealed class ApkReportHistory
    {
        public IReadOnlyList<ApkInspectionReport> Reports { get; }
        public string Warning { get; }
        public ApkReportHistory(IEnumerable<ApkInspectionReport> reports, string warning = null)
        { Reports = Array.AsReadOnly(reports.ToArray()); Warning = warning; }
    }
    public interface IApkInspector
    {
        bool IsSupported { get; }
        Task<ApkPackageFacts> SelectAndInspectAsync(CancellationToken cancellation);
    }
    public interface IApkReportRepository
    {
        void Save(ApkInspectionReport report);
        ApkReportHistory LoadRecent();
    }
}
