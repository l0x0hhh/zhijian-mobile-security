# 说明：仅用本机普通慧慧中文语音生成虚构教学片段，不使用真人参考音或克隆模型。
param([Parameter(Mandatory=$true)][string]$WorkDirectory)
$ErrorActionPreference='Stop'
$s3Work=[IO.Path]::GetFullPath($WorkDirectory)
$s3Temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if(-not $s3Work.StartsWith($s3Temp,[StringComparison]::OrdinalIgnoreCase) -or
    -not (Split-Path $s3Work -Leaf).StartsWith('zhijian-S3-A-')) { throw '中间文件只能写入本任务自己的 Temp 子目录。' }
[IO.Directory]::CreateDirectory($s3Work)|Out-Null
$s3Voice=New-Object -ComObject SAPI.SpVoice
$s3Voices=$s3Voice.GetVoices()
$s3Token=$null
for($s3Index=0;$s3Index -lt $s3Voices.Count;$s3Index++) {
    $s3Candidate=$s3Voices.Item($s3Index)
    if($s3Candidate.GetDescription() -eq 'Microsoft Huihui Desktop - Chinese (Simplified)') {$s3Token=$s3Candidate;break}
}
if($null -eq $s3Token) {throw '未发现目标普通中文语音；不得自动改用他人声音。'}
$s3Voice.Voice=$s3Token
$s3Voice.Volume=100
$s3Segments=@(
    @{file='01-intro.wav';text='是我，林澈。';rate=-2},
    @{file='02-urgent.wav';text='我这边急用';rate=-2},
    @{file='03-amount.wav';text='八百块。';rate=-2},
    @{file='04-payee.wav';text='收款码发你了，你先转过去。';rate=-1},
    @{file='05-callback.wav';text='现在不方便接电话，晚点跟你说。';rate=-1}
)
foreach($s3Segment in $s3Segments) {
    $s3Path=Join-Path $s3Work $s3Segment.file
    if(Test-Path -LiteralPath $s3Path){throw "拒绝覆盖已有片段：$s3Path"}
    $s3Stream=New-Object -ComObject SAPI.SpFileStream
    try {
        $s3Stream.Format.Type=34 # SAPI SAFT44kHz16BitMono。
        $s3Stream.Open($s3Path,3,$false)
        $s3Voice.AudioOutputStream=$s3Stream
        $s3Voice.Rate=$s3Segment.rate
        $s3Voice.Speak($s3Segment.text,0)|Out-Null
    } finally {$s3Stream.Close()}
}
$s3Sapi=Join-Path $env:WINDIR 'System32\Speech\Common\sapi.dll'
$s3Info=[ordered]@{
    voice=$s3Token.GetDescription();tokenId=$s3Token.Id;language=$s3Token.GetAttribute('Language');gender=$s3Token.GetAttribute('Gender');
    api='Windows SAPI.SpVoice';sapiFileVersion=([Diagnostics.FileVersionInfo]::GetVersionInfo($s3Sapi)).FileVersion;
    os=[Environment]::OSVersion.VersionString;powershell=$PSVersionTable.PSVersion.ToString();
    sapiFormatType=34;sampleRate=44100;channels=1;bitsPerSample=16;segments=$s3Segments
}
$s3InfoPath=Join-Path $s3Work 'voice-source.json'
if(Test-Path -LiteralPath $s3InfoPath){throw '语音来源记录已存在，停止覆盖。'}
[IO.File]::WriteAllText($s3InfoPath,($s3Info|ConvertTo-Json -Depth 6)+"`n",[Text.UTF8Encoding]::new($false))
$s3Info|ConvertTo-Json -Depth 6
