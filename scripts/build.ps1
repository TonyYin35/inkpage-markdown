param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sdkVersion = '1.0.4258.31'
$expectedHash = '56F7F4B8BF9AEE4B8EFEFBBDD4F67D5F74EBD1B100ED0806DA71BF76AF481AA9'
$dependencyRoot = Join-Path $projectRoot '.deps'
$package = Join-Path $dependencyRoot "webview2-$sdkVersion.zip"
$sdkRoot = Join-Path $dependencyRoot "webview2-$sdkVersion"
$outputRoot = Join-Path $projectRoot 'dist\InkPage'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 is required on Windows x64.' }
New-Item -ItemType Directory -Force -Path $dependencyRoot,$outputRoot | Out-Null
if (-not (Test-Path -LiteralPath $package)) {
    if ($Offline) { throw 'The pinned WebView2 SDK is not cached. Run once without -Offline.' }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $packageUrl = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$sdkVersion/microsoft.web.webview2.$sdkVersion.nupkg"
    Invoke-WebRequest -Uri $packageUrl -OutFile $package -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'WebView2 package SHA256 mismatch. Build aborted.'
}
Expand-Archive -LiteralPath $package -DestinationPath $sdkRoot -Force
$coreDll = Join-Path $sdkRoot 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$formsDll = Join-Path $sdkRoot 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$loaderDll = Join-Path $sdkRoot 'runtimes\win-x64\native\WebView2Loader.dll'
Copy-Item -LiteralPath $coreDll,$formsDll,$loaderDll -Destination $outputRoot -Force
$outputApp = Join-Path $outputRoot 'app'
New-Item -ItemType Directory -Force -Path $outputApp | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'app') -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $outputApp -Force
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.txt') -Destination $outputRoot -Force
$sourceRoot = Join-Path $projectRoot 'src'
$source = Join-Path $sourceRoot 'Program.cs'
$manifest = Join-Path $sourceRoot 'app.manifest'
$icon = Join-Path $outputApp 'inkpage.ico'
$outputExe = Join-Path $outputRoot 'InkPage.exe'
& $compiler /nologo /utf8output /codepage:65001 /target:winexe /platform:x64 /optimize+ "/win32manifest:$manifest" "/win32icon:$icon" "/out:$outputExe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/reference:$coreDll" "/reference:$formsDll" $source
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Close the running app and retry.' }
Copy-Item -LiteralPath (Join-Path $sourceRoot 'app.config') -Destination ($outputExe + '.config') -Force
Write-Output 'Build complete: dist/InkPage/InkPage.exe'
