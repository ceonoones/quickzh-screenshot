param([switch]$Tests)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$files = Get-ChildItem -LiteralPath $root -Filter '*.cs' | Select-Object -ExpandProperty FullName
if (-not $Tests) { $files = $files | Where-Object { [System.IO.Path]::GetFileName($_) -ne 'Tests.cs' } }
$engine = Join-Path $root 'engine.zip'
if (-not (Test-Path -LiteralPath $engine)) { $engine = Join-Path (Split-Path -Parent $root) 'engine-v3.zip' }
$target = if ($Tests) { Join-Path $root '验证.exe' } else { Join-Path $root '快译截图.exe' }
$entry = if ($Tests) { '/main:QuickZh.Tests' } else { '/main:QuickZh.Program' }
$kind = if ($Tests) { '/target:exe' } else { '/target:winexe' }
& $compiler /nologo /optimize+ /platform:x64 $kind $entry "/out:$target" "/win32manifest:$root\app.manifest" "/win32icon:$root\app.ico" "/resource:$engine,engine.zip" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $files
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Get-Item -LiteralPath $target | Select-Object Name,Length
