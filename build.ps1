$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Windows x64 .NET Framework 4.x compiler is required.' }
New-Item -ItemType Directory -Force -Path build | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /out:build\SwiftEdgeFanControl.exe /win32manifest:app.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll src\*.cs
if ($LASTEXITCODE -ne 0) { throw 'App compilation failed' }
Copy-Item -LiteralPath vendor\LpcACPIEC.bin -Destination build\LpcACPIEC.bin -Force
& $compiler /nologo /target:exe /platform:x64 /out:build\PolicyTests.exe /reference:build\SwiftEdgeFanControl.exe tests\PolicyTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& .\build\PolicyTests.exe
if ($LASTEXITCODE -ne 0) { throw 'Policy tests failed' }
Write-Output 'Built build\SwiftEdgeFanControl.exe. Use --demo for hardware-free preview.'
