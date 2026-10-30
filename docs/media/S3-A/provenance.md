<!-- 说明：记录 S3-A 三份真实媒体的原创来源、普通合成语音、工具版本与尚未确认的许可。 -->
# S3-A 来源与制作记录

制作日期：2026-10-19。作者/操作者：刘总授权的 CC 制作会话。仓库：`E:\unity_project\zhijian-mobile-security`。本记录只涉及 S3-A 独占资源，未读取或依赖其他媒体窗口的交付物。

## 实际素材与来源

| 资产 | 来源与制作过程 | 许可状态 |
| --- | --- | --- |
| AUD-001 / request.wav | 文案取自 `docs/cases/voice-loan.md` §3.1。使用本机 Microsoft Huihui Desktop 普通中文 TTS，未输入真人录音、照片、声纹或任何人物参考；没有训练/克隆模型，也不宣称合成声属于真人林澈。分段后保持音高调时至14秒，原创生成轻微底噪 | `pending`：已记录本机声库来源与版本，但没有取得可核验的“该声音输出对外再分发”条款或单独授权；系统可用不等于本记录确认了分发许可。未伪造许可 |
| IMG-VOICE-PAYEE / payee.png | CC 本次直接编写原创 SVG 几何与排版；杜禾、800元、教学说明均来自虚构脚本。方框交叉线只是“不可扫描”图示，未创建二维码矩阵、二维码载荷、真实账户、收款链接、支付品牌或电话号码 | `self-authored`：自主绘制/排版，字体来源已具备 OFL 记录；没有第三方图片输入 |
| photo-permission.asset.photo / sample-photo.png | CC 本次直接编写原创抽象人物 SVG；人脸、头发、衣服均为简单矢量曲线。无真实人物参考、照片、证件号码、姓名、签名、证件徽章或肖像映射 | `self-authored`：自主绘制/排版，字体来源已具备 OFL 记录；没有第三方照片输入 |

两张图片采用代码原生矢量绘制与排版，再导出实际 PNG，没有调用位图 AI 生成、下载图片或替换扩展名。保留 `sources/payee.svg`、`sources/sample-photo.svg` 和制作脚本供审查。未替整个项目选择或变更开源许可证，`self-authored` 只是来源事实，不等于主窗口已经批准使用。

## 字体来源

使用工程已有的 `UnityProject/Assets/_Project/Art/Fonts/NotoSansSC-Regular.otf`，没有修改或复制字体二进制：

- 名称：Noto Sans SC Regular；许可：SIL Open Font License 1.1。
- 原始记录与全文：同目录 `SOURCE.md`、`OFL.txt`。
- 上游固定提交：`f8d157532fbfaeda587e826d4cd5b21a49186f7c`。
- [Noto 上游字体](https://raw.githubusercontent.com/notofonts/noto-cjk/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/SubsetOTF/SC/NotoSansSC-Regular.otf)；[OFL 原文](https://raw.githubusercontent.com/notofonts/noto-cjk/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/LICENSE)。本轮依据仓库已有文件读取，没有重新下载。
- 实测 SHA-256：`faa6c9df652116dde789d351359f3d7e5d2285a2b2a1f04a2d7244df706d5ea9`。
- PNG 中文通过 Pango 显式指定该 OTF 渲染。SVG 保留中文文字与 Noto Sans SC 字体声明；源文件在别的环境复现时须提供同字体。字体许可全文说明其要求不适用于用字体创作的普通文档；本记录仍保留字体来源，未把字体本身改成项目自有许可。

## 实际语音来源与处理

- 普通声音：`Microsoft Huihui Desktop - Chinese (Simplified)`，语言属性 `804`（zh-CN），Female。
- 本机 token：`HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech\Voices\Tokens\TTS_MS_ZH-CN_HUIHUI_11.0`。
- API：Windows `SAPI.SpVoice`；`sapi.dll` 文件版本 `5.3.30018.00 (WinBuild.160101.0800)`。
- 操作系统读取值：`Microsoft Windows NT 10.0.26200.0`；PowerShell `7.6.5`。以上是实际环境事实，不是声库输出许可证明。
- 输出请求格式：SAPI format 34（44.1kHz、16-bit、mono），最终 WAV 另行读文件头确认。
- 前三段 SAPI rate=-2，后两段 rate=-1，采用同一普通声音，保留分段韵律衔接变化。NumPy 相位声码器只进行约1.08125倍时长调整，Hann窗2048、hop256，未把语音变成任何目标人物的声音。
- “我这边急用 / 八百块”之间实际保留 **3.761224–4.481224秒** 的0.720秒句中停顿。制作指导原拟定4.8–5.5秒，本次按最终采样位置记录，没有把拟定时码冒充成品时码。
- 底噪为本次用 NumPy 伪随机种子20261019生成的原创滤波噪声，没有引入外部声音素材。4.121224秒附近用16ms过渡改变噪声强度/频谱，保持轻微程度，不使用惊吓效果。
- 停顿内两段实际窗口 RMS 为 **-49.044 / -42.965 dBFS**，频谱重心约 **2878.76 / 1123.54 Hz**。这是音频信号测量，不是真假鉴定或机器学习检测结果。
- 实测峰值-2.078 dBFS、整段RMS -21.132 dBFS，0个满幅削波采样。最终14.000000秒、617400采样帧、44.1kHz/16-bit/mono线性PCM。
- `request-transcript.txt` 记录完整口述、各段实际时码与异常位置。角色口述没有追加“这是克隆语音”等答案；播放页的教学模拟提示仍需主窗口提供。

已核对合成输入与源文案、真实WAV文件头、完整PCM解码和波形数值；**逐字人工听审、普通话发音体验、Unity导入与实际播放尚未执行**。不把这些标为通过。声音输出的分发条件在主窗口汇合前仍应核对；不能据此将 approvedForUse/trainingEnabled 改为 true。

## 本机既有工具版本

| 工具 | 实际版本 / 使用 |
| --- | --- |
| Windows SAPI | 文件版本如上，普通慧慧本机声库 |
| Python | 3.12.14，Codex既有依赖运行时 |
| NumPy | 2.3.5，调时、原创底噪、波形测量 |
| Pillow | 12.3.0，只做PNG真实解码和尺寸/像素检查 |
| sharp | 0.35.5，SVG/Pango文字合成与PNG编码 |
| libvips / librsvg / Pango | 8.18.7 / 2.63.2 / 1.58.2，来自同一既有sharp运行时 |

没有下载或全局安装工具，没有更改PATH、系统语音配置、注册表、Packages或ProjectSettings。工具来自本机既有Codex依赖；没有把其二进制放进仓库。中间WAV片段和工具过程记录位于本任务专用 `%TEMP%\zhijian-S3-A-20261019-01`，未执行清理或移动其他文件。

## 制作源文件与复现

- `sources/synthesize-voice.ps1`：枚举/选择指定普通语音，输出5段原始PCM和来源信息至显式指定的本任务Temp目录。
- `sources/build-audio.py`：合成14秒WAV、句中停顿、原创底噪和实际生产参数；默认拒绝覆盖已有成品。
- `sources/build-images.cjs`：生成两份原创SVG和两张PNG；文字用显式字体文件渲染，不依赖系统字体替代。默认拒绝覆盖；`--replace-own`仅可重写脚本内固定的本任务两份源图/成品。
- `sources/inspect-assets.py`：只读解析三份媒体，生成manifest、检查记录和实际文字稿；不是Unity工程测试，不启动引擎，默认拒绝覆盖已有交付记录。

命令中的Python、Node和node_modules使用本机已存在的运行时实际路径，未写入全局PATH。依次运行语音脚本、音频制作、图像制作和媒体检查；全套真实参数/渲染框/版本保留在 `inspection.json`。重新制作需使用新的本任务Temp目录，已有成品默认不会被静默覆盖。

## 当前检查与尚未验证

两张PNG已完整解码并目视检查：中文可读、无裁切，收款图没有可用二维码或账号；抽象人物没有真实身份资料。三份实际成品SHA-256、尺寸、字节数与音频参数见 `manifest.json` 和 `inspection.json`。

只制作资源及其元数据，没有写播放器、业务C#、场景或绑定，没有改CaseAsset、三案JSON、公共素材台账或批准标志。共享Media/VoiceLoan/PhotoPermission父目录meta保留原样。未启动Unity/Hub、编译、运行工程测试、提交、推送或操作其他窗口Git状态。主窗口仍须审查来源、确认普通TTS输出分发条件、导入并绑定固定mediaId，之后再单独记录实际播放与真机结果。
