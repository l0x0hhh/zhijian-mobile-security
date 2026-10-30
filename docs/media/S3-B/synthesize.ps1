# 说明：独立使用本机普通中文 SAPI 合成四句会议配音，仅写入 S3-B 专属临时目录。
param([string]$WorkTemp = (Join-Path ([System.IO.Path]::GetTempPath()) 'zhijian-S3-B-20261019'))
$ErrorActionPreference = 'Stop'
[System.IO.Directory]::CreateDirectory($WorkTemp) | Out-Null
$sentences = @(
    '今天临时加个会，用轻会助手参加。',
    '下载链接已经发到聊天里了。',
    '装好后打开短信和通讯录。',
    '我们马上开始，先把工具准备好。'
)
$voice = New-Object -ComObject SAPI.SpVoice
$token = @($voice.GetVoices() | Where-Object { $_.GetDescription() -eq 'Microsoft Huihui Desktop - Chinese (Simplified)' })[0]
if ($null -eq $token) { throw '本机普通中文声音不可用，未改用克隆或网络声音。' }
$voice.Voice = $token
$voice.Rate = 1
$voice.Volume = 100
$voice.AllowAudioOutputFormatChangesOnNextSet = $false
for ($index = 0; $index -lt $sentences.Count; $index++) {
    $stream = New-Object -ComObject SAPI.SpFileStream
    $stream.Format.Type = 38 # SAPI SAFT48kHz16BitMono，输出仍须从实际 WAV 核实。
    $filePath = Join-Path $WorkTemp ('speech-' + ($index + 1) + '.wav')
    try {
        $stream.Open($filePath, 3, $false)
        $voice.AudioOutputStream = $stream
        [void]$voice.Speak($sentences[$index])
    } finally {
        $stream.Close()
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($stream)
    }
    Write-Output ('SYNTHESIZED ' + $filePath)
}
Write-Output ('VOICE ' + $token.GetDescription())
Write-Output ('TOKEN ' + $token.Id)
[void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($voice)
