<!-- 说明：窗口 4 只读核对 Android 环境事实，区分版本、文档依据和未执行的构建验证。 -->
# Android 环境核对

核对日期：2026-10-19（Asia/Shanghai）；执行者：CC；窗口：4。
仓库：`E:\unity_project\zhijian-mobile-security`；工程：其下 `UnityProject`。
本轮仅新增 QA 与 T4 文档。已阅读根 `AGENTS.md`、并行任务表、代码架构设计、需求与验收、开发计划，并补读 P1 实现记录、UI 美术规范和相关源码/资产。

## 1. 结论与证据等级

| 维度 | 本轮结论 | 证据边界 |
| --- | --- | --- |
| 文件存在 | 编辑器、AndroidPlayer、SDK、NDK、OpenJDK、Gradle 核心目录及下表关键文件存在 | 仅代表检查时能找到/读取；不是完整安装校验 |
| 版本信息可读取 | 编辑器版本资源、source.properties、JDK release、Gradle JAR 内版本及五项版本查询可读 | 不是编译、依赖解析或 APK 运行结果 |
| 兼容性文档依据 | 官方团结 1.6、Unity 2022.3、Android AGP 7.4 文档提供分项依据，详见第 4 节 | Unity 同代参考不冒充团结 t10 精确组合认证 |
| 实际构建通过 | **未验证** | 未启动编辑器，未运行 Gradle、batchmode、编译或测试，未生成本轮 APK |
| 真机安装与功能验收 | **未执行** | 未连接/查询设备，未安装应用、操作真机或采集截图 |

未安装或下载组件，未改 PATH、注册表、工程配置、包清单或其他窗口文件。没有调用 sdkmanager（包括许可证接受操作）；未执行 Gradle 的任何命令，连 --version 也未执行。

## 2. 本机路径和版本事实

以下缩写仅用于缩短表格，拼接后即完整路径：

- `EDITOR = E:\unity\2022.3.61t10\Editor`
- `ANDROID = E:\unity\2022.3.61t10\Editor\Data\PlaybackEngines\AndroidPlayer`

“依据 Dn”对应第 4 节已阅读的官方文档。所有“构建”列独立保留为未验证。

| 组件/文件（相对上述根） | 文件存在 | 版本信息可读取/读取方法 | 兼容性文档依据 | 实际构建通过 |
| --- | --- | --- | --- | --- |
| `EDITOR\Tuanjie.exe` | 是，91,183,440 字节 | PE FileVersion `2022.3.61.3946985`；ProductVersion `2022.3.61t10_3c39e943eae5`；ProductName Tuanjie | 本机版本与 ProjectVersion 一致；不是官方兼容认证 | 未验证 |
| `ANDROID`（AndroidPlayer） | 是；含 SDK、NDK、OpenJDK、Tools、Variations、AndroidPlayerBuildProgram.exe、UnityEditor.Android.Extensions.dll | 该目录无独立统一版本；关联编辑器版本，不编造模块版本号 | D1 要求 Android Build Support | 未验证 |
| `ANDROID\SDK` | 是 | SDK 是多组件集合，无单一“SDK 版本”，分项如下 | D1、D2 | 未验证 |
| `SDK\tools\source.properties` | 是 | Pkg.Revision `26.1.1` | 历史 tools 事实；不据此选择/替换工具 | 未验证 |
| `SDK\cmdline-tools\6.0\source.properties`、`bin\sdkmanager.bat` | 是 | Pkg.Revision `6.0`；只读元数据，未运行 sdkmanager | D2 的 2022.3 表列 6 | 未验证 |
| `SDK\platform-tools\source.properties`、`adb.exe` | 是 | Pkg.Revision `32.0.0`；adb version 返回 `1.0.41 / 32.0.0-8006631` | D2 的 2022.3 表列 32.0.0 | 未验证 |
| `SDK\build-tools\34.0.0\source.properties` | 是 | Pkg.Revision `34.0.0`；同目录 aapt2.exe、apksigner.bat、zipalign.exe 存在 | D2 的 2022.3 表列 34.0.0；D4 提供 AGP 分项最低版本参考 | 未验证 |
| `SDK\build-tools\34.0.0\aapt2.exe` | 是 | version 返回 `Android Asset Packaging Tool (aapt) 2.19-10229193`；这是 aapt2 自身版本 | 不把 aapt2 版本当作整个 SDK 版本 | 未验证 |
| `SDK\platforms\android-33` | 是，含 android.jar、source.properties | API 33；Platform.Version 13；Pkg.Revision 2 | 平台已安装；不等于本项目最终选择 API 33 | 未验证 |
| `SDK\platforms\android-34` | 是，含 android.jar、source.properties | API 34；Platform.Version 14；Pkg.Revision 2 | 平台已安装；Target/Compile API 完整兼容待确认 | 未验证 |
| `SDK\platforms\android-35` | 是，含 android.jar、source.properties | API 35；Platform.Version 15；Pkg.Revision 1 | 平台已安装；不能据此宣称当前 AGP/工程支持 API 35 | 未验证 |
| `ANDROID\NDK\source.properties` | 是，含 ndk-build.cmd、toolchains | Pkg.Revision `23.1.7779620`（r23b） | D2 的 2022.3 行；D4 默认 NDK 同值 | 未验证 |
| `NDK\toolchains\llvm\prebuilt\windows-x86_64\bin\clang.exe` | 是 | --version 返回 clang `12.0.8`，Android build `7714059`，基于 r416183c1 | NDK 子工具版本；未调用编译器编译 | 未验证 |
| `ANDROID\OpenJDK\release` | 是；bin/java.exe、bin/javac.exe、bin/server/jvm.dll 均存在 | Temurin `11.0.14.1+1`；JAVA_VERSION `11.0.14.1`；Windows x86_64；java/javac -version 一致 | D1、D2 的 JDK 11；D4 最低 JDK 11 | 未验证 |
| `ANDROID\Tools\gradle\lib\gradle-launcher-7.5.1.jar` | 是；Gradle 根目录内有 lib | 只读 `gradle-base-services-7.5.1.jar!org/gradle/build-receipt.properties`：versionNumber/baseVersion `7.5.1`，isSnapshot=false | D3 的 Unity 2022.3.38f1+ 行；D4 最低 Gradle 7.5 | 未验证 |
| `Tools\GradleTemplates\baseProjectTemplate.gradle` | 是 | application/library 两项 plugin 声明 `7.4.2` | D3、D4；这里只证明模板声明，未解析/下载 AGP 实体依赖 | 未验证 |
| `EDITOR\Data\il2cpp`、`ANDROID\Variations` | 是 | 仅目录存在，未检查所有目标 ABI 变体或执行 IL2CPP | 供后续后端/ABI 决策核查，不能代表 IL2CPP 可构建 | 未验证 |

表内省略 ANDROID 前缀的 SDK、NDK、OpenJDK、Tools 路径均从 ANDROID 起算。
Gradle JAR 内 buildTimestamp 为 `20220805211756+0000`，commitId 为 `d1daa0cbf1a0103000b71484e1dbfe096e095918`；读取 ZIP 条目没有启动 Java/Gradle。

### 明确缺项与仍未知事项

| 项目 | 当前事实 | 后续处理边界 |
| --- | --- | --- |
| SDK 许可证目录 | `ANDROID\SDK\licenses` 未发现 | 许可证接受/可用状态未验证；不能仅据目录不存在断言构建必失败。本轮不接受许可证、不运行 sdkmanager |
| Gradle 命令脚本 | `ANDROID\Tools\gradle\bin\gradle.bat` 不存在；lib 和 launcher JAR 存在 | 不把编辑器随附的 lib 形式直接判为组件损坏；不另装 Gradle 补此脚本 |
| External Tools 实际选中路径 | 未打开编辑器；未读取或修改注册表偏好 | 磁盘路径存在不证明编辑器已使用它们，待后续界面截图核对 |
| Target API/Compile API | 项目目标值为 0（自动）；尚无本轮生成的 Gradle 工程 | 不按最高已装 API 35 推定构建兼容，不替用户锁定目标 API |
| Gradle/Maven 依赖及离线缓存 | 未做依赖解析，未检查完整缓存与 AGP 下载状态 | 不能宣称首次打包可离线完成；应用离线可用与构建依赖获取分开 |
| 设备与 ABI | 未提供实际型号、Android/API、支持 ABI、分辨率、安全区 | 架构/最低版本和真机覆盖待确认，详见 T4 设备清单 |
| 编辑器许可、导入与资源处理 | 未启动编辑器 | 激活、导入、TMP 图集生成、插件处理均未验证 |

## 3. 工程静态快照

来源为 `UnityProject/ProjectSettings/`、`Packages/` 以及 P1 相关资产；这是 2026-10-19 读取时的快照，共享目录可能由其他窗口继续开发。

| 项目 | 读取事实 | 解释/尚未验证 |
| --- | --- | --- |
| ProjectVersion.txt | m_EditorVersion `2022.3.61t10`；revision `3c39e943eae5`；m_TuanjieEditorVersion `1.6.9` | 与本机 Tuanjie 产品版本/修订一致，未由本窗口改写 |
| 包清单与锁 | 两个 JSON 均可解析；41 个直接依赖、55 个锁条目；41 项直接版本与锁中版本一致 | 只核对静态一致性；包解析、下载、导入与运行兼容未验证 |
| 主要包 | uGUI `1.0.0`、TMP `3.0.9`、2D feature `2.0.1`、Timeline `1.7.7`、Visual Scripting `1.9.4`、Test Framework `1.1.33`、Codely Bridge `1.0.87-exp.2` | 测试包存在不等于测试已执行；不建议为 QA 随意换版本 |
| 包源 | 锁文件的 registry URL 为 `https://packages.tuanjie.cn`；另有 builtin 条目 | 未访问/验证注册表、未触发包更新 |
| 标识 | companyName Zhijian；productName 智鉴；Android 包名 `com.zhijian.mobile.security`；bundleVersion 1.0；AndroidBundleVersionCode 1 | 最终安装包中的实际标识仍待读取 |
| 屏幕 | defaultScreenOrientation 0（竖屏，需界面确认）；预览宽高 1080×1920 | defaultScreenWidth/Height 不等于 Android 真机尺寸 |
| 安全区 | androidStartInFullscreen=1；androidRenderOutsideSafeArea=1 | 可绘制到安全区外，不证明可操作内容已避开刘海 |
| UI 适配 | PhoneShell 的 Canvas Scaler：m_UiScaleMode=1；参考 1080×1920；m_ScreenMatchMode=0；m_MatchWidthOrHeight=0 | SafeAreaAdapter 读取 Screen.safeArea 并更新锚点；真实 Insets/布局未执行 |
| SDK 级别 | AndroidMinSdkVersion=22；AndroidTargetSdkVersion=0 | 最低 API 22 和自动目标是当前记录；最终选择须结合设备与工具链明确 |
| 架构 | AndroidTargetArchitectures=1 | 按同代枚举通常为 ARMv7；本轮保留原始值，待编辑器显示确认，不能宣称覆盖 ARM64-only 设备 |
| 脚本后端/API 兼容 | scriptingBackend={}；apiCompatibilityLevelPerPlatform={}；全局 apiCompatibilityLevel=6 | 没有 Android 显式覆盖；不据此认定 Mono 或 IL2CPP 最终生效值 |
| 图形 API | AndroidPlayer m_Automatic=1；m_APIs 原值 `150000000b000000` | 未改 Vulkan/OpenGLES 顺序，驱动兼容须真机检查 |
| 网络/签名/输入 | ForceInternetPermission=0；androidUseCustomKeystore=0；签名路径空；activeInputHandler=0；androidPredictiveBackSupport=0 | 不强制 Internet 不代表最终无网络权限；未生成/查找签名密钥；手势返回需实测 |
| 场景 | EditorBuildSettings：Main.scene 启用且排首；SampleScene.scene 保留但禁用 | 未检查编辑器当前 Active Build Target；未切换平台 |
| Android 插件文件 | `Assets/Plugins` 目录未发现；Assets 全树未发现 Manifest、gradle、properties、aar、jar、androidlib 匹配项 | 搜索范围仅 Assets，不能覆盖包生成行为/最终合并清单 |
| 敏感 API 静态线索 | `Assets/_Project` 定向检索系统权限、WebCam、Microphone、AndroidJava、外链、网络/媒体/持久化 API 关键字，未命中所查模式 | 不是完整安全审计；包、动态调用和生成清单仍须后续核对 |
| P1 运行边界 | AppRoot 仍组装目录/导航/说明页；三案 ID 为 voice-loan、face-meeting、photo-permission | 即使并行窗口已新增 Domain 文件，也不等于调查 UI 已接入 |
| 中文字体 | NotoSansSC-Regular.otf、OFL.txt、SOURCE.md 存在；ThemeApplier 创建动态 TMP 字体 | 中文字形在运行时生成；未播放预制体默认拉丁字体不能作为最终验收结果 |

`docs/需求与验收.md` 的“尚未实现 Unity 功能”是旧的整体状态描述；P1 实现记录和当前入口源码已描述预览骨架。本窗口不改公共文档，验收清单按 P1 预览/未来业务拆分，不将旧状态文字或新增纯领域代码视为业务集成证据。

## 4. 官方兼容性依据与使用范围

访问日期均为 2026-10-19。只使用官方资料；不以论坛、博客、第三方教程判断版本。

| 编号 | 已阅读的官方链接 | 本次采用的依据及限制 |
| --- | --- | --- |
| D1 | [团结 1.6：Android 环境设置](https://docs.unity.cn/cn/tuanjiemanual/1.6/Manual/android-sdksetup.html) | 模块、SDK/NDK/JDK 目录、External Tools 入口与随附依赖原则；页面不是 t10 完整版本矩阵，NDK 表也未单列本机 t10 |
| D2 | [Unity 2022.3：支持的依赖版本](https://docs.unity3d.com/2022.3/Documentation/Manual/android-supported-dependency-versions.html) | 2022.3 行为 Build Tools 34.0.0、Command-line Tools 6、Platform Tools 32.0.0、NDK r23b、JDK 11；本机分项与此同代参考一致 |
| D3 | [Unity 2022.3：Gradle for Android](https://docs.unity3d.com/2022.3/Documentation/Manual/android-gradle-overview.html) | 英文版本表明确 2022.3.38f1+ 使用 Gradle 7.5.1/AGP 7.4.2；与本机 JAR/模板一致，但不把 f1 补丁边界等同于团结 t10 版本认证 |
| D4 | [Android Developers：AGP 7.4 兼容表](https://developer.android.com/build/releases/agp-7-4-0-release-notes) | 最低 Gradle 7.5/JDK 11，默认 NDK 23.1.7779620；仅支持这些分项要求判断，不证明 API 34/35、所有插件或本工程成功 |
| D5 | [Unity 2022.3：Android External Tools](https://docs.unity3d.com/2022.3/Documentation/Manual/android-external-tools-reference.html) | JDK、SDK、NDK、Gradle 的随附/自定义路径选择方式；实际偏好尚未查看 |
| D6 | [团结 1.6：Android Player 设置](https://docs.unity.cn/cn/tuanjiemanual/1.6/Manual/class-PlayerSettingsAndroid.html) | 后续核对 Android Player 页面、方向、API、架构及安全区等选项；界面标签以本机 1.6.9 显示为准 |

部分官方本地化旧页面仍概括 2022.3 为 Gradle 7.2/AGP 7.1.2；本报告保留英文补丁分段与本机事实，不据旧概括降级或替换现有配套工具。未获得“团结 2022.3.61t10 + 本工程所有包 + 指定设备”的完整兼容性证明。

## 5. 后续编辑器核对/设置步骤（全部未执行）

以下是交给主窗口/刘总的后续操作清单，不是本轮授权执行内容。启动、平台切换、设置和打包由后续明确安排执行；涉及配置差异交主窗口处理。

1. 用已核对的 Tuanjie.exe 打开唯一 `UnityProject`；记录 About 的 1.6.9 / 2022.3.61t10、导入结果与 Console 真实错误，不把文档快照当导入成功。
2. 到 Edit → Preferences → External Tools → Android，逐项截图随附工具选项和路径。期望配套根分别为 `ANDROID\OpenJDK`、`ANDROID\SDK`、`ANDROID\NDK`、`ANDROID\Tools\gradle`；核对当前实际值，不另找系统 Java/Gradle 替代。若选错，列差异交主窗口按授权修正。
3. 在 Build Settings 核对 Android 平台及模块、Main.scene 为唯一启用的预期入口。切换平台会导入/编译，应留到获准的后续运行批次。
4. 到 Player → Android 核对包名、版本、竖屏、全屏和 Render outside safe area。保留背景延展时，检查 PhoneShell 的可操作内容确实受安全区约束；不要为了“通过”本清单自行更改配置。
5. 结合验收机实际 Android/API、支持 ABI 与交付用途，记录 Minimum/Target API、Scripting Backend、Target Architectures 的明确决定；先核对现有 SDK 平台和官方依据。当前自动目标与原始架构值不能代替决定，也不能直接指定 API 35 或要求升级工具链。
6. 核对中文源字体 Include Font Data、Theme 引用和动态 TMP 图集；在播放后的视图检查缺字、首次绘制、长文本和深浅主题。按人工清单检查 320/360/412 参考宽度，再用真实设备验证。
7. 确认基础版仅使用预置媒体与模拟权限/安装，排查插件自动加入的权限与联网组件。最终清单必须来自后续真实构建的 merged AndroidManifest/APK，不能只检查 ForceInternetPermission 或手写清单。
8. 后续打包若出现许可证、缺依赖、目标 API 或版本不支持提示，保存原始信息交主窗口处理；不默认接受下载、升级或替换。不要把本轮只读检查写成已完成这些处理。
9. 另行安排 APK 构建、签名、安装及人工验收；记录工程修订、配置、APK SHA-256、设备与日志，再更新真实结果。签名口令/密钥不进入 QA 文档或 Git。

## 6. 本轮可复核方法

PowerShell 的 Test-Path/Get-Item/Get-Content/只读目录枚举与 rg 搜索；PE VersionInfo；两个包 JSON 静态解析和直接版本比对；ZipFile.OpenRead 读取 Gradle build-receipt；仅以下版本查询成功返回：

```text
ANDROID\OpenJDK\bin\java.exe -version
ANDROID\OpenJDK\bin\javac.exe -version
ANDROID\SDK\platform-tools\adb.exe version
ANDROID\SDK\build-tools\34.0.0\aapt2.exe version
ANDROID\NDK\toolchains\llvm\prebuilt\windows-x86_64\bin\clang.exe --version
```

adb 只查询本地二进制版本，未调用 devices/start-server、未连接设备。javac/clang 只查版本，未传入源码。
普通沙箱启动遇到 CryptUnprotectData 错误；经工具审批改用同样受任务范围约束的只读命令完成检查。该错误不是 Unity/Android 构建错误。
人工用例与设备记录格式见 [manual-acceptance.md](manual-acceptance.md)，窗口交付边界见 [T4 记录](../tasks/T4-环境与验收.md)。

