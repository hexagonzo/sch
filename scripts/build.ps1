param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$data = Join-Path $GameDir 'data_sts2_windows_x86_64'
dotnet build "$PSScriptRoot/../sch.csproj" -c Release "-p:GameDataDir=$data"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$dest = Join-Path $GameDir 'mods/sch'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item "$PSScriptRoot/../bin/Release/net9.0/sch.dll" $dest -Force
Copy-Item "$PSScriptRoot/../mod_manifest.json" $dest -Force
Write-Host "Installed into $dest"
