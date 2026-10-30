// 说明：显式保存真实检查事实与冻结解释，校验格式；不保存用户原始 APK、文件 URI 或训练成绩。
using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zhijian.Application.Inspection;

namespace Zhijian.Infrastructure.Inspection
{
    [Serializable] internal sealed class ApkFactsDto
    {
        public string fileName, label, packageName, versionName, sha256;
        public long versionCode, fileBytes;
        public int minimumSdk, targetSdk;
        public string[] permissions, signerSha256;
        public ApkPackageFacts ToFacts()
        {
            if (permissions == null || signerSha256 == null) throw new FormatException("权限或证书字段缺失，不能视为未声明。");
            return new ApkPackageFacts(fileName, label, packageName, versionName,
                versionCode, minimumSdk, targetSdk, fileBytes, sha256, permissions, signerSha256);
        }
        public static ApkFactsDto From(ApkPackageFacts f) => new ApkFactsDto
        { fileName = f.FileName, label = f.Label, packageName = f.PackageName, versionName = f.VersionName,
            versionCode = f.VersionCode, minimumSdk = f.MinimumSdk, targetSdk = f.TargetSdk,
            fileBytes = f.FileBytes, sha256 = f.Sha256, permissions = f.Permissions.ToArray(), signerSha256 = f.SignerSha256.ToArray() };
    }
    [Serializable] internal sealed class ApkFindingDto
    { public string permission, title, explanation; public bool needsReview; }
    [Serializable] internal sealed class ApkReportDto
    {
        public int schemaVersion = 1;
        public string formatId = "zhijian.apk-report", id, createdUtc, ruleVersion;
        public int purpose;
        public ApkFactsDto package;
        public ApkFindingDto[] findings;
    }
    internal static class ApkReportJson
    {
        public static string Encode(ApkInspectionReport r) => JsonUtility.ToJson(new ApkReportDto
        {
            id = r.Id.ToString("D"), createdUtc = r.CreatedUtc.ToString("O", CultureInfo.InvariantCulture),
            ruleVersion = r.RuleVersion, purpose = (int)r.Purpose, package = ApkFactsDto.From(r.Package),
            findings = r.Findings.Select(x => new ApkFindingDto { permission = x.Permission, title = x.Title,
                explanation = x.Explanation, needsReview = x.NeedsReview }).ToArray()
        }, true);
        public static ApkInspectionReport Decode(string json)
        {
            var dto = JsonUtility.FromJson<ApkReportDto>(json);
            if (dto == null || dto.schemaVersion != 1 || dto.formatId != "zhijian.apk-report" ||
                dto.package == null || dto.findings == null || dto.findings.Length > 512 ||
                !Guid.TryParseExact(dto.id, "D", out var id) ||
                !DateTime.TryParseExact(dto.createdUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc) ||
                utc.Kind != DateTimeKind.Utc) throw new FormatException("报告格式损坏或当前版本不支持。");
            if (dto.findings.Any(x => x == null || string.IsNullOrEmpty(x.permission) || x.permission.Length > 512 ||
                string.IsNullOrEmpty(x.title) || x.title.Length > 200 || string.IsNullOrEmpty(x.explanation) || x.explanation.Length > 2000))
                throw new FormatException("报告条目无效。");
            return new ApkInspectionReport(id, utc, dto.ruleVersion, (ApkPurpose)dto.purpose, dto.package.ToFacts(),
                dto.findings.Select(x => new ApkFinding(x.permission, x.title, x.explanation, x.needsReview)));
        }
    }
}
