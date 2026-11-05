param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'prepare.ps1')
& (Join-Path $root 'src/build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& python (Join-Path $PSScriptRoot 'package.py') $Version
if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
