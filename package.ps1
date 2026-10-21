$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$stage = Join-Path $PSScriptRoot 'build\package'
New-Item -ItemType Directory -Force -Path $stage,downloads | Out-Null
Copy-Item -LiteralPath build\SwiftEdgeFanControl.exe,build\LpcACPIEC.bin,README.md,LICENSE -Destination $stage -Force
Copy-Item -LiteralPath docs,vendor -Destination $stage -Recurse -Force
Compress-Archive -Path "$stage\*" -DestinationPath downloads\SwiftEdgeFanControl-win-x64.zip -Force
$files = @('downloads\SwiftEdgeFanControl-win-x64.zip','build\SwiftEdgeFanControl.exe','build\LpcACPIEC.bin')
$lines = foreach ($file in $files) { $hash=Get-FileHash -LiteralPath $file -Algorithm SHA256; "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $file)" }
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'downloads\SHA256SUMS.txt'),$lines)
Write-Output 'Packaged downloads\SwiftEdgeFanControl-win-x64.zip'
