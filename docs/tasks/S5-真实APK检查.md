<!-- 说明：记录真实 APK 静态权限检查实现、验证证据与尚未完成的 Android 真机验收。 -->
# S5 真实 APK 检查

日期：2026-10-30；CC。刘总选择第一项真实能力为“检查陌生 APK”。本轮实现选择实际文件、原生解析、用途核实提示和独立报告。没有接入 AI 换脸鉴伪模型。

## 使用路径

打开 Main.scene → Play → 首页“检查陌生 APK”。编辑器只显示功能说明和平台限制，不提供样例检查结果。Android 构建运行后选择文件、读取实际结果；选择用途后查看解释，点击“保存检查报告”，从首页“检查报告”回看。首页仍提供原有三案，成绩记录仍位于原入口。

## 实际实现

- Android 内部 Activity 通过 ACTION_OPEN_DOCUMENT 让用户主动选择文件；不请求存储、短信、通讯录或安装权限。Activity exported=false。没有网络服务、自动安装或执行所选 APK 的入口。
- 原生后台线程将文件流式复制到应用私有缓存，同时计算 SHA-256；最大256 MiB。检查清单、资源表大小和ZIP条目上限，使用 PackageManager.getPackageArchiveInfo 读取真实声明。仅支持单个完整 APK，不支持 APKS / XAPK 等分包集合；解析失败、取消、超限都不产生成功结果。
- 输出文件名、应用标签、包名、版本、文件大小、最低 / 目标 API、全部可解析的权限声明及当前签名证书 SHA-256。API22/23不能取得最低SDK时显示未提供。证书信息不代表安装级签名完整性验证，也不证明开发者身份。
- 正常、失败和取消路径都尝试删除本次临时副本。删除失败不发布成功。原始用户文件保持原位；进程被系统强杀时 finally 不能保证执行，可能留下私有缓存临时副本，需在后续真机回收策略中核对；不能据源码宣称任何中断下都已删除。
- Unity 通过主线程轮询取得带请求ID的原生结果；只接受当前请求。离页取消、重复点击不并发选择、超时和旧回调隔离。打开系统选择器引起的应用暂停不会直接当成用户取消。
- 用途由用户选择（未确定、照片编辑、聊天通话、其他）。规则逐项解释权限能力和需核实事项；未覆盖的权限原文保留并标为未覆盖。规则不把声明解释成授权，不推断实际窃取行为，不输出病毒判定、安全保证或统计概率。
- 报告冻结事实、用途、解释、UTC检查时间与规则版本。切换用途产生新报告ID，已保存报告保持原解释。页面不接收原始文件URI；外部标签、版本和权限文字按普通文本显示，避免 TMP 富文本注入。
- 报告单独保存在 persistentDataPath/Zhijian/ApkReports/<guid>.json；与训练成绩服务及目录分开。写入临时文件并Flush后以新文件发布，已存在同ID不同内容时拒绝覆盖；保存失败保留本次内存结果并明确未保存。文件格式、大小、ID和权限 / 解释映射均校验；不兼容或损坏的既有报告只跳过并提示，原文件保留。最多展示最近20份，最多扫描最近100个JSON文件；旧报告不会自动删除。
- 复用现有 InvestigationPageView / 行池 / 主题 / 滚动容器，没有增加序列化字段或要求手工重新绑定 Scene / Prefab。

## 实际文件清单

现有文件修改：

1. UnityProject/Assets/_Project/Scripts/Bootstrap/AppRoot.cs：创建检查适配器、报告仓库与页面。
2. UnityProject/Assets/_Project/Scripts/Presentation/Navigation/NavigationRequest.cs：在既有编号后追加三个检查路由。
3. UnityProject/Assets/_Project/Scripts/Presentation/Navigation/CaseAwareNavigator.cs：进入检查工具也处理未结束调查。
4. UnityProject/Assets/_Project/Scripts/Presentation/Home/CaseSelectionPresenter.cs：首页检查与报告入口。
5. UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationPageData.cs：在既有编号后追加三个检查操作。
6. UnityProject/Assets/_Project/Prefabs/Pages/CaseSelection.prefab：首页标题 / 说明适配工具与练习。
7. README.md：更新状态与本记录入口。

新增源码：

- UnityProject/Assets/_Project/Scripts/Application/Inspection/ApkInspectionModels.cs
- UnityProject/Assets/_Project/Scripts/Application/Inspection/ApkInspectionService.cs
- UnityProject/Assets/_Project/Scripts/Infrastructure/Inspection/ApkReportJson.cs
- UnityProject/Assets/_Project/Scripts/Infrastructure/Inspection/JsonApkReportRepository.cs
- UnityProject/Assets/_Project/Scripts/Infrastructure/Inspection/AndroidApkInspector.cs
- UnityProject/Assets/_Project/Scripts/Presentation/Inspection/ApkInspectionPresenter.cs

新增原生库，根目录 UnityProject/Assets/Plugins/Android/ZhijianInspection.androidlib：

- AndroidManifest.xml
- build.gradle
- project.properties
- consumer-rules.pro
- src/com/zhijian/inspection/ApkInspectionActivity.java

新增 Assets 文件 / 目录配套21份 .meta；现有源码和预制体 .meta 保持原值。所有新增、修改 C# 及 Java / Gradle / PowerShell 均有首行简略中文注释。

另新增：tests/apk-inspection/ApkInspectionChecks.cs、tests/apk-inspection/run.ps1、本交付文档、docs/qa/apk-inspection-checks.json。

Domain、原调查用例、原成绩存档实现、案例数据、媒体、Packages、asmdef和ProjectSettings没有改动；118份保护文件哈希保持一致。本轮没有操作Git提交或推送。

## 已执行验证

运行 tests/apk-inspection/run.ps1，在新的系统临时目录生成输出：

- 使用本机编辑器附带 Roslyn / .NET Standard 2.1 参考及 Unity 模块，隔离编译六个运行时C#程序集。
- 另定义 UNITY_ANDROID 编译 Infrastructure，覆盖 Android JNI 分支。
- 使用 Mono 执行16项独立检查，覆盖用途匹配、未知权限、零声明、不接受错误事实、只读事实、重复解释拒绝、改变用途后报告冻结、保存失败反馈、重复点击、离页取消、迟到成功回调和取消选择。这里的原生适配器与报告仓库是用例替身，不代表真实文件系统或 Android API 运行。
- 使用本机 JDK11、SDK android-33/android.jar 编译 Java 源码，Java8语法目标。旧API兼容代码有弃用提示，独立 javac 有 bootstrap class path 选项警告；C# Inspector / JSON 反射赋值字段的 CS0649 在脚本中单独排除。
- XML解析确认插件未声明 uses-permission、内部Activity不导出；全Assets GUID无重复、21份新增meta、118份保护文件哈希检查。

这不是 Unity 导入 / IL2CPP / Gradle合并或APK构建结果；未打开 Unity，未访问正式运行存档，未在 Android 设备上运行。已有 Library 中 UI / TMP 的DLL仅作为编译参考，不作为最新界面运行证据。

## 待真机验收

1. Android Library能被团结引擎识别并参与Gradle打包；检查最终合并清单、Activity和混淆保留规则。
2. 从文件选择器选自有已知APK，对照包名、版本、权限、签名证书摘要；文件SHA-256与电脑计算一致。
3. 同文件在不同用途下得到相应核实解释；未覆盖权限仍显示；界面不输出“病毒 / 已偷取数据 / 已安全”。
4. 取消文件选择、读取中返回、连续点击、旋转 / 后台 / 系统回收后重进不串旧结果；核对私有缓存清理与强杀残留。
5. 空文件、损坏ZIP、APKS / XAPK、单独分包、超大文件、无可读URI、内存 / 存储不足显示真实失败。
6. 保存后退出并重开，在报告入口回看相同事实与解释。保存失败不能显示成功；损坏 / 新格式报告不被覆盖；训练成绩仍可正常查看。
7. 日夜主题、长权限名、长中文、512权限极限下的滚动与内存需要观察；当前通用行高度仍需实际屏幕验收。

## 技术依据与视频检测判断

- [Android 文件选择器](https://developer.android.com/training/data-storage/shared/documents-files)：用户主动选取文件的授权边界。
- [PackageManager](https://developer.android.com/reference/android/content/pm/PackageManager#getPackageArchiveInfo(java.lang.String,%20int)) 与 [PackageInfo](https://developer.android.com/reference/android/content/pm/PackageInfo#requestedPermissions)：声明、授权和证书字段的含义。
- [Deepfake-Eval-2024](https://arxiv.org/abs/2503.02857)：真实传播媒体上检测模型表现低于常用实验数据，不能把原论文成绩直接作为本App准确率。
- [Evaluating Deepfake Detectors in the Wild](https://arxiv.org/abs/2507.21905)：压缩、增强及陌生生成方式会影响检测。该研究主要评估图像检测，不作为本App视频检测成绩。

若后续做AI鉴伪，先限定一种含清晰人脸的短视频场景，定义无法判断的条件，完成权重授权核对与独立数据评估，再接到App。当前没有实现或声称完成此能力。
