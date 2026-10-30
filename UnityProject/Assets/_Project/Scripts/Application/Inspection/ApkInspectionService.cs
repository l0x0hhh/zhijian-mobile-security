// 说明：按 APK 真实权限声明解释能力，结合用户用途提示核实；不输出病毒结论或伪造风险概率。
using System;
using System.Collections.Generic;

namespace Zhijian.Application.Inspection
{
    public static class ApkInspectionService
    {
        public const string RuleVersion = "apk-declarations-1";
        private sealed class Rule
        {
            public readonly string Title, Explanation;
            public readonly bool RelevantToPhoto, RelevantToCommunication;
            public Rule(string title, string explanation, bool photo = false, bool communication = false)
            { Title = title; Explanation = explanation; RelevantToPhoto = photo; RelevantToCommunication = communication; }
        }
        private static readonly Dictionary<string, Rule> Rules = new Dictionary<string, Rule>(StringComparer.Ordinal)
        {
            ["android.permission.READ_SMS"] = new Rule("读取短信", "可能涉及短信正文和验证码，应核实具体功能与处理方式。"),
            ["android.permission.RECEIVE_SMS"] = new Rule("接收短信", "可能接收新短信内容，应核实必要性。"),
            ["android.permission.SEND_SMS"] = new Rule("发送短信", "可能发送短信并产生费用，应核实必要性。"),
            ["android.permission.READ_CONTACTS"] = new Rule("读取通讯录", "可能读取联系人信息，需要明确使用目的。", communication: true),
            ["android.permission.WRITE_CONTACTS"] = new Rule("修改通讯录", "可能新增或修改联系人，需要明确使用目的。", communication: true),
            ["android.permission.READ_CALL_LOG"] = new Rule("读取通话记录", "可能读取通话对象和记录，应核实必要性。"),
            ["android.permission.CALL_PHONE"] = new Rule("拨打电话", "可能直接发起电话并产生费用，应核实必要性。", communication: true),
            ["android.permission.RECORD_AUDIO"] = new Rule("麦克风", "可用于录音或语音通话，仍需相应授权。", communication: true),
            ["android.permission.CAMERA"] = new Rule("摄像头", "可用于拍摄或视频通话，仍需相应授权。", photo: true, communication: true),
            ["android.permission.ACCESS_FINE_LOCATION"] = new Rule("精确位置", "可能访问精确位置信息，应核实功能是否需要。"),
            ["android.permission.ACCESS_COARSE_LOCATION"] = new Rule("大致位置", "可能访问大致位置，应核实功能是否需要。"),
            ["android.permission.ACCESS_BACKGROUND_LOCATION"] = new Rule("后台位置", "涉及非前台使用场景的位置访问，需要单独核实。"),
            ["android.permission.READ_MEDIA_IMAGES"] = new Rule("读取图片", "涉及共享图片访问；单张处理可考虑系统照片选择器。", photo: true, communication: true),
            ["android.permission.READ_MEDIA_VIDEO"] = new Rule("读取视频", "涉及共享视频访问，应核实是否只需要用户选定的视频。", communication: true),
            ["android.permission.READ_EXTERNAL_STORAGE"] = new Rule("读取共享存储", "实际范围取决于 Android 版本和目标 API，可核实是否能用文件选择器。", photo: true, communication: true),
            ["android.permission.WRITE_EXTERNAL_STORAGE"] = new Rule("写入共享存储", "实际效果取决于 Android 版本，不能据此断言可以访问全部文件。", photo: true, communication: true),
            ["android.permission.MANAGE_EXTERNAL_STORAGE"] = new Rule("所有文件访问声明", "属于特殊访问，声明并不代表已获得；需要核实是否确有广泛文件管理需求。"),
            ["android.permission.REQUEST_INSTALL_PACKAGES"] = new Rule("请求安装其他应用", "声明安装请求能力，并不表示可以静默安装；应核实具体用途。"),
            ["android.permission.SYSTEM_ALERT_WINDOW"] = new Rule("悬浮窗声明", "属于特殊访问，可能在其他应用上显示内容；需要单独核实。"),
            ["android.permission.INTERNET"] = new Rule("网络访问", "可用于网络连接；此声明不能证明应用正在上传数据。", photo: true, communication: true),
            ["android.permission.QUERY_ALL_PACKAGES"] = new Rule("广泛应用列表查询", "可能涉及已安装应用信息，需要核实是否与核心功能相关。")
        };
        public static ApkInspectionReport Analyze(ApkPackageFacts facts, ApkPurpose purpose)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            if (!Enum.IsDefined(typeof(ApkPurpose), purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
            var findings = new List<ApkFinding>();
            foreach (var permission in facts.Permissions)
            {
                if (!Rules.TryGetValue(permission, out var rule))
                {
                    findings.Add(new ApkFinding(permission, "其他权限声明", "当前规则未覆盖此项，不能据此判定安全；可向开发者核实。", false));
                    continue;
                }
                var needsReview = purpose == ApkPurpose.PhotoEditing ? !rule.RelevantToPhoto :
                    purpose == ApkPurpose.Communication ? !rule.RelevantToCommunication :
                    !rule.RelevantToPhoto && !rule.RelevantToCommunication;
                var context = needsReview ? " 需结合实际用途进一步核实必要性。" :
                    " 即使与用途可能相关，也应按需授权。";
                findings.Add(new ApkFinding(permission, rule.Title, rule.Explanation + context, needsReview));
            }
            return new ApkInspectionReport(Guid.NewGuid(), DateTime.UtcNow, RuleVersion, purpose, facts, findings);
        }
    }
}
