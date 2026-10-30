<!-- 说明：记录 S3-A 已实际制作的语音、收款图、虚构人物图及其检查与主窗口接入事项。 -->
# S3-A：语音与静图交付

日期：2026-10-19。实际路径：`E:\unity_project\zhijian-mobile-security`。依据 AGENTS.md、P3并行任务表、P3接口与交付契约、两案脚本、UI美术规范及公共素材台账制作。本轮只产出资源与制作资料，没有写播放器或业务代码，没有等待或引用其他媒体窗口产物。

## 实际资源

| 文件（共同前缀 `UnityProject/Assets/_Project/`） | 实测内容 | 字节数 | 待绑定 ID / 来源状态 |
| --- | --- | --- | --- |
| `Media/VoiceLoan/request.wav` | 真正 RIFF/WAVE，线性 PCM signed 16-bit little-endian，44100Hz，单声道，617400帧，14.000000秒 | 1234844 | `voice-loan.media.request` / AUD-001；普通本机慧慧TTS，`licenseStatus=pending` |
| `Media/VoiceLoan/payee.png` | 真正PNG，RGBA，1080×1080；杜禾、800元、教学标记；不可扫描图示，无真实收款载荷 | 73771 | `voice-loan.media.payee` / IMG-VOICE-PAYEE；原创SVG排版，`self-authored` |
| `Media/PhotoPermission/sample-photo.png` | 真正PNG，RGBA，960×1280；原创抽象人物，标明非真人，无证件数据 | 88291 | assetId=`photo-permission.asset.photo`；**当前无Domain mediaId，manifest为null**；`self-authored` |

SHA-256：

```text
request.wav      8ffd83d3a9083482f9f93446e6bbaf9a517966c60cf3ded31cd636548d9478e1
payee.png        912cf13d319dad8c35a9851bedfed8d54b7329c3ad1e91af16cd0047d77b3c6e
sample-photo.png 9e1e5138a560018e4a56e53deec00d7c3254c9517d58f92d4f4f79ada82907b3
```

三份媒体的实际格式、时长/尺寸、完整路径、哈希、文字稿和来源路径均保留在 [manifest.json](../media/S3-A/manifest.json)，不是只创建了文件名或改扩展名。

## 实际新增文件清单

共17个文件：

1. `UnityProject/Assets/_Project/Media/VoiceLoan/request.wav`
2. `UnityProject/Assets/_Project/Media/VoiceLoan/request.wav.meta`
3. `UnityProject/Assets/_Project/Media/VoiceLoan/payee.png`
4. `UnityProject/Assets/_Project/Media/VoiceLoan/payee.png.meta`
5. `UnityProject/Assets/_Project/Media/PhotoPermission/sample-photo.png`
6. `UnityProject/Assets/_Project/Media/PhotoPermission/sample-photo.png.meta`
7. `docs/media/S3-A/manifest.json`
8. `docs/media/S3-A/request-transcript.txt`
9. `docs/media/S3-A/provenance.md`
10. `docs/media/S3-A/inspection.json`
11. `docs/media/S3-A/sources/synthesize-voice.ps1`
12. `docs/media/S3-A/sources/build-audio.py`
13. `docs/media/S3-A/sources/build-images.cjs`
14. `docs/media/S3-A/sources/inspect-assets.py`
15. `docs/media/S3-A/sources/payee.svg`
16. `docs/media/S3-A/sources/sample-photo.svg`
17. 本文件 `docs/tasks/S3-A-语音与静图.md`

只给三份新增工程资源生成唯一.meta；本轮没有新增工程子目录，VoiceLoan/PhotoPermission共享父目录.meta保留原样。docs里的制作目录不属于Unity Assets，不生成Unity导入元数据。四份制作/检查脚本均有首行简略注释；SVG保留XML声明和职责说明。

## 声音与图像制作事实

普通语音为本机 Microsoft Huihui Desktop（zh-CN），通过Windows SAPI合成完整脚本文字，没有真人参考音、声纹、克隆模型或特定人物模仿。前段rate=-2、后段rate=-1，分段衔接/速度变化作为韵律示例；音高保持调时约1.08125倍后输出14秒。

成品“急用 / 八百块”之间停顿为 **3.761224–4.481224秒，共0.720秒**；4.121224秒附近切换轻微原创底噪。原稿4.8–5.5秒是制作指导，本轮没有照抄拟定时码。真实文字稿和成品段落时间见 [request-transcript.txt](../media/S3-A/request-transcript.txt)。口述未添加揭示答案的教学旁白，播放页仍应持续显示教学模拟标识。

底噪使用NumPy原创伪随机信号，没有外部音效素材。停顿两段实测RMS为-49.044/-42.965dBFS，频谱重心约2878.76/1123.54Hz；音量保持轻微。整段峰值-2.078dBFS、RMS -21.132dBFS，0个满幅削波采样。这些是波形测量，不是真伪鉴定。

静图直接编写原创SVG，再用已有sharp/Pango及项目已有的Noto Sans SC（SIL OFL 1.1）渲染为PNG。收款人和金额使用大字，整体暖白/墨绿配色；二维码区域只画交叉线图示，不能支付。人物由原创几何曲线构成，没有真人参考、证件数据或外部照片。未使用AI位图生成或下载图片。

工具：PowerShell7.6.5；SAPI DLL 5.3.30018.00；Python3.12.14、NumPy2.3.5、Pillow12.3.0；sharp0.35.5、libvips8.18.7、librsvg2.63.2、Pango1.58.2。来源、字体许可文件位置、处理参数与版本详见 [provenance.md](../media/S3-A/provenance.md) 和 [inspection.json](../media/S3-A/inspection.json)。工具均已存在，未下载/全局安装，未改PATH。中间片段只写入本任务专用 `%TEMP%\zhijian-S3-A-20261019-01`。

## 已实际执行的检查

- 按真实字节检查RIFF/WAVE头和PCM格式标签1，使用Python wave完整解码617400帧；44100Hz/16-bit/mono、14秒、字节数均由文件本身读取。
- 用Pillow分别verify并完整load两张PNG，尺寸与色彩模式由文件本身读取，确认实际像素内容；没有用其他格式改名。
- 两张PNG已在会话中目视检查：必要中文没有缺字、遮挡或裁切，收款信息可读，抽象人物没有真实身份信息。金额已明确为“800 元”。
- 对最终成品重新计算SHA-256，逐项核对manifest，图像durationSeconds=0，示例照片mediaId=null，未给照片补造Domain媒体门槛。
- 三份新增.meta的GUID与现有Assets元数据做只读去重检查；共享VoiceLoan/PhotoPermission父目录.meta哈希与制作前一致。未把其他窗口文件计入本轮交付。
- 检查制作脚本首行注释与源文件有效性；实际执行仅为媒体制作与文件检查，不是Unity工程测试或编译。

## 未验证事项与主窗口接入

1. **逐字人工听审、发音与实际试听体验尚未执行。** 已核对合成输入与脚本文字、完成真实文件解码/波形检查，不能把它写成人工试听通过。
2. **Microsoft普通TTS声音输出的对外分发条件尚未取得可核验依据。** 因此音频licenseStatus为pending；本机声库可用不等于已确认分发许可。两张图的self-authored状态亦不是主窗口的自动使用批准。本轮未伪造真人授权或声库许可。
3. **Unity导入、播放器实际完成回调、安卓真机播放/缩放均未验证。** 新meta提供稳定GUID与导入提示，不声称引擎已接受或资源已接线。
4. 主窗口汇合后复核manifest与来源，将WAV/Sprite绑定到 `voice-loan.media.request`、`voice-loan.media.payee`。示例照片按独立assetId用于指定照片范围演示，不能擅自给案件三增加音视频或检测门槛。
5. 主窗口负责更新公共素材台账、CaseAsset引用及许可/开放标志，确认TTS输出条件与导入/播放后再决定批准。S3-A未修改这些公共文件，不假定已有approvedForUse/trainingEnabled。
6. 未写播放器、C#或业务JSON，未改AppRoot、Packages、ProjectSettings、asmdef、公共台账、其他窗口文件；没有启动Unity/Hub、编译、运行工程测试、提交、推送或操作其他窗口Git状态。
