# 说明：在临时目录编译 C# 程序集与 Java 插件，执行独立检查；不启动 Unity 或生成 APK。
param([string]$EditorData = 'E:\unity\2022.3.61t10\Editor\Data')
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('zhijian-apk-checks-' + [guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($outputRoot)
$dotnet = Join-Path $EditorData 'NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $EditorData 'DotNetSdkRoslyn/csc.dll'
$mono = Join-Path $EditorData 'MonoBleedingEdge/bin/mono.exe'
$sourcesRoot = Join-Path $projectRoot 'UnityProject/Assets/_Project/Scripts'
$references = @('-r:' + (Join-Path $EditorData 'NetStandard/ref/2.1.0/netstandard.dll'))
$engineReferences = @(Get-ChildItem -LiteralPath (Join-Path $EditorData 'Managed/UnityEngine') -Filter '*.dll' | ForEach-Object { '-r:' + $_.FullName })
$built = @{}
foreach ($layer in @('Domain','Application','Presentation','Infrastructure','UnityUI','Bootstrap')) {
    $files = @(Get-ChildItem -LiteralPath (Join-Path $sourcesRoot $layer) -Filter '*.cs' -Recurse | ForEach-Object FullName)
    $target = Join-Path $outputRoot ('Zhijian.' + $layer + '.dll')
    $argsList = @('-nologo','-noconfig','-nostdlib+','-langversion:9','-nowarn:0649','-target:library',('-out:' + $target)) + $references
    foreach ($name in $built.Keys) { $argsList += '-r:' + $built[$name] }
    if ($layer -in @('Infrastructure','UnityUI','Bootstrap')) { $argsList += $engineReferences }
    if ($layer -in @('UnityUI','Bootstrap')) {
        foreach ($name in @('Unity.TextMeshPro','UnityEngine.UI')) {
            $argsList += '-r:' + (Join-Path $projectRoot ('UnityProject/Library/ScriptAssemblies/' + $name + '.dll'))
        }
    }
    & $dotnet $compiler @argsList @files
    if ($LASTEXITCODE -ne 0) { throw ('C# compilation failed: ' + $layer) }
    $built[$layer] = $target
}
# 另编译 Android 条件分支，避免只检查编辑器分支。
$androidTarget = Join-Path $outputRoot 'Zhijian.Infrastructure.Android.dll'
$argsList = @('-nologo','-noconfig','-nostdlib+','-langversion:9','-nowarn:0649','-target:library','-define:UNITY_ANDROID',('-out:' + $androidTarget)) + $references + $engineReferences
foreach ($name in @('Domain','Application')) { $argsList += '-r:' + $built[$name] }
$files = @(Get-ChildItem -LiteralPath (Join-Path $sourcesRoot 'Infrastructure') -Filter '*.cs' -Recurse | ForEach-Object FullName)
& $dotnet $compiler @argsList @files
if ($LASTEXITCODE -ne 0) { throw 'Android C# branch compilation failed.' }
$testTarget = Join-Path $outputRoot 'ApkInspectionChecks.exe'
$argsList = @('-nologo','-noconfig','-nostdlib+','-langversion:9','-target:exe',('-out:' + $testTarget)) + $references
foreach ($name in @('Domain','Application','Presentation')) { $argsList += '-r:' + $built[$name] }
& $dotnet $compiler @argsList (Join-Path $PSScriptRoot 'ApkInspectionChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Check harness compilation failed.' }
& $mono $testTarget
if ($LASTEXITCODE -ne 0) { throw 'C# checks failed.' }
$javac = Join-Path $EditorData 'PlaybackEngines/AndroidPlayer/OpenJDK/bin/javac.exe'
$androidJar = Join-Path $EditorData 'PlaybackEngines/AndroidPlayer/SDK/platforms/android-33/android.jar'
$javaOutput = Join-Path $outputRoot 'java'
[void][System.IO.Directory]::CreateDirectory($javaOutput)
$native = Join-Path $projectRoot 'UnityProject/Assets/Plugins/Android/ZhijianInspection.androidlib/src/com/zhijian/inspection/ApkInspectionActivity.java'
& $javac '-J-Duser.language=en' '-J-Duser.country=US' -encoding UTF-8 -source 8 -target 8 -classpath $androidJar -d $javaOutput $native
if ($LASTEXITCODE -ne 0) { throw 'Java compilation failed.' }
Write-Output ('PASS C# assemblies, Android C# branch, independent checks, Java against API 33. Outputs: ' + $outputRoot)
