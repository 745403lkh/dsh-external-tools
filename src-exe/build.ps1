# Build dsh-external-tools.exe with the .NET Framework compiler that ships with Windows.
# No .NET SDK, DSH, Node, or npm is required.

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = Split-Path $here -Parent
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
  $framework = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319'
  $csc = Join-Path $framework 'csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
  throw 'csc.exe not found. Windows .NET Framework 4.x is required.'
}

$icon = Join-Path $here 'app.ico'
if (-not (Test-Path -LiteralPath $icon)) {
  $maker = Join-Path $here 'make-icon.ps1'
  if (-not (Test-Path -LiteralPath $maker)) {
    throw 'app.ico is missing and make-icon.ps1 was not found.'
  }
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $maker
}
if (-not (Test-Path -LiteralPath $icon)) {
  throw 'app.ico generation failed.'
}
$output = Join-Path $root 'dsh-external-tools.exe'
$references = @(
  'System.dll',
  'System.Core.dll',
  'System.Windows.Forms.dll',
  'System.Drawing.dll',
  'System.Web.Extensions.dll',
  'System.IO.Compression.dll',
  'System.IO.Compression.FileSystem.dll'
) | ForEach-Object { '/reference:' + (Join-Path $framework $_) }

$sources = @(Get-ChildItem -LiteralPath $here -Recurse -Filter '*.cs' -File | Sort-Object FullName | ForEach-Object {
  '"{0}"' -f $_.FullName
})

$arguments = @(
  '/nologo',
  '/target:winexe',
  '/platform:anycpu',
  '/optimize+',
  '/codepage:65001',
  ('/out:"{0}"' -f $output),
  ('/win32icon:"{0}"' -f $icon),
  ('/win32manifest:"{0}"' -f (Join-Path $here 'app.manifest'))
) + $references + $sources

& $csc $arguments
if ($LASTEXITCODE -ne 0) {
  throw "csc failed with exit code $LASTEXITCODE"
}
Write-Host "Built: $output" -ForegroundColor Green
Write-Host "Size : $((Get-Item -LiteralPath $output).Length) bytes" -ForegroundColor DarkGray
