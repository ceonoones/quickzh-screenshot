$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $root 'resources/ocr-bundle.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$target = Join-Path $root 'src/engine.zip'
if (Test-Path -LiteralPath $target) {
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $manifest.sha256) {
        Write-Output 'OCR resources verified.'
        return
    }
    throw 'OCR resource checksum mismatch. Remove the incorrect local engine.zip before retrying.'
}
$temporary = "$target.download"
try {
    Invoke-WebRequest -Uri $manifest.url -OutFile $temporary
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.sha256) {
        throw 'Downloaded OCR resource checksum mismatch.'
    }
    Move-Item -LiteralPath $temporary -Destination $target
    Write-Output 'OCR resources downloaded and verified.'
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
