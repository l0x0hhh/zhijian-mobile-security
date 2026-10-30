<!-- 说明：记录本窗口实际案件页面、媒体与存档接线，不把静态文件检查当作运行结果。 -->
# S3-UI 案件页面交付

日期：2026-10-19；CC，刘总授权继续。仓库 `E:\unity_project\zhijian-mobile-security`，工程为其下 UnityProject。

## 已落盘行为

原生页面覆盖聊天、资料原文、音视频/图片、相册、教学检测、应用详情、隐私政策、参考资料、模拟文件/安装、权限列表/范围、核验、线索、结论、结果和历史详情。设置与帮助继续复用原有 InfoView。资料、决策、结果三个组合器生成不可变显示数据，Presenter 转发命令，View 不读取领域规则或正确答案。

- 可见消息行才登记阅读；长原文滚动至正文底部登记。图片实际显示后才登记对应图片消息。媒体/检测来源由领域成功回调登记，不能通过打开页面伪造。
- 线索夹只展示已收集内容；可收集项通过现有 EvidenceRules 判断，同一证据 ID 继续去重。核验只显示所选方式的反馈。
- 权限支持拒绝、允许、指定照片/全部相册，证件照先选用途；所有访问和安装只改变模拟状态，不调用设备权限或安装真实包。
- 结论提交使用当前 revision；缺少必做项显示中文提示，缺少关键线索需确认，确认后状态变更会要求重新提交。危险处置直接转发现有用例，不加调查完成前提。
- 音频采用实际 AudioClip 数据和 AudioSource 播放，视频采用 VideoClip、VideoPlayer 准备/错误/播放结束回调，独立请求隔离旧回调；不使用文字稿或固定计时器冒充播放。离页、后台或禁用取消任务，释放 RenderTexture。图片显示等一帧后才回报。
- 检测是明确标注的案件预设教学演示，不上传媒体、不输出真实鉴定置信度。只有完成演示后展示对应观察点。
- 离开进行中调查先确认；返回键优先关闭确认弹层。结果页清理调查返回栈，不能回到旧页面继续改分。
- AppRoot 在 Unity 主线程创建唯一 ProgressService / JsonProgressRepository，目录由 persistentDataPath 下的 Zhijian/Progress 派生。结果冻结事件在调查用例分发返回后导航并记录；页面离开不销毁保存服务。
- ResultPersistenceCoordinator 保留存档未接收或保存失败的冻结结果，按首次接收顺序重送。恢复新建明确确认并保留损坏原件，较新格式不提供覆盖入口。未保存数量和状态始终可见；本次运行内保留不等于关闭后可恢复。
- 首页按“正式练习/可预览”显示，照片案聊天入口改为应用详情；保持暖白/墨绿和日夜主题。中文 TMP 动态字体采样从48提高到72、padding从5提高到7，仍用2048多图集。

## 媒体与开放状态

S3-A/S3-B 的 manifest、来源和实际成品哈希已经核对。主窗口目视检查原创收款图与示例照片；绑定 Sprite 并批准这两份静图。WAV 和 MP4 的真实引用已经写入 CaseAsset，但配音许可仍为 pending，approvedForUse=false，因此播放器会明确提示未就绪，语音/会议案继续预览，不伪造计分或保存。

证件照案没有 Domain 媒体/检测要求，本轮 trainingEnabled=true，仍须现有运行时内容校验通过才显示正式练习。示例照片是独立 assetId，不新增领域媒体门槛。视频字幕已经烧入，未重复叠加 SRT。

## 本窗口实际文件

以下仅列本窗口写入，不把 S3-A/B/C 的独立产物计入自身交付。所有新增源码和目录均配套 meta，已有共享父 meta 保留。另新增本交付文档与静态检查记录 `docs/qa/P3-静态接线.json`。

| 操作 | 文件 |
| --- | --- |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Navigation/DialogContracts.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Navigation/DialogContracts.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Media/MediaContracts.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Media/MediaContracts.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationPageData.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationPageData.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationWorkspace.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationWorkspace.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Navigation/CaseAwareNavigator.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Navigation/CaseAwareNavigator.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/PageComposerContext.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/PageComposerContext.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/SourcePageComposer.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/SourcePageComposer.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/DecisionPageComposer.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/DecisionPageComposer.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/ResultsPageComposer.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/ResultsPageComposer.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationPagePresenter.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationPagePresenter.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Media/UnityMediaAsset.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Media/UnityMediaAsset.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Media/UnityMediaPlayer.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Media/UnityMediaPlayer.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Media/TeachingDetectionRunner.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Media/TeachingDetectionRunner.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation/InvestigationRowView.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation/InvestigationRowView.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation/InvestigationPageView.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation/InvestigationPageView.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs/ConfirmationView.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs/ConfirmationView.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs/UnityDialogService.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs/UnityDialogService.cs) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Progress/ResultPersistenceCoordinator.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Progress/ResultPersistenceCoordinator.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/Presentation/Navigation/NavigationRequest.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Navigation/NavigationRequest.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/UnityUI/Navigation/PageRouter.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Navigation/PageRouter.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/Presentation/Phone/PhonePresenter.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Phone/PhonePresenter.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/UnityUI/Views/Phone/PhoneView.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Views/Phone/PhoneView.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/UnityUI/Theme/ThemeBinding.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Theme/ThemeBinding.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/UnityUI/Theme/ThemeApplier.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Theme/ThemeApplier.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/Presentation/Info/InfoPresenter.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Info/InfoPresenter.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/Presentation/Home/CaseSelectionPresenter.cs](../../UnityProject/Assets/_Project/Scripts/Presentation/Home/CaseSelectionPresenter.cs) |
| 修改 | [UnityProject/Assets/_Project/Prefabs/Pages/CaseSelection.prefab](../../UnityProject/Assets/_Project/Prefabs/Pages/CaseSelection.prefab) |
| 修改 | [UnityProject/Assets/_Project/Scripts/UnityUI/Views/PhoneShellView.cs](../../UnityProject/Assets/_Project/Scripts/UnityUI/Views/PhoneShellView.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/Infrastructure/Content/CaseCatalogAsset.cs](../../UnityProject/Assets/_Project/Scripts/Infrastructure/Content/CaseCatalogAsset.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/Infrastructure/Content/CaseAsset.cs](../../UnityProject/Assets/_Project/Scripts/Infrastructure/Content/CaseAsset.cs) |
| 修改 | [UnityProject/Assets/_Project/Scripts/Bootstrap/AppRoot.cs](../../UnityProject/Assets/_Project/Scripts/Bootstrap/AppRoot.cs) |
| 新增 | [UnityProject/Assets/_Project/Prefabs/UI/InvestigationRow.prefab](../../UnityProject/Assets/_Project/Prefabs/UI/InvestigationRow.prefab) |
| 新增 | [UnityProject/Assets/_Project/Prefabs/Pages/InvestigationPage.prefab](../../UnityProject/Assets/_Project/Prefabs/Pages/InvestigationPage.prefab) |
| 新增 | [UnityProject/Assets/_Project/Prefabs/UI/Confirmation.prefab](../../UnityProject/Assets/_Project/Prefabs/UI/Confirmation.prefab) |
| 修改 | [UnityProject/Assets/_Project/Scenes/Main.scene](../../UnityProject/Assets/_Project/Scenes/Main.scene) |
| 修改 | [UnityProject/Assets/_Project/Data/Cases/voice-loan.asset](../../UnityProject/Assets/_Project/Data/Cases/voice-loan.asset) |
| 修改 | [UnityProject/Assets/_Project/Data/Cases/face-meeting.asset](../../UnityProject/Assets/_Project/Data/Cases/face-meeting.asset) |
| 修改 | [UnityProject/Assets/_Project/Data/Cases/photo-permission.asset](../../UnityProject/Assets/_Project/Data/Cases/photo-permission.asset) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Navigation/DialogContracts.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Navigation/DialogContracts.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Media/MediaContracts.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Media/MediaContracts.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Media.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Media.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationPageData.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationPageData.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationWorkspace.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationWorkspace.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Navigation/CaseAwareNavigator.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Navigation/CaseAwareNavigator.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/PageComposerContext.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/PageComposerContext.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/SourcePageComposer.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/SourcePageComposer.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/DecisionPageComposer.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/DecisionPageComposer.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/ResultsPageComposer.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/ResultsPageComposer.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationPagePresenter.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Investigation/InvestigationPagePresenter.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Media/UnityMediaAsset.cs.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Media/UnityMediaAsset.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Media.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Media.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Media/UnityMediaPlayer.cs.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Media/UnityMediaPlayer.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Media/TeachingDetectionRunner.cs.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Media/TeachingDetectionRunner.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation/InvestigationRowView.cs.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation/InvestigationRowView.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation/InvestigationPageView.cs.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Views/Investigation/InvestigationPageView.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs/ConfirmationView.cs.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs/ConfirmationView.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs/UnityDialogService.cs.meta](../../UnityProject/Assets/_Project/Scripts/UnityUI/Dialogs/UnityDialogService.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Progress/ResultPersistenceCoordinator.cs.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Progress/ResultPersistenceCoordinator.cs.meta) |
| 新增 | [UnityProject/Assets/_Project/Scripts/Presentation/Progress.meta](../../UnityProject/Assets/_Project/Scripts/Presentation/Progress.meta) |
| 新增 | [UnityProject/Assets/_Project/Prefabs/UI/InvestigationRow.prefab.meta](../../UnityProject/Assets/_Project/Prefabs/UI/InvestigationRow.prefab.meta) |
| 新增 | [UnityProject/Assets/_Project/Prefabs/Pages/InvestigationPage.prefab.meta](../../UnityProject/Assets/_Project/Prefabs/Pages/InvestigationPage.prefab.meta) |
| 新增 | [UnityProject/Assets/_Project/Prefabs/UI/Confirmation.prefab.meta](../../UnityProject/Assets/_Project/Prefabs/UI/Confirmation.prefab.meta) |
| 修改 | [README.md](../../README.md) |
| 修改 | [docs/素材台账.md](../素材台账.md) |
| 修改 | [docs/开发计划.md](../开发计划.md) |
| 修改 | [docs/P3并行任务表.md](../P3并行任务表.md) |

## 静态检查与待验收

- 30 个 C# 首行注释、定界符、程序集依赖文本检查
- Assets 全局 GUID 无重复；新增目录保留独立 meta
- 8 份原生预制体、场景和案件资产的本地 ID / 外部 GUID 引用检查
- 领域源码和冻结存档契约未改；所有目标仍匹配写入前基线
- 已装配 S3-C 具体存档实现；本检查未执行存档代码或访问运行存档
- 新增预制体的本地对象 ID 和脚本/资源 GUID 可定位，Main 的外部组件引用与新增预制体 ID 对应；媒体引用与固定 ID 对应，成品哈希与 manifest 一致。
- 检查属于源码、元数据和序列化文本核对，没有实例化 C# 服务或执行领域校验器。
- 未启动 Unity/Hub、导入、编译、运行工程测试、访问真实运行存档、构建 APK、真机试听/播放、验证返回/后台/保存恢复。布局仍需刘总在编辑器查看；不能据此写成运行通过。
- 未改 Domain、冻结存档接口、S3-A/B/C 独占实现、asmdef、Packages 或 ProjectSettings；未 stage、提交、推送或清理他人文件。

建议查看路径：打开 Main.scene → Play → 证件照权限 → 应用详情逐项阅读 → 权限管理选用途并逐项判断 → 核实来源 → 线索夹 → 作出判断 → 提交 → 结果/记录。语音与会议的静图/资料可预览，音视频许可门槛解除前不会标记完成。
