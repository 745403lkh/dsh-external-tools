# 更新日志 / Changelog

## [1.0.0] - 2026-11-24

首个正式版。这里只写它能做什么，不写迭代过程。

### 中文

**独立便携**
- 单个 Windows EXE，双击运行；不需要安装器、服务、后台进程，不加载 DSH / Node / Electron。
- EXE 可以单独复制、压缩、发给别人；不依赖 DSH 插件，也不注册 DSH 设置页。
- 使用 `highestAvailable` manifest：管理员账号启动会走 UAC，标准账号仍可运行。
- 需要 Windows 10 / 11 和 .NET Framework 4.x；不需要 .NET SDK、Node、pnpm 或 DSH 运行时。

**启动与定位**
- 优先读取 `DSH_NODE_EXECUTABLE`，其次读取 HKCU 卸载项和常见安装路径，定位当前 DSH。
- 读取 DSH EXE 的文件版本，按 `0.2.2` 系列校验；版本不匹配时禁用重装入口，避免把不兼容的安装包覆盖上去。
- 自动定位 profile、`package.json`、`cordis.patch.yml`、CLI `lib/bin.js` 和 Web profile；也允许手动选择 profile。

**一键诊断**
- 检查 `package.json` 是否可解析、`dsh.profile.bundles` 是否完整、依赖目录是否存在。
- 检查 `cordis.patch.yml`、bundle 版本、DSH 进程、端口占用、配置备份和最近日志。
- 输出可复制的中文 / English 诊断报告，给出 `[OK]`、`[INFO]`、`[WARN]`、`[ERROR]` 分级结果。
- 支持导出诊断 ZIP：包含配置、patch、日志、bundle 列表、进程列表和诊断报告，便于发给别人排查。

**安全模式**
- 只保留 `dsh-base` / `dsh-web-app`，把其它 bundle 临时移出启动列表，用来判断 DSH 是否被某个插件拖垮。
- 写入前自动备份 `package.json`；退出安全模式时恢复原来的 bundle 列表。
- 安全模式记录写在 profile 下，DSH 重启后仍可识别和退出。

**备份恢复**
- 每次修改配置前自动备份 `package.json`，滚动保留最近备份。
- 列表显示备份时间、文件大小；恢复前再次备份当前配置，避免“恢复一次把当前状态也弄丢”。
- 也可以直接从“备份恢复”模块打开备份目录。

**日志分析**
- 离线规则匹配 DSH 日志，识别端口占用、依赖缺失、模块解析失败、插件加载异常、进程崩溃等常见问题。
- 汇总问题级别、出现次数、相关日志文件和可执行建议。
- 纯本地分析，不上传日志、配置或任何诊断数据。

**进程处理**
- 查看当前 DSH 进程数量和 PID。
- 结束卡死或残留的 DSH 进程；也可以直接启动 DSH。
- 进程操作前提示影响范围，避免误杀其它程序。

**依赖与配置修复**
- 在 profile 目录执行 `pnpm install --prefer-offline`，修复 `node_modules` 缺失或依赖损坏。
- 备份后重置损坏的 `cordis.patch.yml` 为 `[]`，不删除任何 bundle。
- 一键打开配置目录、工具目录、日志目录和 README，方便手工排查。

**插件控制**
- 列出 profile 中的全部 bundle，显示插件名称、包名、版本、启用状态和安装来源。
- 名称优先读取 `displayName` / `title` / README，未知时回退包名；悬停可看包名和说明。
- 支持启用 / 禁用插件；受保护的核心插件不能禁用；写入前备份、写入后校验。
- 支持查看插件详情：名称、包名、版本、状态、类型、安装路径、依赖声明、作者、许可、仓库、描述和 README 摘要。
- 支持通过 npm registry 更新第三方插件，命令为 `npm install <包名>@latest --save --no-audit --no-fund --package-lock=false`；更新前备份 `package.json`，更新后提示重启 DSH。
- 本地 `link:` / `file:` 插件和官方内置 bundle 会明确提示不走 npm 更新，避免误操作。

**Web 端 DSH**
- 使用 DSH 自带 CLI 的绝对路径启动独立的 web profile，不依赖 `dsh` PATH 或 `DSH_*` 环境变量。
- 显示 profile、CLI 路径和启动 URL；Web profile 与 desktop 的插件、设置、会话隔离。
- 启动过程写到临时 `.cmd`，在可见终端里运行，方便看到 CLI 输出和端口。

**重装入口**
- 自动搜索官方安装包并显示路径；也允许手动选择安装包。
- 按当前 DSH 版本校验，版本不匹配时禁用安装入口。
- 只打开官方安装包做覆盖安装，不自动卸载、不删除 profile。

**语言与界面**
- 简体中文 / English 双语；默认跟随系统，非中文 / 英文系统回退中文。
- 切换语言原地重建界面，不重启 EXE；保留当前模块和“运行时强制写入”勾选。
- Windows 11 设置 / Office 风格：浅灰底、白色卡片、细边框、线性图标，只保留一个蓝色强调色。
- 首页 3 列 / 2 列响应式布局；1060×720 和 920×620 都检查过，不横向裁切。
- 卡片描述和提示按字号计算行距，标题按字号保留最小高度；长文本换行 + tooltip，避免截断。
- 图标为 DeepSeek 鲸鱼 + 右下角齿轮；提供 16 / 24 / 32 / 48 / 64 / 128 / 256 多尺寸 ICO。

**安全设计**
- 配置写入统一走“备份 → 写入 → 回读校验”，失败时保留备份路径。
- 受保护插件列表固定，禁止误禁用 `dsh-base` / `dsh-web-app` 等核心 bundle。
- 不注册 Agent 工具，避免模型获得“关掉任意插件”的能力。
- 日志分析离线；Web 启动只调用本机 DSH CLI；不下载安装包。

**测试**
- 自检覆盖：配置往返读取、写入 / 禁用 / 启用、安全模式进入 / 退出、patch 重置。
- 发布验证：profile 复制后执行自检，`roundTrip`、`writeTest`、`safeModeTest`、`patchTest` 全部为 `true`。
- 界面验证：首页、插件控制、关于、安全模式、备份恢复、诊断、日志、Web、重装、修复工具逐页检查；中文 / English 均验证。
- 构建验证：使用 Windows 自带 `csc.exe`，不依赖 .NET SDK、Node 或 pnpm。

### English

**Standalone**
- One Windows EXE, double-click to run; no installer, service, or background process, and no DSH / Node / Electron runtime.
- The EXE can be copied, zipped, or shared on its own; it does not depend on a DSH plugin and registers no DSH settings page.
- Uses a `highestAvailable` manifest: administrators get a UAC prompt, standard users can still run it.
- Requires Windows 10 / 11 and .NET Framework 4.x; no .NET SDK, Node, pnpm, or DSH runtime is required.

**Discovery**
- Finds the current DSH through `DSH_NODE_EXECUTABLE`, the HKCU uninstall entry, and common install paths.
- Reads the DSH EXE file version and validates the `0.2.2` series; a mismatch disables reinstall to avoid overwriting with an incompatible build.
- Resolves the profile, `package.json`, `cordis.patch.yml`, CLI `lib/bin.js`, and web profile; manual profile selection is also supported.

**One-click diagnosis**
- Checks whether `package.json` parses, whether `dsh.profile.bundles` is complete, and whether dependency folders exist.
- Checks `cordis.patch.yml`, bundle versions, DSH processes, port conflicts, config backups, and recent logs.
- Produces a copyable Chinese / English report with `[OK]`, `[INFO]`, `[WARN]`, and `[ERROR]` levels.
- Exports a diagnostic ZIP with config, patch, logs, bundle list, process list, and report for sharing.

**Safe mode**
- Keeps only `dsh-base` / `dsh-web-app` and temporarily removes other bundles to isolate a plugin-caused startup failure.
- Backs up `package.json` before writing; exiting safe mode restores the previous bundle list.
- The safe-mode record lives under the profile and survives DSH restarts.

**Backup and restore**
- Automatically backs up `package.json` before every config write and keeps recent rotations.
- Shows backup time and size; backs up the current config again before restoring, so a restore cannot lose the current state.
- Opens the backup folder directly from the Backup and restore module.

**Log analysis**
- Offline rule matching for port conflicts, missing dependencies, module resolution failures, plugin load errors, and crashes.
- Summarizes severity, count, source log file, and a practical suggestion.
- Purely local: no logs, config, or diagnostic data are uploaded.

**Process control**
- Shows the current DSH process count and PIDs.
- Kills a hung or leftover DSH process; can also start DSH.
- Explains the impact before acting to avoid killing unrelated programs.

**Dependency and config repair**
- Runs `pnpm install --prefer-offline` in the profile to repair missing `node_modules` or broken dependencies.
- Backs up and resets a broken `cordis.patch.yml` to `[]` without deleting any bundle.
- Opens the config, tool, log, and README locations for manual repair.

**Plugin control**
- Lists every bundle in the profile with display name, package id, version, enabled state, and install source.
- Resolves display names from `displayName` / `title` / README and falls back to the package id; hover shows the package id and description.
- Enables or disables plugins; protected core plugins cannot be disabled; writes are backed up and verified.
- Shows plugin details: name, package, version, status, type, install path, dependency spec, author, license, repository, description, and README excerpt.
- Updates third-party plugins through the npm registry with `npm install <package>@latest --save --no-audit --no-fund --package-lock=false`; backs up `package.json` first and asks for a DSH restart afterward.
- Explains that local `link:` / `file:` plugins and official built-in bundles do not use the npm update path.

**Web DSH**
- Starts an isolated web profile through the DSH CLI's absolute path, without relying on a `dsh` PATH entry or `DSH_*` environment variables.
- Shows the profile, CLI path, and startup URL; the web profile keeps its plugins, settings, and sessions separate from desktop.
- Writes a temporary `.cmd` and runs it in a visible terminal so CLI output and port selection remain visible.

**Reinstall entry**
- Finds the official installer and shows its path; manual selection is also supported.
- Validates the current DSH version and disables installation on a mismatch.
- Only opens the official installer for an in-place install; it never auto-uninstalls or deletes the profile.

**Language and UI**
- Simplified Chinese / English UI; follows the system language by default and falls back to Chinese for other locales.
- Rebuilds the UI in place when switching languages; keeps the current module and the force-write checkbox.
- Windows 11 Settings / Office-inspired visual: light gray surface, white cards, thin borders, line icons, and a single blue accent.
- Responsive 3-column / 2-column home layout; checked at 1060×720 and 920×620 without horizontal clipping.
- Line spacing follows font size; titles keep a minimum height; long text wraps and exposes a tooltip instead of being cut off.
- Icon: DeepSeek whale plus a lower-right gear, with 16 / 24 / 32 / 48 / 64 / 128 / 256 ICO sizes.

**Safety**
- Every config write follows backup → write → read-back verification; a failure preserves the backup path.
- Protected core plugins cannot be disabled by mistake.
- No Agent tools are registered, so a model cannot disable arbitrary plugins.
- Log analysis is offline; web startup only invokes the local DSH CLI; no installer is downloaded.

**Testing**
- Self-test covers config round-trip, write / disable / enable, safe-mode enter / exit, and patch reset.
- Release check runs against a copied profile; `roundTrip`, `writeTest`, `safeModeTest`, and `patchTest` all return `true`.
- UI pass covers home, plugin control, About, safe mode, backup restore, diagnosis, logs, web, reinstall, and repair tools in Chinese and English.
- Build pass uses the Windows-bundled `csc.exe`; no .NET SDK, Node, or pnpm is required.
