$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$stage = Join-Path $PSScriptRoot ('build\package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage,downloads | Out-Null
Copy-Item -LiteralPath build\release\SwiftEdgeFanControl.exe,build\release\LpcACPIEC.bin,LICENSE -Destination $stage -Force
Copy-Item -LiteralPath vendor -Destination $stage -Recurse -Force
Remove-Item -LiteralPath (Join-Path $stage 'vendor\LpcACPIEC.bin')
Copy-Item -LiteralPath docs\QUICK_START.txt -Destination $stage -Force
try { Compress-Archive -Path "$stage\*" -DestinationPath downloads\SwiftEdgeFanControl-win-x64.zip -Force }
finally { Remove-Item -LiteralPath $stage -Recurse -Force }
$files = @('downloads\SwiftEdgeFanControl-win-x64.zip','build\release\SwiftEdgeFanControl.exe','build\release\LpcACPIEC.bin')
$lines = foreach ($file in $files) { $hash=Get-FileHash -LiteralPath $file -Algorithm SHA256; "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $file)" }
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'downloads\SHA256SUMS.txt'),$lines)
Write-Output 'Packaged downloads\SwiftEdgeFanControl-win-x64.zip'
