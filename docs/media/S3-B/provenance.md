<!-- 说明：S3-B 实际素材来源与制作记录；区分原创几何、普通系统合成音、已知许可及未确认分发许可。 -->
# S3-B 会议视频来源记录

制作日期：2026-10-19，Asia/Shanghai。仓库：`E:\unity_project\zhijian-mobile-security`。需求依据为 [P3 并行任务表](../../P3并行任务表.md)、[交付契约](../../P3接口与交付契约.md)和[会议脚本](../../cases/face-meeting.md)。本记录不修改公共素材台账或批准资源开放。

## 实际素材

- 视频：`UnityProject/Assets/_Project/Media/FaceMeeting/meeting.mp4`，16.000 秒，720×1280，25fps，共 400 帧，H.264 Main level 3.1 / yuv420p，AAC-LC 48kHz 单声道；424,518 bytes。
- 字幕：同目录 `meeting.srt`，UTF-8，4 段；另有本目录一致副本。成片内也烧录了同一份字幕，后续播放器避免重复叠加字幕层。
- 资源 ID：`VID-001`；待主窗口绑定 `face-meeting.media.meeting`。本窗口没有修改 CaseAsset、案件 JSON、approvedForUse 或 trainingEnabled。
- 视频 SHA-256：`4b9b22176b7aa8489dc16fb37d4ecc7ca9c0d7d13f57381aa5e43e78197e780d`。
- SRT SHA-256：`bd175693e7bc771876a9586315b8649222f5c945303ae6dc0b782e03b6ea473d`。

## 角色、画面与字体

人物“许岚”、工作室、背景、服装、五官、发型、植物和陈设均由本轮 `render_meeting.py` 的代码原生矢量几何绘制，没有输入真人照片、参考人像、声音样本、第三方角色或真实会议平台截图。原始可编辑几何见 `character.svg`，动画规则保留在生成脚本。未调用 imagegen：本任务采用可保留源码的矢量角色，没有位图角色生成步骤。

画面使用暖白与墨绿，信息为“会议说明”“青栎工作室”、虚构角色姓名和台词；不使用真实品牌或平台商标。顶部“虚构情境 · 教学模拟”从第一帧到最后一帧持续保留；底部说明普通合成配音及无真人素材。视频中不额外标出异常答案。

字体只读复用工程已有的 `NotoSansSC-Regular.otf`，未复制或修改字体文件：

- 工程来源：`UnityProject/Assets/_Project/Art/Fonts/SOURCE.md`。
- 已有上游：Noto CJK 官方仓库提交 `f8d157532fbfaeda587e826d4cd5b21a49186f7c`。
- 字体 SHA-256：`faa6c9df652116dde789d351359f3d7e5d2285a2b2a1f04a2d7244df706d5ea9`。
- 已有许可：[SIL OFL 1.1 文本](../../../UnityProject/Assets/_Project/Art/Fonts/OFL.txt)。实际检查了本地许可对生成文档的说明；此处仅将文字栅格化进视频，不再分发另一份字体。

矢量几何和场景为本轮制作内容，不冒称获得真人肖像授权；本任务没有使用真人肖像，因此没有虚构“真人授权书”。“原创矢量”不自动替代声音及其他工具的适用条款核对。

## 独立配音来源

使用本机已存在的 `Microsoft Huihui Desktop - Chinese (Simplified)`，通过 Windows `SAPI.SpVoice` 普通文字转语音。没有调用 S3-A 的任何产物，没有训练或克隆声音，也没有提供目标真人样本。未联网调用合成服务，未复制语音引擎或语音模型到仓库。

- 本机 token：`HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech\Voices\Tokens\TTS_MS_ZH-CN_HUIHUI_11.0`，仅记录标识，未修改注册表。
- 参数：Rate=1，Volume=100；SAPI 格式 38，即 48kHz / PCM 16-bit / 单声道，生成后已实际读取 WAV 确认。
- 四句输入和生成过程：`synthesize.ps1`；未处理的分句 WAV 仅存任务 Temp。四句输入与案件脚本文字一致。
- 本目录 `meeting-voice.wav` 是裁去句首/句尾静音、按实测活动边界排入 16 秒时间线的制作音轨。没有变声或变速，没有背景音乐和惊吓音效。
- SAPI 技术依据：[SpVoice](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms720149(v=vs.85))、[AudioOutputStream](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms723597(v=vs.85))、[SpeechAudioFormatType](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms720595(v=vs.85))。这些是接口说明，不是声音输出再分发许可。

**许可状态仍为 pending**：角色与画面无外部人物素材，字体许可已在工程留存，声音来源和生成事实可追溯；但本轮没有取得该 Windows 内置声音输出面向 APK/公开视频再分发的独立许可凭据。因此不把“普通合成音”“系统内置”或问答网页视为已获完整分发授权。主窗口最终确认适用条款或更换具有明确分发许可的普通声音后，再决定资源使用批准。此状态不代表技术文件缺失，也不替主窗口设置批准标记。

## 编码工具来源与校验

原有 PATH 未找到 ffmpeg/ffprobe；Pillow、NumPy 和本机普通中文 SAPI 已可用。只下载便携工具到：

`C:\Users\l0x0hhh\AppData\Local\Temp\zhijian-S3-B-20261019\`

没有全局安装、改 PATH、改工程 Packages 或引入编码器二进制。生成时原始帧通过 stdin 管道直接交给编码器，没有数百帧序列写入仓库；解码检查的六张原始 PNG 也只保留在任务 Temp。

- 入口：[FFmpeg 官网下载页](https://ffmpeg.org/download.html)，其 Windows 区链接到 [Gyan Windows 构建页](https://www.gyan.dev/ffmpeg/builds/)。这属于官网列出的第三方 Windows 构建，不冒称 FFmpeg 官方直接发布二进制。
- 固定 ZIP：[ffmpeg-9.0.2-essentials_build.zip](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip)。
- 发布方校验值：[对应 SHA-256](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip.sha256)。实际下载与该值匹配：`60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba`。
- 版本实测：`ffmpeg version 9.0.2-essentials_build-www.gyan.dev`；gcc 16.2.0，使用 libx264 与 AAC 编码器。版本、库版本和输出参数见 `toolchain.txt`、`encoding-command.txt`、`verification/ffprobe.json`。
- `ffmpeg.exe` 实际 SHA-256：`3256173f3f8bffd7df12227c68adf68025edb1832273a9530688a7bb1ed8edec`。
- `ffprobe.exe` 实际 SHA-256：`f0d36ecbbdd3bcfac3efa078c96c7271c2e68b3810595552ac3b7f17e9a65c52`。
- 构建页标明工具为 GPLv3；编码器仅是本机制作工具，不打包进游戏。做了发布方 SHA-256 比对，没有执行 PGP 验签，也不把哈希匹配称为完整签名验证。

## 三处实际画面异常

| 范围 | 实际制作方式 | 成片检查 |
| --- | --- | --- |
| 0–4 秒 | 嘴形采用实际音轨音量包络，但驱动延后 0.46 秒；25fps 下显示按帧量化。后段不额外施加该延时 | 0.52 秒音轨已有语音而嘴形闭合，0.96 秒嘴形张开；实际解码音轨与源音轨相关系数约 0.99997。参数与逐帧轨迹见 motion-cues.csv；不是逐音素鉴定 |
| 4.15–7.90 秒 | 头部左右转动并改变面部特征位置；右侧脸缘局部条带随时间变形、偏移 | 实际 4.80/5.04 秒解码局部可见轮廓不稳定；没有只在检测页写异常 |
| 8–12 秒 | 脸部左明右暗，露出的颈部左暗右明，方向相反，进出段落有短过渡 | 9.44 秒成片可见脸/颈方向差异；初稿发型遮挡颈部后已调整发型、重编码并复核最终版本 |

这三项是人工制作的矢量教学模拟，并非真实换脸算法的检测输出，也不声称对任何真人、真实会议或真实媒体作了鉴定。[关键帧总览](verification/contact-sheet.jpg)和[局部检查图](verification/anomaly-detail.png)均来自最终 MP4 的实际解码帧。

## 字幕与实际时间线

先生成四段实际语音，以绝对幅度阈值 140/32768 检出活动边界，保留极短静音余量后放入制作时间线；再以实测边界前留约 60ms、后留约 120ms生成 SRT。最终又从 AAC 解码音轨核对活动范围是否被每段字幕覆盖，没有照搬原案拟定的 0–4/4–8/8–12/12–16 时码。

| 段落 | 最终 SRT 起止 | 最终 AAC 实测活动范围（秒，约） |
| --- | --- | --- |
| 1 | 00:00:00,280–00:00:03,814 | 0.340–3.693 |
| 2 | 00:00:04,400–00:00:06,882 | 4.460–6.761 |
| 3 | 00:00:08,320–00:00:10,771 | 8.380–10.651 |
| 4 | 00:00:12,000–00:00:15,229 | 12.060–15.108 |

四段均在 16 秒内、递增且不重叠；工程 SRT 与文档副本逐字节一致。文字稿、原始语音边界、排布和字幕范围见 `meeting-transcript.txt`、`subtitle-timing.csv`。阈值核对是语音活动覆盖检查，不是语音识别或人工逐字听审；本轮没有声称完成听音人工审校。

## 复现与未验证边界

在仓库根目录，使用现有 Python（Pillow/NumPy）、PowerShell 和已校验的便携工具：

```powershell
& ./docs/media/S3-B/synthesize.ps1 -WorkTemp <自己的任务临时目录>
& <Python路径> ./docs/media/S3-B/render_meeting.py --ffmpeg <ffmpeg完整路径> --work-temp <自己的任务临时目录>
& <Python路径> ./docs/media/S3-B/verify_meeting.py --ffmpeg <ffmpeg完整路径> --ffprobe <ffprobe完整路径> --work-temp <自己的任务临时目录>
```

生成脚本会覆盖本任务自己的输出，复现前应先确认没有他人修改。不同 Windows 声音/库版本可能使配音和视频哈希改变，届时必须重新测量字幕、检查异常并更新 manifest，不能沿用本轮哈希。

已验证：真实 MP4 容器、视频/音轨编码及长度、完整解码退出码 0、4 字幕范围、哈希和实际关键帧。未验证：Unity VideoClip 导入、meta 被编辑器采纳、播放器音画表现、字幕层接法、后台中断、Android 真机和 APK。没有启动 Unity、编译或运行工程测试。
