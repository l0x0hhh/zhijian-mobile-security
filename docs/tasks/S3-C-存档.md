<!-- 说明：记录 S3-C 的实际新增实现、冻结接口使用、数据与恢复策略及只做静态检查的边界。 -->
# S3-C：存档交付记录

日期：2026-10-19（Asia/Shanghai）；执行者：CC；刘总授权范围：[P3 并行任务表](../P3并行任务表.md) 与 [P3 接口与交付契约](../P3接口与交付契约.md) 的 S3-C。

实现已新增，**未启动 Unity、未编译、未运行自动化测试或存档代码，未做真实写入/恢复验收**。本轮没有实例化 JsonProgressRepository、访问用户运行存档、生成测试数据、修改正式记录或接入 UI。不能把下面的源码行为描述当作运行通过。

## 1. 实际工作区与文件

实际工作区：`E:\unity_project\zhijian-mobile-security`；工程位于其下 UnityProject。未新建/切换分支或工作树，未 stage、提交、推送或清理他人改动。

实际新增 7 个 C# 文件和 7 个配对 .meta；没有新建 Assets 子目录，已有共享父目录 meta 保持原样：

| C# 文件 | 配对元数据 | 职责 |
| --- | --- | --- |
| [Application/Ports/IProgressRepository.cs](../../UnityProject/Assets/_Project/Scripts/Application/Ports/IProgressRepository.cs) | [meta](../../UnityProject/Assets/_Project/Scripts/Application/Ports/IProgressRepository.cs.meta) | 加载、完整档提交、确认恢复后的归档新建接口 |
| [Application/Progress/ProgressArchive.cs](../../UnityProject/Assets/_Project/Scripts/Application/Progress/ProgressArchive.cs) | [meta](../../UnityProject/Assets/_Project/Scripts/Application/Progress/ProgressArchive.cs.meta) | 不可变存档、永久回执、聚合、历史与交叉关系校验 |
| [Application/Progress/ProgressRepositoryResult.cs](../../UnityProject/Assets/_Project/Scripts/Application/Progress/ProgressRepositoryResult.cs) | [meta](../../UnityProject/Assets/_Project/Scripts/Application/Progress/ProgressRepositoryResult.cs.meta) | 仓储加载反馈及阻止覆盖的明确状态 |
| [Application/Progress/ProgressService.cs](../../UnityProject/Assets/_Project/Scripts/Application/Progress/ProgressService.cs) | [meta](../../UnityProject/Assets/_Project/Scripts/Application/Progress/ProgressService.cs.meta) | 冻结 IProgressService 的实现；FIFO、去重、待保存合并、快照发布 |
| [Infrastructure/Persistence/ProgressJsonDto.cs](../../UnityProject/Assets/_Project/Scripts/Infrastructure/Persistence/ProgressJsonDto.cs) | [meta](../../UnityProject/Assets/_Project/Scripts/Infrastructure/Persistence/ProgressJsonDto.cs.meta) | 普通显式字段 DTO，集合用数组，不含完整会话/定义 |
| [Infrastructure/Persistence/ProgressJsonCodec.cs](../../UnityProject/Assets/_Project/Scripts/Infrastructure/Persistence/ProgressJsonCodec.cs) | [meta](../../UnityProject/Assets/_Project/Scripts/Infrastructure/Persistence/ProgressJsonCodec.cs.meta) | 格式探测、显式 Guid/UTC/枚举转换、分数/聚合校验 |
| [Infrastructure/Persistence/JsonProgressRepository.cs](../../UnityProject/Assets/_Project/Scripts/Infrastructure/Persistence/JsonProgressRepository.cs) | [meta](../../UnityProject/Assets/_Project/Scripts/Infrastructure/Persistence/JsonProgressRepository.cs.meta) | 目录注入、主备探测、临时校验、替换、原件复制和恢复 |

另新增：

- [docs/qa/progress/manual-cases.md](../qa/progress/manual-cases.md)：38 条待执行边界用例，全部“未执行”；新 docs/qa/progress 不在 Assets 内，不生成 Unity meta。
- [docs/tasks/S3-C-存档.md](S3-C-存档.md)：本记录。

合计 16 个新增文件。代码首行均为简略中文职责注释。没有修改冻结 IProgressService.cs/ProgressSnapshot.cs 或其 meta，没有另建同名模型；Domain、Investigation、Bootstrap、UI、asmdef、Packages、ProjectSettings 和其他窗口文件均未由本窗口写入。

## 2. 冻结接口及新增仓储用法

命名空间与入口：

```csharp
Zhijian.Application.Progress.ProgressService(IProgressRepository repository)
Zhijian.Infrastructure.Persistence.JsonProgressRepository(string directory)
```

实现完整承接冻结 IProgressService：Current、Changed、InitializeAsync、RecordAsync、RetryPendingAsync、StartNewSaveAsync、Dispose。冻结模型的构造参数、枚举及名称均未修改。

IProgressRepository 为新端口，实际方法是：

```csharp
Task<ProgressRepositoryResult> LoadAsync();
Task SaveAsync(ProgressArchive archive);
Task ArchiveAndCreateAsync(ProgressArchive archive);
```

ProgressRepositoryResult 返回固定 ProgressLoadState、不可变 ProgressArchive 和显示消息；ProgressWriteBlockedException 只携带 RecoveryRequired 或 Incompatible。一般 IO 异常由 ProgressService 转为保存失败，待保存结果保留。ProgressArchive/ProgressReceipt 是跨 Application/Infrastructure 的新存档数据，不替代冻结的页面模型。

由主窗口在 UI 同步上下文（例如 AppRoot 的主线程组装阶段）构造单例；以下仅为接入片段，**没有修改或执行 AppRoot**：

```csharp
// 片段中的回调、result 与确认动作由主窗口提供，不是本窗口新增的 UI。
var repository = new JsonProgressRepository(
    System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "LearningProgress"));
IProgressService progress = new ProgressService(repository);
progress.Changed += OnProgressChanged; // 此回调只刷新展示。
await progress.InitializeAsync();

// 既有调查入口产生唯一冻结 CaseResult 时：
var feedback = await progress.RecordAsync(result);

// 后续用户点击“重试保存”：
await progress.RetryPendingAsync();

// 仅在 RecoveryRequired 且用户已明确确认保留原件后：
await progress.StartNewSaveAsync();
```

目录必须是调用方传入的专用绝对路径。构造函数不创建目录、加载不新建空档；代码不含固定盘符。只在真实保存或确认恢复时按传入目录创建文件。Application 不引用 Unity、JSON 或文件 API；JsonUtility 和 File/Directory 代码均在 Infrastructure，未增加第三方包或改程序集。

## 3. 队列、快照与生命周期

- 所有请求按入队锁所确定的首次接收顺序，在同一 FIFO Task 链串行完成；每次写入的是合并后的完整候选档，不让页面各自持有一份旧档覆盖。
- 首次被接收的新 resultId 分配上一水位+1 的 Sequence。重试、重复送达、保存完成时间和 ResolvedAtUtc 都不重分配 Sequence。重启从成功加载档的水位继续。
- 初始化隐式先于首次 Record/Retry/StartNew，并且每个服务仅加载一次；尚未加载成功不能写默认空档。初始加载普通 IO 失败时为 Failed/Blocked，不以新建代替读取。
- 永久回执单独保存所有 resultId、Sequence、案件/评分版本、总分、Outcome，用于持久去重与聚合校验；它不随最近历史裁剪。
- 聚合按 caseId+scoringRuleVersion 保留 BestTotalScore、可空 BestSafeScore、HasSafeClear、LastResult。未知案件和旧规则版本不按当前目录过滤。失败不能清已有安全分/标志。
- 全局 RecentResults 按 Sequence 倒序最多 10 条；每组 LastResult 与最高分独立于该 10 条展示历史，永久回执不裁剪。回执空间随已接收结果增长，不在本轮引入会破坏持久去重的自动压缩。
- 普通写入失败时，候选档和待保存 ID 集合在当前进程内保留；下一 Record 或 Retry 合并全部待保存结果。只有仓储完整提交成功才清空待保存集合。只复制冻结结果的展示字段，不保留完整会话/定义，不调用领域操作或修改原 CaseResult。
- Cases/RecentResults 快照包含当前已接收、可能尚未落盘的数据，UI 必须结合 SaveState 和 PendingResultCount 明确“未保存”。不能只看通关标志就声称已经持久化。
- 创建服务时捕获 SynchronizationContext；正常情况下 Current 更新及 Changed 都在该上下文派发，等待通知完成后再继续下一操作。没有创建上下文时在串行执行线程通知，不保证 UI 亲和性。
- Changed 逐一隔离同步订阅异常；已成功写入不会因此变 Failed 或回滚。通知期间重入命令同步抛 InvalidOperationException；回调只刷新，命令在通知结束后调度。异步 void 订阅者跨 await 的异常仍须订阅者自己处理。
- 若上下文本身拒绝 Post，更新线程安全的 Current，记录诊断且不改到错误线程触发事件。若上下文接受但不再处理消息，操作可能等待；主窗口须保持上下文存活并异步 await，不在 UI 线程 .Wait()/Result。
- Dispose 解除订阅并拒绝新命令；已排队的操作继续完成，不删除存档。Dispose 本身不保证未落盘结果已经持久化；关闭/强杀后不承诺恢复未保存数据。

## 4. 数据格式与校验

当前 schemaVersion=1，formatId=`zhijian.progress`。contentVersion 保留每份可见结果的原内容版本；scoringRuleVersion 是聚合键的一部分。当前没有已定义的旧 schema，不猜测未知历史格式的迁移；后续增加版本时应显式增加迁移实现与用例。

空档结构示例（仅写在本文，没有创建运行文件）：

```json
{"schemaVersion":1,"formatId":"zhijian.progress","lastSequence":0,"receipts":[],"cases":[],"recentResults":[]}
```

- DTO 为 [Serializable] 的普通字段类，运行时字典转换为 receipts/cases 条目数组；从不直接序列化 CaseResult、SessionState 或 CaseDefinition。
- Guid 显式使用非空 D 格式字符串；DateTimeOffset 显式转 UTC，以 invariant 的 O 格式往返；Outcome 和 ScoreDimension 显式整型并校验枚举。可选说明文字的 null 规范化为空字符串，不改变原业务对象。
- nullable BestSafeScore 用 hasBestSafeScore+bestSafeScore 表达，安全零分不等于没有安全通关。
- 校验版本/格式、必要集合、非空条目、ID/Sequence 唯一、顺序水位、版本/分值范围、三分项与明细合计、receipt/LastResult/最近历史关联、每组最高总分和最高安全分/标志。
- 最近历史必须对应最新的至多 10 条回执；同 ID 在聚合和历史中重复出现时，冻结展示内容必须一致。输入集合可规范排序。
- 保存前还要求候选包含磁盘已有全部回执，拒绝用陈旧实例覆盖其他已持久化结果。这是陈旧写入防护，**不是多实例/多进程互斥保证**；同目录必须由主窗口保留唯一应用级服务/仓储实例。
- JsonUtility 仅在仓储工作线程转换独占的普通 DTO。官方依据见 [Unity 2022.3 JSON Serialization](https://docs.unity3d.com/2022.3/Documentation/Manual/JSONSerialization.html) 和 [FromJson 字符串重载](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/JsonUtility.FromJson.html)；文档允许普通结构 JSON 的后台线程转换。本机团结实际运行仍未验证。

## 5. 文件与恢复行为

所有路径以构造参数 directory 为根：

| 文件/目录 | 源码策略 |
| --- | --- |
| progress.json | 固定主档 |
| progress.backup.json | 固定有效备份；主档有效时先备份旧主档；首次保存没有有效主/备档时先备份候选 |
| .<目标文件名>.<独立 GUID>.tmp | 同目录独占临时文件，写完 Flush(true)，重新读取/解析/全关系校验并比较规范 JSON 后才晋升 |
| recovery-<独立 GUID>/ | 原件归档；逐字节复制主/备文件，重新计算 SHA-256 比对；永久保留，不自动清理 |
| <目标文件名>.previous-<独立 GUID> | File.Replace 不支持时的同目录重命名前件；成功校验后仅清理本次自己的前件，失败残留不批量清除 |

正常优先 File.Replace；捕获 NotSupportedException 时才使用重命名后备流程，权限、空间等一般 IO 错误不伪装成“不支持”继续替换。替换主档前必须有可重新解析的有效备份；若重命名安装失败，尝试恢复原路径。临时与目标都重新解析校验，普通读写错误返回失败。仅清理本次调用生成的确切临时路径，不扫描删除其他用户文件或上次残留。

| 探测状态 | 加载/写入行为 |
| --- | --- |
| 主备均不存在 | Ready/Idle 空快照；只读加载不落盘 |
| 主档有效 | 读取主档；备份损坏时显示异常，下次提交先保留坏件再修复 |
| 主档损坏或缺失、备份有效 | RecoveredFromBackup；初始化不改原件，下次提交保留损坏原件和有效备份后修复 |
| 主备均无有效数据（含一份坏、一份缺失） | RecoveryRequired/Blocked；原件保持不变，等待明确确认 |
| 用户确认 StartNew 且仍为 RecoveryRequired | 先复制并校验所有原件；全部成功后才新建主/备档。初次双坏加载使用空档；运行中已有合法内存档/待保存结果时保留该内存档及原顺序 |
| 主档或备份任一为较新 schemaVersion | Incompatible/Blocked，绝不回退旧备份、覆盖较新文件或允许 StartNew |
| 文件不可读取 | 与损坏区分；初次无法读取主档为 Failed/Blocked，不绕过读取错误覆盖。主档有效但备份不可读时可展示主档，保存会失败并保留待保存数据 |
| 正常写入 IO 失败 | 只影响保存状态，保留候选；下一次写入重查磁盘状态，遇较新格式/双坏再转对应阻断状态 |

仅当文件可按现行格式验证时才称“有效”。加载不自动晋升不明临时文件；较新格式在解析完整现行模型之前识别。双坏新建前归档任一步失败都不得继续覆盖。备份代表最后一份可验证的备份快照，不承诺找回已损坏主档独有的新结果。

File.Replace、Flush(true)、重命名间隙及强制结束的真实平台效果尚未执行；代码没有目录 fsync/多进程锁，不声称应对所有断电或存储硬件故障。必须以专用 QA/Android 实测更新结论。

## 6. 主窗口 AppRoot 接入位置与契约建议

1. 在创建 Result/Records Presenter 前于主线程组装一套 ProgressService/JsonProgressRepository，专用目录由 UnityEngine.Application.persistentDataPath 派生。不要每个页面重新加载/创建一个仓储。
2. 初始化后，将同一 IProgressService 注入结果/记录 Presenter；在既有调查产生终局 CaseResult 的应用级分支只送一次 RecordAsync。页面离开不销毁待保存服务，重复送达仍由 resultId 去重兜底。
3. Changed 只刷新；保存失败按钮调用 RetryPendingAsync；RecoveryRequired 的明确确认动作调用 StartNewSaveAsync，Incompatible 不提供新建覆盖。
4. Blocked 的新 Record 没有被接收/分配 Sequence，也不会被服务暗中加入队列。调用方需保留该原 CaseResult；恢复可写后重新送达。已经接收的待保存结果则由服务持有，恢复时不重排。
5. InitializeAsync 按契约只初始化一次；若主窗口需要“重试读取”体验，应先等待已接收操作结束并释放旧服务，再构造新服务重新读，不能把 RetryPending 偷改成覆盖式初始化。
6. 若需要退出前保存，先 await 实际待保存操作，等 Saved 后再关闭上下文/Dispose；不能把 OnDestroy 的 Dispose 写成已保存承诺。
7. 本窗口没有修改/实现结果页、记录页、调查用例、生命周期适配、AppRoot、任何预制体或场景；具体接线由主窗口完成。

这些是接入建议及固定接口的边界说明，不是要求修改冻结契约的阻塞事项。本窗口未等待媒体或页面产物。

## 7. 静态检查与未验证事项

已做的静态核对：

- 7 个源码的首行注释、代码定界符、端口方法/模型构造参数与冻结接口对照；落盘文本与准备稿一致。定界符检查不是 C# 编译。
- 7 对源码/meta 配对，新增 GUID 各自在 Assets 中只出现一次；没有新建/重写共享父目录 meta。
- 新增 Application 文件未引用 UnityEngine/UnityEditor、System.IO、JsonUtility 或第三方 JSON；文件与 JSON 实现在 Infrastructure。未改任何 asmdef/包。
- 两份冻结源码和各自 meta、Application/Progress.meta、Infrastructure/Persistence.meta、Application/Ports.meta 共 7 个文件的 SHA-256 与读前基线一致。其中冻结代码匹配契约发布值：
  - IProgressService.cs：`5f796865489777dbcfb882edd77067cab633830abbe09dc75ecac77e1de2748c`。
  - ProgressSnapshot.cs：`1b0c73532a6322384f5977ed87c1555aeef4adbd02abc1c39d4d3747a5cf50db`。
- 38 条用例均标未执行；交付文档只链接本窗口已有文件/公共契约，未将静态推演改成运行结果。

未做：Unity/团结启动、导入、C# 编译、自动化测试、JsonUtility 实际往返、线程上下文通知执行、实际保存/备份/损坏/中断/恢复、Android 真机/性能或 UI 集成验收。没有实际构建或执行通过证据，没有触碰用户已有运行存档。

验收后由主窗口在 [manual-cases.md](../qa/progress/manual-cases.md) 关联真实设备、目录和日志补结果；本轮状态保持“未执行”。

## 8. 新增源码与元数据 SHA-256

以下从实际落盘文件只读计算；只用于主窗口汇合核对，不代表编译或执行通过。本文自身不嵌入自身哈希。

| 源码文件名 | 源码 SHA-256 | 配对 meta SHA-256 |
| --- | --- | --- |
| IProgressRepository.cs | `dbeb13f8bd606a0b403e79985b40538050663ce2490d42b52efb6b4fa9d55f1c` | `be5e406fc8bf3e7b30f498f9027b992a87c388b7a33a6cbebf5582e44223ea74` |
| ProgressArchive.cs | `784cb1486d6055f39fc0e739f758439ba3e2ad5ae1c1e68abfc1857515558321` | `0ece7cf6b03389a487e387b58d6548ee053c347dec32199bf1e12386e208e709` |
| ProgressRepositoryResult.cs | `7608da6fbcd11d69e331b6e470eabd64f712d86b0aac186fbc49615a441252a8` | `07fc7bff22c11784d76126fd4ff1b4f8ad7ec9e2d465a2c4f86fcdf6e0f186b1` |
| ProgressService.cs | `f590c819fbad149fae2db7de427ed93400492b562af15ce6e5eae9595b808a39` | `7cf1c2be773e61094a169300ef1098e7d63cfdbf2863fd255f995c6794ef4455` |
| JsonProgressRepository.cs | `e56c5028c9c65d5306fc92191faa2bf4c2306f0c86fd2aec0853d4f4afada584` | `631ed119e2e7af47ba3b58fcdda3eccef3e0d9dfe3cee8df83dcd601ae661bf1` |
| ProgressJsonCodec.cs | `a303e5ed498fa8d93eb08e3e43961826aa012f2f5df85e895c22e4b6807f5bf4` | `25a9f34bfc348a0b96e660c16d960fdd7fbf3bd254450f2eb1529865a2524a0f` |
| ProgressJsonDto.cs | `700681e231ae5031b1b0ce454db2297a7e8d4be925daddd0693542fa0b17042f` | `2c6e8ed54a4db8a4c6951d4a76eaa2a6277a6805e03e2d28f390d91c4672637b` |

待执行用例文档 SHA-256：`97f244498cd829357e1d74350d055bd296c58aa25f01af763bd41e451984671c`。运行数据不在本轮文件清单中，没有为获取这些哈希执行存档实现。
