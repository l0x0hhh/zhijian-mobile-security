<!-- 说明：记录团结引擎构建入口、Actions 自托管运行器和公开 Release 更新检查的使用边界。 -->
# Android 构建与更新检查

## 本机构建

需要团结 1.6.9 / 2022.3.61t10 与 Android Build Support、SDK、NDK 和 JDK，且编辑器已激活。APK 检查插件使用 SDK platform 33 和 Build Tools 34.0.0；主工程本次自动选择 platform 35。先关闭同一个工程的编辑器，避免项目锁冲突。

在 PowerShell 中指定一个**不存在的 APK 路径**，再执行：

```powershell
$env:ZHIJIAN_APK_PATH = 'E:\unity_project\zhijian-mobile-security\UnityProject\Builds\manual\zhijian-debug.apk'
& 'E:\unity\2022.3.61t10\Editor\Tuanjie.exe' -batchmode -quit -projectPath 'E:\unity_project\zhijian-mobile-security\UnityProject' -buildTarget Android -executeMethod Zhijian.Editor.AndroidBuild.Build -logFile 'E:\unity_project\zhijian-mobile-security\UnityProject\Builds\build.log'
```

构建目录需要提前存在；Build Settings 的 Main.scene 必须启用。入口以编辑器实际 BuildReport 和输出文件是否存在判断成功，已存在的目标 APK 不会被覆盖。当前工程采用默认调试签名；不同机器/账户生成的调试证书可能不同，无法保证覆盖安装。

2026-10-30 本机验证：首次构建因插件默认寻找未安装的 Build Tools 30.0.3 失败；将插件明确指定已安装的 34.0.0 后构建成功。随后修复独立编译中 `Application` 命名冲突并再次完整构建成功，退出码0。最终文件为 `UnityProject/Builds/verified-20261030-200019/zhijian-debug.apk`，大小 33,887,063 字节，SHA-256 `B456FA71B16A58A5A3B801E9AAAFD220E3A7F2C7159F2B0BB5D524C9878E0D56`。该目录由 .gitignore 排除，不会作为源码提交。

`aapt` 显示包名 `com.zhijian.mobile.security`、版本 `1.0` / code 1、最低 API 22、目标 API 35、ABI 为 `armeabi-v7a`；仅声明 INTERNET 权限。只支持32位 ARM 的测试包可能无法安装到仅支持64位应用的手机，后续需评估 IL2CPP / ARM64 构建。`apksigner verify` 返回成功，v1 / v2 验证为 true；工具对两个 `META-INF` 元数据条目给出 v1 签名覆盖警告。该包不作为正式发布签名。

构建过程中 AGP 7.4.2 对自动选择的 compileSdk 35 提示兼容性警告；本机成功构建不代表该组合已通过官方兼容认证。当前没有检测到连接的 Android 设备，未安装或执行真机测试；32项独立逻辑检查通过，但不覆盖真实文件系统、更新网络或设备交互。

## GitHub Actions

`.github/workflows/android-build.yml` 在推送 main 或手动运行时执行，不运行 pull_request；构建后执行 APK 检查模块独立检查。权限为 contents: read，只上传 APK 和 SHA-256，保留14天，不自动创建 Release。失败日志存放在运行器临时目录，终端只输出过滤后的末尾信息。

该工作流指定 `[self-hosted, Windows, X64, tuanjie-1.6.9]`，不是 GitHub 免费云主机。要使推送触发的工作实际运行：

1. 在准备作为运行器的 Windows 机器上安装同一版本的团结编辑器和 Android 模块，确认该账户能完成命令行构建和访问依赖。编辑器路径为 `E:\unity\2022.3.61t10\Editor\Tuanjie.exe`；其他机器需调整工作流或该路径。
2. 打开 GitHub 仓库 Settings → Actions → Runners → New self-hosted runner，按 GitHub 显示的当前指令下载并注册 Windows x64 runner，添加自定义标签 `tuanjie-1.6.9`。注册使用 GitHub 临时令牌，不要提交到仓库。选择与团结授权及Android工具链兼容的运行账户。
3. 保持运行器在线；推送包含完整工程和工作流的 main，或从 Actions 页面手动运行。成功后在该次运行的 Artifacts 下载 APK 和 SHA256.txt；失败时检查运行器上的 build.log。

仓库以后公开时，主分支保护和运行器账户隔离要先配置好。自托管运行器会在本机执行仓库代码；仅让可信成员推动能触发该工作流的分支，避免运行不可信 PR。参考 [GitHub 自托管运行器文档](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners) 和 [安全建议](https://docs.github.com/en/actions/reference/security/secure-use)。

## 更新检查与发布

首页“检查更新”由用户主动请求 `https://api.github.com/repos/l0x0hhh/zhijian-mobile-security/releases/latest`，不内置凭据。它比较当前 Application.version 与公开正式 Release 的数字版本标签（例如 v1.0.1），发现更新后只打开固定的 GitHub 最新 Release 页面；用户自行下载和安装。

仓库仍为私有或没有公开 Release 时，GitHub 匿名接口可能返回404，页面明确提示不可用；网络失败提示重试。CI Artifact 不等于 Release，推送后生成 APK 不会自动让 App 检测到新版本。以后发布前，应确定正式的稳定签名密钥和安全保管方式，递增 Android versionCode 与版本号，完成真机验收后再创建 Release；跨签名证书的 APK 不能作为同一应用的覆盖更新。

工作流未把调试包自动发布为正式版本。CI 产物不代表已通过真机安装、覆盖更新、APK 检查或课程验收。

## 修改文件

- 新增 `UnityProject/Assets/_Project/Editor/AndroidBuild.cs` 和其 `.meta`。
- 新增 `.github/workflows/android-build.yml`。
- 新增 `UnityProject/Assets/_Project/Scripts/UnityUI/Updates/UpdatePage.cs` 及目录和脚本 `.meta`。
- 修改 `AppRoot.cs`、`NavigationRequest.cs`、`CaseAwareNavigator.cs`、`InvestigationPageData.cs`、`CaseSelectionPresenter.cs`，接入更新页面。
- 修改 `UnityProject/Assets/Plugins/Android/ZhijianInspection.androidlib/AndroidManifest.xml`，声明更新检查所需 INTERNET 权限；不申请短信、通讯录等敏感权限。
- 修改同一 Android 库的 `build.gradle`，显式使用编辑器配套的 Build Tools 34.0.0，避免插件默认尝试下载30.0.3。
- 新增本文；更新 README。

工作流 YAML 已用 Gradle 随附 SnakeYAML 解析，并核对推送、手动触发与4个执行步骤；这不是 GitHub Actions 运行验证。本地工作流和源码尚未提交或推送；尚未注册 / 验证 GitHub 自托管 Runner，也没有 GitHub Actions 实际运行或公开 Release 的结果。
