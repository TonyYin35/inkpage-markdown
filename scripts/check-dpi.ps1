$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path $projectRoot '.test-output'
$probe = Join-Path $testRoot 'DpiProbe.exe'
$app = Join-Path $projectRoot 'dist\InkPage\InkPage.exe'
$result = Join-Path $projectRoot 'dist\InkPage\.test-output\result.txt'
if (-not (Test-Path -LiteralPath $app)) { throw 'Run scripts/build.ps1 first.' }
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
& $compiler /nologo "/out:$probe" (Join-Path $projectRoot 'tests\DpiProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'DPI probe compilation failed.' }
& $probe $app
if ($LASTEXITCODE -ne 0) { throw 'Native integration test or PerMonitorV2 check failed.' }
if (-not (Test-Path -LiteralPath $result)) { throw 'Native integration result was not generated.' }
$report = Get-Content -LiteralPath $result -Raw -Encoding UTF8
if (-not $report.StartsWith('PASS:')) { throw 'Native integration result is not PASS.' }
Write-Output $report
