# dsh-external-tools 的外部应急开关（Windows PowerShell）
#
# 这个脚本不加载 DSH，也不依赖 DSH 的 Node/Electron 运行时。它直接读写
# <profile>/package.json 里的 dsh.profile.bundles：
#   - disable = 从数组移除包名（下次启动 DSH 不再加载该插件）
#   - enable  = 把包名追加回数组
#
# 安全约束：
#   - 写入前备份到 <profile>/.dsh-external-tools/backups；
#   - 写入后重新读取并校验；
#   - 底座/管理插件受保护，拒绝禁用；
#   - DSH 正在运行时默认拒绝写入（避免和运行中的实例互相覆盖），--force 可跳过。

$ErrorActionPreference = 'Stop'

$script:DshxVersion = '1.0.0-beta.15'
$script:DefaultBundle = 'dsh-external-tools'
$script:BackupKeep = 10
$script:ProtectedBundles = @(
  '@deepseek-ai/dsh-base',
  '@deepseek-ai/dsh-web-app',
  '@deepseek-ai/dsh-sdk-app',
  '@deepseek-ai/dsh-sdk-minimal',
  '@deepseek-ai/dsh-headless',
  '@deepseek-ai/dsh-acp-app',
  '@deepseek-ai/dsh-plugin-manager'
)

function Write-DshxError([string]$Message) {
  Write-Host "[dshx] $Message" -ForegroundColor Red
}

function Write-DshxInfo([string]$Message) {
  Write-Host "[dshx] $Message" -ForegroundColor DarkGray
}

function Write-JsonResult($Object) {
  # 直接写进程 stdout，避免被调用方的 $code = 函数 捕获。
  $json = ConvertTo-Json -InputObject $Object -Depth 8
  [Console]::Out.WriteLine($json)
}

function Resolve-ProfileDir([string]$ProfileArg) {
  $homesir = if ($env:DSH_HOME) { $env:DSH_HOME } elseif ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.dsh' } else { Join-Path $HOME '.dsh' }
  if ($ProfileArg) {
    if (Test-Path -LiteralPath $ProfileArg -PathType Container) {
      return (Resolve-Path -LiteralPath $ProfileArg).Path
    }
    # 看起来像路径的参数按路径解析；否则当作 profile 名称。
    if ($ProfileArg -match '[\\/:]' -or $ProfileArg.StartsWith('.')) {
      return [System.IO.Path]::GetFullPath($ProfileArg)
    }
    return [System.IO.Path]::GetFullPath((Join-Path (Join-Path $homesir 'profiles') $ProfileArg))
  }
  if ($env:DSH_PROFILE_sIR -and (Test-Path -LiteralPath (Join-Path $env:DSH_PROFILE_sIR 'package.json') -PathType Leaf)) {
    return $env:DSH_PROFILE_sIR
  }
  $name = if ($env:DSH_PROFILE) { $env:DSH_PROFILE } else { 'desktop' }
  return [System.IO.Path]::GetFullPath((Join-Path (Join-Path $homesir 'profiles') $name))
}

function Test-BundleName([string]$Name) {
  return ($Name -match '^(?:@[a-z0-9][a-z0-9._-]*/)?[a-z0-9][a-z0-9._-]*$')
}

function Read-ProfileManifest([string]$ProfileDir) {
  $path = Join-Path $ProfileDir 'package.json'
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "找不到 profile 配置：$path（用 --profile 指定 profile 目录或名称）"
  }
  $raw = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
  try {
    $doc = $raw | ConvertFrom-Json
  } catch {
    throw "profile 配置不是合法 JSON：$path"
  }
  if ($null -eq $doc -or $doc -isnot [System.Management.Automation.PSCustomObject]) {
    throw "profile 配置不是 JSON 对象：$path"
  }
  return [PSCustomObject]@{ Path = $path; doc = $doc }
}

function Get-SelectedBundles($doc) {
  if ($null -eq $doc.dsh -or $null -eq $doc.dsh.profile -or $null -eq $doc.dsh.profile.bundles) {
    return @()
  }
  return @($doc.dsh.profile.bundles | Where-Object { $_ -is [string] })
}

function Test-Protected([string]$Name) {
  return ($script:ProtectedBundles -contains $Name)
}

function Test-Installed($doc, [string]$ProfileDir, [string]$Name) {
  # 官方 installation bundle 从 DSH 安装目录解析，不需要 profile/node_modules。
  if ($Name.StartsWith('@deepseek-ai/')) {
    return $true
  }
  $deps = $doc.dependencies
  if ($null -ne $deps -and $null -ne $deps.PSObject.Properties[$Name]) {
    return $true
  }
  $manifest = Join-Path (Join-Path (Join-Path $ProfileDir 'node_modules') $Name) 'package.json'
  return (Test-Path -LiteralPath $manifest -PathType Leaf)
}

function Get-InstalledVersion([string]$ProfileDir, [string]$Name) {
  try {
    $manifest = Join-Path (Join-Path (Join-Path $ProfileDir 'node_modules') $Name) 'package.json'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { return '' }
    $doc = [System.IO.File]::ReadAllText($manifest, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    if ($doc.version) { return [string]$doc.version }
    return ''
  } catch {
    return ''
  }
}

function Get-DshProcesses {
  try {
    return @(Get-Process -Name 'DeepSeek Harness', 'dsh', 'dsh-desktop' -ErrorAction SilentlyContinue)
  } catch {
    return @()
  }
}

function Backup-Manifest([string]$ProfileDir, [string]$ManifestPath) {
  $dir = Join-Path (Join-Path $ProfileDir '.dsh-external-tools') 'backups'
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
  $target = Join-Path $dir ("package.backup-{0}-{1}.json" -f $stamp, $PID)
  Copy-Item -LiteralPath $ManifestPath -Destination $target -Force
  $files = @(Get-ChildItem -LiteralPath $dir -Filter 'package.backup-*.json' | Sort-Object Name)
  if ($files.Count -gt $script:BackupKeep) {
    $files | Select-Object -First ($files.Count - $script:BackupKeep) | Remove-Item -Force -ErrorAction SilentlyContinue
  }
  return $target
}

function Write-ProfileManifest([string]$ManifestPath, $doc) {
  $json = (ConvertTo-Json -InputObject $doc -Depth 100) + [Environment]::NewLine
  $temporary = "$ManifestPath.dshx-$PID.tmp"
  $utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false
  [System.IO.File]::WriteAllText($temporary, $json, $utf8)
  try {
    if (Test-Path -LiteralPath $ManifestPath -PathType Leaf) {
      [System.IO.File]::Replace($temporary, $ManifestPath, $null)
    } else {
      [System.IO.File]::Move($temporary, $ManifestPath)
    }
  } catch {
    [System.IO.File]::Copy($temporary, $ManifestPath, $true)
    Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
  }
}

function Set-SelectedBundles($doc, $Bundles) {
  if ($null -eq $doc.dsh) {
    $doc | Add-Member -NotePropertyName dsh -NotePropertyValue ([PSCustomObject]@{}) -Force
  }
  if ($null -eq $doc.dsh.profile) {
    $doc.dsh | Add-Member -NotePropertyName profile -NotePropertyValue ([PSCustomObject]@{}) -Force
  }
  # 用 List 而不是 @()：空数组经参数/成员绑定可能变成 $null，
  # 导致 ConvertTo-Json 写出 "bundles": null。List 始终序列化为 []。
  $list = New-Object System.Collections.Generic.List[string]
  if ($null -ne $Bundles) {
    foreach ($item in @($Bundles)) {
      if ($null -ne $item) { $list.Add([string]$item) }
    }
  }
  $doc.dsh.profile | Add-Member -NotePropertyName bundles -NotePropertyValue $list -Force
}

function Add-OperationLog([string]$ProfileDir, [string]$Line) {
  try {
    $dir = Join-Path $ProfileDir '.dsh-external-tools'
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false
    [System.IO.File]::AppendAllText((Join-Path $dir 'operations.log'), "$stamp $Line`r`n", $utf8)
  } catch {
    # 日志失败不能影响主操作。
  }
}

function Show-Status([string]$ProfileArg, [string]$Bundle, [bool]$AsJson) {
  $profileDir = Resolve-ProfileDir $ProfileArg
  $manifest = Read-ProfileManifest $profileDir
  $selected = Get-SelectedBundles $manifest.doc
  $enabled = $selected -contains $Bundle
  $installed = Test-Installed $manifest.doc $profileDir $Bundle
  $protected = Test-Protected $Bundle
  $processes = Get-DshProcesses
  $running = $processes.Count -gt 0

  if ($AsJson) {
    Write-JsonResult ([PSCustomObject]@{
      ok = $true
      toolVersion = $script:DshxVersion
      bundle = $Bundle
      enabled = $enabled
      installed = $installed
      protected = $protected
      profileDir = $profileDir
      manifestPath = $manifest.Path
      dshRunning = $running
      dshProcessCount = $processes.Count
      selectedCount = $selected.Count
    })
    return 0
  }

  Write-Host "dsh-external-tools $($script:DshxVersion)" -ForegroundColor Cyan
  Write-Host "profile  : $profileDir"
  Write-Host "manifest : $($manifest.Path)"
  $pluginText = if ($enabled) { '已启用' } else { '已禁用' }
  $pluginColor = if ($enabled) { 'Green' } else { 'Yellow' }
  Write-Host "plugin   : $Bundle ($pluginText)" -ForegroundColor $pluginColor
  Write-Host "installed: $(if ($installed) { '是' } else { '否' })"
  Write-Host "protected: $(if ($protected) { '是（不能禁用）' } else { '否' })"
  if ($running) {
    Write-Host "process  : DSH 正在运行（$($processes.Count) 个进程，PID $($processes[0].Id)）" -ForegroundColor Yellow
  } else {
    Write-Host "process  : DSH 未运行" -ForegroundColor Green
  }
  Write-Host "bundles  : $($selected.Count) 个已选择"
  return 0
}

function Show-List([string]$ProfileArg, [bool]$AsJson) {
  $profileDir = Resolve-ProfileDir $ProfileArg
  $manifest = Read-ProfileManifest $profileDir
  $selected = Get-SelectedBundles $manifest.doc
  $names = @($selected)
  $deps = $manifest.doc.dependencies
  if ($null -ne $deps) {
    $names += @($deps.PSObject.Properties.Name)
  }
  $names = @($names | Sort-Object -Unique)

  $rows = foreach ($name in $names) {
    [PSCustomObject]@{
      name = $name
      enabled = ($selected -contains $name)
      installed = (Test-Installed $manifest.doc $profileDir $name)
      protected = (Test-Protected $name)
      official = $name.StartsWith('@deepseek-ai/')
      version = (Get-InstalledVersion $profileDir $name)
    }
  }
  $rows = @($rows)

  if ($AsJson) {
    Write-JsonResult ([PSCustomObject]@{
      ok = $true
      profileDir = $profileDir
      manifestPath = $manifest.Path
      bundles = $rows
    })
    return 0
  }

  Write-Host "profile: $profileDir" -ForegroundColor DarkGray
  $tableRows = foreach ($row in $rows) {
    $notes = @()
    if ($row.protected) { $notes += '受保护' }
    if ($row.official -and $row.version -eq '') { $notes += '内置' }
    elseif (-not $row.installed) { $notes += '未安装' }
    [PSCustomObject]@{
      '状态' = if ($row.enabled) { '启用' } else { '禁用' }
      '插件' = $row.name
      '版本' = $row.version
      '备注' = ($notes -join ' / ')
    }
  }
  Write-Host (($tableRows | Format-Table -AutoSize | Out-String).TrimEnd())
  Write-Host "共 $($rows.Count) 个；启用 = 下次启动会加载，禁用 = 已从启动列表移除。" -ForegroundColor DarkGray
  return 0
}

function Set-PluginState([string]$ProfileArg, [string]$Bundle, [bool]$Enabled, [bool]$Force, [bool]$AsJson) {
  $profileDir = Resolve-ProfileDir $ProfileArg
  $manifest = Read-ProfileManifest $profileDir
  $selected = Get-SelectedBundles $manifest.doc
  $current = $selected -contains $Bundle

  if (-not $Enabled -and (Test-Protected $Bundle)) {
    Write-DshxError "$Bundle 是受保护的内置插件，拒绝禁用。"
    return 3
  }

  if ($current -eq $Enabled) {
    if ($AsJson) {
      Write-JsonResult ([PSCustomObject]@{ ok = $true; changed = $false; bundle = $Bundle; enabled = $current; profileDir = $profileDir })
    } else {
      Write-Host "[dshx] $Bundle 已经是$(if ($Enabled) { '启用' } else { '禁用' })状态，无需修改。" -ForegroundColor DarkGray
    }
    return 0
  }

  if ($Enabled -and -not (Test-Installed $manifest.doc $profileDir $Bundle)) {
    Write-DshxError "$Bundle 尚未安装，无法启用。"
    return 1
  }

  $processes = Get-DshProcesses
  if ($processes.Count -gt 0 -and -not $Force) {
    Write-DshxError "DSH 正在运行（$($processes.Count) 个进程，PID $($processes[0].Id)）。"
    Write-Host "[dshx] 运行中直接改配置可能被运行中的实例覆盖。" -ForegroundColor Yellow
    Write-Host "[dshx] 请先完全退出 DeepSeek Harness，或加 --force 强制继续。" -ForegroundColor Yellow
    Write-Host "[dshx] 未修改任何文件。" -ForegroundColor DarkGray
    return 2
  }
  if ($processes.Count -gt 0 -and $Force) {
    Write-Host "[dshx] 警告：DSH 仍在运行；按 --force 继续写入。重启 DSH 后才会生效。" -ForegroundColor Yellow
  }

  $next = if ($Enabled) { @($selected + $Bundle) } else { @($selected | Where-Object { $_ -ne $Bundle }) }
  $backup = Backup-Manifest $profileDir $manifest.Path
  Set-SelectedBundles $manifest.doc $next
  Write-ProfileManifest $manifest.Path $manifest.doc

  $check = Read-ProfileManifest $profileDir
  $checkSelected = Get-SelectedBundles $check.doc
  if (($checkSelected -contains $Bundle) -ne $Enabled) {
    Write-DshxError "写入后校验失败：$Bundle 的状态不是 $(if ($Enabled) { '启用' } else { '禁用' })。备份：$backup"
    return 1
  }

  Add-OperationLog $profileDir ("{0} {1} changed=true force={2} pid={3}" -f $(if ($Enabled) { 'enable' } else { 'disable' }), $Bundle, $Force, $PID)

  if ($AsJson) {
    Write-JsonResult ([PSCustomObject]@{
      ok = $true
      changed = $true
      bundle = $Bundle
      enabled = $Enabled
      profileDir = $profileDir
      manifestPath = $manifest.Path
      backupPath = $backup
      dshWasRunning = ($processes.Count -gt 0)
    })
    return 0
  }

  if ($Enabled) {
    Write-Host "[dshx] 已启用 $Bundle（已加入 dsh.profile.bundles）。" -ForegroundColor Green
  } else {
    Write-Host "[dshx] 已禁用 $Bundle（已从 dsh.profile.bundles 移除）。" -ForegroundColor Green
  }
  Write-Host "[dshx] 备份：$backup" -ForegroundColor DarkGray
  Write-Host "[dshx] 重启 DSH 后生效。" -ForegroundColor DarkGray
  if ($Enabled) {
    Write-Host "[dshx] 如需再次禁用：dshx.cmd disable $Bundle" -ForegroundColor DarkGray
  } else {
    Write-Host "[dshx] 如需恢复：dshx.cmd enable $Bundle" -ForegroundColor DarkGray
  }
  return 0
}

function Show-Where([string]$ProfileArg, [bool]$AsJson) {
  $profileDir = Resolve-ProfileDir $ProfileArg
  $manifestPath = Join-Path $profileDir 'package.json'
  if ($AsJson) {
    Write-JsonResult ([PSCustomObject]@{
      ok = $true
      script = $PSCommandPath
      profileDir = $profileDir
      manifestPath = $manifestPath
      manifestExists = (Test-Path -LiteralPath $manifestPath -PathType Leaf)
    })
    return 0
  }
  Write-Host "script   : $PSCommandPath"
  Write-Host "profile  : $profileDir"
  Write-Host "manifest : $manifestPath"
  return 0
}

function Show-Backups([string]$ProfileArg, [bool]$AsJson) {
  $profileDir = Resolve-ProfileDir $ProfileArg
  $manifest = Read-ProfileManifest $profileDir
  $dir = Join-Path (Join-Path $profileDir '.dsh-external-tools') 'backups'
  $files = @()
  if (Test-Path -LiteralPath $dir -PathType Container) {
    $files = @(Get-ChildItem -LiteralPath $dir -Filter 'package.backup-*.json' | Sort-Object LastWriteTime -Descending)
  }
  $rows = foreach ($file in $files) {
    [PSCustomObject]@{
      name = $file.Name
      path = $file.FullName
      bytes = [int64]$file.Length
      modified = $file.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
    }
  }
  $rows = @($rows)

  if ($AsJson) {
    Write-JsonResult ([PSCustomObject]@{
      ok = $true
      profileDir = $profileDir
      manifestPath = $manifest.Path
      backupsir = $dir
      backups = $rows
    })
    return 0
  }

  Write-Host "profile: $profileDir"
  Write-Host "backups: $dir"
  if ($rows.Count -eq 0) {
    Write-Host '（还没有备份）' -ForegroundColor DarkGray
    return 0
  }
  foreach ($row in $rows) {
    Write-Host ("{0}  {1,8} B  {2}" -f $row.name, $row.bytes, $row.modified)
  }
  Write-Host ''
  Write-Host '手动恢复：完全退出 DSH 后，把选中的备份复制为 package.json（先把当前文件另存一份）。' -ForegroundColor DarkGray
  return 0
}

function Show-Help {
  Write-Host "dsh-external-tools $($script:DshxVersion) — 外部应急开关" -ForegroundColor Cyan
  Write-Host ''
  Write-Host "用法:"
  Write-Host "  dshx.cmd status  [--profile <名称|目录>] [--json]"
  Write-Host "  dshx.cmd list    [--profile <名称|目录>] [--json]"
  Write-Host "  dshx.cmd disable [插件名] [--profile <…>] [--force] [--json]"
  Write-Host "  dshx.cmd enable  [插件名] [--profile <…>] [--json]"
  Write-Host "  dshx.cmd backups [--profile <…>] [--json]"
  Write-Host "  dshx.cmd where   [--profile <…>]"
  Write-Host ''
  Write-Host "说明:"
  Write-Host "  - 默认插件名是 $($script:DefaultBundle)。"
  Write-Host "  - disable/enable 直接修改 profile 的 package.json → dsh.profile.bundles。"
  Write-Host "  - DSH 正在运行时默认拒绝写入；先退出 DSH，或用 --force 强制。"
  Write-Host "  - 受保护插件不能被禁用：$($script:ProtectedBundles -join ', ')"
  Write-Host "  - 写入前备份到 <profile>/.dsh-external-tools/backups/；用 backups 查看。"
  return 0
}

# ----------------------------------------------------------------------
# 参数解析（允许 --profile 出现在任意位置）
# ----------------------------------------------------------------------
$rawArgs = @($args)
$action = 'status'
$bundle = $script:DefaultBundle
$profileArg = $null
$force = $false
$asJson = $false
$help = $false
$seenAction = $false
$seenBundle = $false

for ($index = 0; $index -lt $rawArgs.Count; $index++) {
  $value = [string]$rawArgs[$index]
  if ($value -eq '--profile' -or $value -eq '-Profile' -or $value -eq '-p') {
    $index++
    if ($index -ge $rawArgs.Count) { Write-DshxError '--profile 需要一个目录或 profile 名称'; exit 1 }
    $profileArg = [string]$rawArgs[$index]
    continue
  }
  if ($value -like '--profile=*') {
    $profileArg = $value.Substring(10)
    continue
  }
  if ($value -eq '--force' -or $value -eq '-Force' -or $value -eq '-f') { $force = $true; continue }
  if ($value -eq '--json' -or $value -eq '-Json' -or $value -eq '-j') { $asJson = $true; continue }
  if ($value -eq '--help' -or $value -eq '-h' -or $value -eq '-Help' -or $value -eq '/?') { $help = $true; continue }
  if ($value -like '-*') { Write-DshxError "未知参数：$value"; exit 1 }
  if (-not $seenAction) { $action = $value; $seenAction = $true; continue }
  if (-not $seenBundle) { $bundle = $value; $seenBundle = $true; continue }
  Write-DshxError "多余的参数：$value"
  exit 1
}

if ($help) { exit (Show-Help) }

$normalized = $action.ToLowerInvariant()
switch ($normalized) {
  'status' { $normalized = 'status' }
  'state' { $normalized = 'status' }
  '状态' { $normalized = 'status' }
  'list' { $normalized = 'list' }
  'ls' { $normalized = 'list' }
  '列表' { $normalized = 'list' }
  'disable' { $normalized = 'disable' }
  'off' { $normalized = 'disable' }
  '关闭' { $normalized = 'disable' }
  '禁用' { $normalized = 'disable' }
  'enable' { $normalized = 'enable' }
  'on' { $normalized = 'enable' }
  '启用' { $normalized = 'enable' }
  'where' { $normalized = 'where' }
  'path' { $normalized = 'where' }
  'backups' { $normalized = 'backups' }
  'backup' { $normalized = 'backups' }
  '备份' { $normalized = 'backups' }
  'help' { $normalized = 'help' }
  default {
    Write-DshxError "未知命令：$action（可用：status / list / disable / enable / backups / where / help）"
    exit 1
  }
}

if ($normalized -eq 'help') { exit (Show-Help) }

if ($normalized -eq 'status' -or $normalized -eq 'disable' -or $normalized -eq 'enable') {
  if (-not (Test-BundleName $bundle)) {
    Write-DshxError "插件名无效：$bundle"
    exit 1
  }
}

try {
  switch ($normalized) {
    'status' { $code = Show-Status $profileArg $bundle $asJson }
    'list' { $code = Show-List $profileArg $asJson }
    'where' { $code = Show-Where $profileArg $asJson }
    'backups' { $code = Show-Backups $profileArg $asJson }
    'disable' { $code = Set-PluginState $profileArg $bundle $false $force $asJson }
    'enable' { $code = Set-PluginState $profileArg $bundle $true $force $asJson }
    default { $code = 1 }
  }
  exit $code
} catch {
  Write-DshxError $_.Exception.Message
  exit 1
}
