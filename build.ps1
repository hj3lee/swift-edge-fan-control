param([switch]$Development)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Windows x64 .NET Framework 4.x compiler is required.' }
$out = if ($Development) { 'build\development' } else { 'build\release' }
New-Item -ItemType Directory -Force -Path $out | Out-Null
$sources = @(Get-ChildItem src\*.cs | ForEach-Object FullName)
$options = @()
if ($Development) { $sources += (Resolve-Path tests\Preview.cs).Path; $options += '/define:DEVELOPMENT' }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$out\SwiftEdgeFanControl.exe" /win32manifest:app.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll @options @sources
if ($LASTEXITCODE -ne 0) { throw 'App compilation failed' }
Copy-Item -LiteralPath vendor\LpcACPIEC.bin -Destination "$out\LpcACPIEC.bin" -Force
& $compiler /nologo /target:exe /platform:x64 "/out:$out\PolicyTests.exe" "/reference:$out\SwiftEdgeFanControl.exe" tests\PolicyTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& ".\$out\PolicyTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Policy tests failed' }
Write-Output "Built $out\SwiftEdgeFanControl.exe. Preview flags exist only in -Development builds."
