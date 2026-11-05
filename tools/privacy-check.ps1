param([string]$Path = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
& python (Join-Path $PSScriptRoot 'privacy_check.py') $Path
if ($LASTEXITCODE -ne 0) { throw 'Publication privacy check failed.' }
