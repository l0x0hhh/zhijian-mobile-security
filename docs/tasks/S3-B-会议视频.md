<!-- 说明：S3-B 真实会议视频交付记录；媒体制作和技术检查已完成，未替主窗口绑定资源或批准许可。 -->
# S3-B 会议视频交付

日期：2026-10-19，Asia/Shanghai。仓库：`E:\unity_project\zhijian-mobile-security`。按 [P3 并行任务表](../P3并行任务表.md)与[交付契约](../P3接口与交付契约.md)独立完成，不等待、读取或复用 S3-A 的配音/角色产物。

## 1. 交付结果

实际成片为 16.000 秒、720×1280、25fps / 400 帧、H.264 Main level 3.1 / yuv420p、AAC-LC 48kHz 单声道。完整解码无报错，4 段字幕按实际配音和最终 AAC 活动范围校准。视频持续显示“虚构情境 · 教学模拟”，普通配音没有克隆真人；角色和场景为本轮代码原生矢量绘制。

**制作与媒体文件检查已完成；许可状态 pending、Unity/Android 播放未验收、资源未由本窗口绑定。** manifest 的 pending 是未取得内置声音输出独立分发许可凭据的事实，不是媒体占位或文件损坏；没有伪造授权，也没有设置 approvedForUse/trainingEnabled。

## 2. 实际文件清单

工程资源前缀：`UnityProject/Assets/_Project/Media/FaceMeeting/`。

| 实际新增文件 | 内容 |
| --- | --- |
| `meeting.mp4` | 真正的 H.264/AAC MP4，424,518 bytes；没有将图片/字幕改扩展名 |
| `meeting.mp4.meta` | 新 VideoClipImporter meta，GUID `c7c6b12217254230b7ad386a8fbdbbae`，保留音轨导入设置 |
| `meeting.srt` | 实际对齐的 UTF-8 字幕，4 段，末尾 15.229 秒 |
| `meeting.srt.meta` | 新 DefaultImporter meta，GUID `4c0dfe6a61d249b5ba691c4a8e33abe8`；字幕如何读取/显示由主窗口处理 |

制作资料前缀：`docs/media/S3-B/`。下列 18 个文件均为本窗口实际新增：

| 实际新增文件 | 内容 |
| --- | --- |
| [manifest.json](../media/S3-B/manifest.json) | schemaVersion=1、taskId=S3-B、VID-001、固定 mediaId、真实规格/哈希、来源路径与许可状态 |
| [provenance.md](../media/S3-B/provenance.md) | 原创画面、普通配音、字体、工具来源/校验、异常制作、实际字幕、许可待核对与复现说明 |
| `character.svg` | 原创虚构角色和场景的可编辑矢量几何快照 |
| `render_meeting.py` | 原创几何动画、配音排布、嘴形错位/边缘/光照、字幕与视频编码源码，首行有说明 |
| `synthesize.ps1` | 本机 SAPI 普通中文合成源脚本，首行有说明 |
| `verify_meeting.py` | 实际媒体探测、全解码、字幕活动范围与关键帧检查源脚本，首行有说明 |
| `meeting-voice.wav` | 本任务独立配音制作音轨，PCM s16le，48kHz 单声道，16 秒 |
| `meeting-transcript.txt` | 实际台词与测得活动时间；非原拟定四秒时码 |
| `meeting.srt` | 与工程字幕逐字节一致的文档副本 |
| `subtitle-timing.csv` | 原分句实测长度、活动边界、放置位置、最终字幕时间和原音频哈希 |
| `motion-cues.csv` | 400 帧嘴形包络/延时、转头、边缘偏移与光照参数的制作轨迹 |
| `build-measurements.json` | 实际源配音测量、制作规格、最终视频/音轨哈希；只在自己的 docs 范围，不是运行时案件 JSON |
| `encoding-command.txt` | 实际编码命令与参数 |
| `toolchain.txt` | 实测 Python/Pillow/NumPy、声音、FFmpeg 版本与哈希 |
| `verification/ffprobe.json` | 对最终 MP4 的真实 ffprobe 输出 |
| `verification/verification.json` | 实际全解码、音轨、字幕、哈希和未验证标记 |
| [verification/contact-sheet.jpg](../media/S3-B/verification/contact-sheet.jpg) | 最终 MP4 实际解码的 6 帧总览 |
| [verification/anomaly-detail.png](../media/S3-B/verification/anomaly-detail.png) | 实际成片嘴形/边缘对比与脸颈受光局部 |

此外新增本记录 `docs/tasks/S3-B-会议视频.md`。合计 23 个文件（工程 4，制作资料 18，交付记录 1）。工程里没有新建子目录，因此只为两个资源新增 meta；既有 `FaceMeeting.meta`、`Media.meta` 均未改写。

## 3. 实际台词与字幕

| 段落 | 最终 SRT | 台词 |
| --- | --- | --- |
| 1 | 00:00:00,280 → 00:00:03,814 | 今天临时加个会，用轻会助手参加。 |
| 2 | 00:00:04,400 → 00:00:06,882 | 下载链接已经发到聊天里了。 |
| 3 | 00:00:08,320 → 00:00:10,771 | 装好后打开短信和通讯录。 |
| 4 | 00:00:12,000 → 00:00:15,229 | 我们马上开始，先把工具准备好。 |

字幕是在本任务实际声音生成后量测活动边界、排布后生成，并从最终 AAC 再检查覆盖；未照抄案件脚本的拟定时码。字幕已经烧入画面，SRT 仍单独交付便于检查或后续使用；主窗口需避免再叠加一层重复字幕。

## 4. 三异常与真实检查结果

| 项目 | 真实结果 / 证据 |
| --- | --- |
| 嘴形音画错位 | 0–4 秒嘴形驱动显式滞后音轨 0.46 秒；最终 0.52 秒语音已开始而嘴形闭合，0.96 秒嘴形张开；后段不额外加同一延迟。参数不是实际鉴定结果 |
| 转头边缘不稳定 | 4.15–7.90 秒转头与面部位置变化时，右脸缘局部条带变形/漂移；实际 4.80/5.04 秒帧可对照 |
| 脸颈光照差异 | 8–12 秒脸左亮右暗、颈左暗右亮；实际 9.44 秒可见。初稿发型遮住颈部后已调整并重编码，文档/哈希对应最终版本 |
| 格式、时长与轨道 | ffprobe 实测 MP4，16.000 秒，视频 400 帧，720×1280，25/1fps，H.264 Main / yuv420p，AAC-LC / 48kHz / 单声道，音轨同为 16.000 秒 |
| 完整解码 | FFmpeg 对最终成片全解码，退出码 0，无错误输出；这属于媒体文件检查，不是 Unity 工程测试 |
| 音轨有效性 | 最终 AAC 解码为 768,000 个样本；与制作音轨同长度，相关系数约 0.9999689，四段均有有效语音活动 |
| 字幕范围 | 4 段递增、无重叠、均在 0–16 秒内，末段结束 15.229 秒；四段均覆盖最终 AAC 阈值活动范围；工程/文档 SRT 完全一致 |
| 可视检查 | 已查看最终解码帧总览和局部图；教学标签、字幕及三异常可见，没有只写检测文本 |
| 哈希 | MP4：`4b9b22176b7aa8489dc16fb37d4ecc7ca9c0d7d13f57381aa5e43e78197e780d`；SRT：`bd175693e7bc771876a9586315b8649222f5c945303ae6dc0b782e03b6ea473d` |
| 来源/结构静态核对 | manifest 引用实际存在且哈希匹配；SVG 可解析；三脚本首行说明存在；两新增 GUID 在当前 Assets 扫描中各只出现一处；共享父 meta 与初始哈希一致 |

未声称人工完整听审、逐音素口型鉴定或真实合成检测。字幕活动核对使用音量阈值，不是语音识别；三异常是人为教学示例。

## 5. 来源、工具与许可

- 角色/画面：本轮代码原生矢量制作，无真人、第三方角色或真实会议平台参考；源文件已保留，未用 imagegen 生成位图角色。
- 配音：本机 `Microsoft Huihui Desktop - Chinese (Simplified)` 普通 SAPI 合成，Rate=1；未克隆真人，未依赖 S3-A，未联网调用声音服务。完整来源与 token 见 provenance。
- 字体：只读复用工程 Noto Sans SC 与既有 SIL OFL 1.1，不改字体或其 meta。
- 工具：FFmpeg 官网下载页列出的 Gyan 第三方 Windows 构建，实际版本 9.0.2；固定 ZIP 的 SHA-256 `60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba` 与发布方匹配。工具、ZIP 和原分句/检查帧只放自己的 Temp，没有改 PATH、全局安装或打包工具进工程。
- 许可：没有声音输出的独立分发许可凭据，manifest 如实 `pending`；不把系统可生成或接口文档等同分发授权。主窗口最终核对适用条款或选择许可明确的替代普通配音后决定批准。

## 6. 待主窗口绑定及未验证项

待绑定固定关系：`VID-001` → `face-meeting.media.meeting` → `UnityProject/Assets/_Project/Media/FaceMeeting/meeting.mp4`；配套字幕为同目录 `meeting.srt`。主窗口在最终汇合时检查来源和许可，独自绑定 VideoClip、处理 SRT/字幕显示并更新素材台账与阶段状态。本窗口未改 CaseAsset、案件 JSON、AppRoot、播放器、公共台账、Packages、ProjectSettings、asmdef 或其他窗口文件。

未执行：Unity 导入/meta 采纳、播放器完整音画试听、应用后台/返回行为、Android 真机、APK 或工程测试。后续实际播放仍需验证字体/字幕缩放、音画表现、视频中断与恢复；本轮媒体解码通过不等于这些已经通过。

没有启动 Unity/Hub、编译、运行工程测试、切换分支、stage、提交、推送或清理他人文件。没有改共享父 meta，没有把工具或巨量中间帧放入工程。所有交付文件均在 S3-B 独占范围。
