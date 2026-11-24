/**
 * Profile manifest operations for dsh-external-tools.
 *
 * DSH persists which bundles start with a profile in
 * `<profile>/package.json` → `dsh.profile.bundles`. Disabling a bundle means
 * removing its package name from that array; enabling means appending it.
 * This module performs exactly that edit, with a backup and a read-back check,
 * so it works both inside a running DSH and through the standalone CLI.
 *
 * The CLI in `bin/dshx.ps1` implements the same file contract for the case
 * where DSH is not running. Keep the two in sync.
 */

import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  renameSync,
  unlinkSync,
  writeFileSync,
} from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

/** Package name and loader row id; also the bundle name shown in the UI. */
export const BUNDLE_NAME = 'dsh-external-tools'

/** Local HTTP route prefix shared with client.js. */
export const ROUTE_PREFIX = '/.dsh-external-tools'

/** Header a same-origin local client must send. */
export const CLIENT_HEADER = 'x-dsh-external-tools'

/** Bundles that make the application manageable or startable; never disable them. */
const PROTECTED_BUNDLES = new Set([
  '@deepseek-ai/dsh-base',
  '@deepseek-ai/dsh-web-app',
  '@deepseek-ai/dsh-sdk-app',
  '@deepseek-ai/dsh-sdk-minimal',
  '@deepseek-ai/dsh-headless',
  '@deepseek-ai/dsh-acp-app',
  '@deepseek-ai/dsh-plugin-manager',
])

/** How many rotated profile backups to keep. */
const BACKUP_KEEP = 10

/** A failure with a stable code the HTTP layer can map to a status. */
export class ExternalToolsError extends Error {
  /**
   * @param {string} code Stable machine-readable code.
   * @param {string} message Human-readable message.
   */
  constructor(code, message) {
    super(message)
    this.name = 'ExternalToolsError'
    this.code = code
  }
}

/** Absolute package root. */
export const PACKAGE_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')

/** Read this plugin's own version without importing package.json. */
export function readPluginVersion() {
  try {
    const manifest = JSON.parse(readFileSync(join(PACKAGE_ROOT, 'package.json'), 'utf8'))
    return typeof manifest.version === 'string' ? manifest.version : '0.0.0'
  } catch {
    return '0.0.0'
  }
}

/**
 * Resolve the active profile directory.
 * @param {NodeJS.ProcessEnv} [env]
 * @returns {string}
 */
export function resolveProfileDir(env = process.env) {
  const explicit = typeof env.DSH_PROFILE_DIR === 'string' ? env.DSH_PROFILE_DIR.trim() : ''
  if (explicit !== '' && existsSync(join(explicit, 'package.json'))) return explicit
  const home = typeof env.DSH_HOME === 'string' && env.DSH_HOME.trim() !== ''
    ? env.DSH_HOME.trim()
    : join(env.USERPROFILE || env.HOME || process.cwd(), '.dsh')
  const profile = typeof env.DSH_PROFILE === 'string' && env.DSH_PROFILE.trim() !== ''
    ? env.DSH_PROFILE.trim()
    : 'desktop'
  return join(home, 'profiles', profile)
}

/**
 * Read and parse a profile manifest.
 * @param {string} profileDir
 * @returns {{ path: string, doc: Record<string, any> }}
 */
export function readManifest(profileDir) {
  const path = join(profileDir, 'package.json')
  if (!existsSync(path)) {
    throw new ExternalToolsError('PROFILE_NOT_FOUND', `找不到 profile 配置：${path}`)
  }
  let doc
  try {
    const text = readFileSync(path, 'utf8').replace(/^\uFEFF/, '')
    doc = JSON.parse(text)
  } catch (error) {
    throw new ExternalToolsError('MANIFEST_INVALID', `profile 配置不是合法 JSON：${path}（${error instanceof Error ? error.message : String(error)}）`)
  }
  if (doc === null || typeof doc !== 'object' || Array.isArray(doc)) {
    throw new ExternalToolsError('MANIFEST_INVALID', `profile 配置不是 JSON 对象：${path}`)
  }
  return { path, doc }
}

/**
 * The selected bundle names of a manifest.
 * @param {Record<string, any>} doc
 * @returns {string[]}
 */
export function selectedBundles(doc) {
  const list = doc?.dsh?.profile?.bundles
  return Array.isArray(list) ? list.filter((name) => typeof name === 'string') : []
}

/**
 * Whether a bundle is protected from management.
 * @param {string} name
 * @returns {boolean}
 */
export function isProtected(name) {
  return PROTECTED_BUNDLES.has(name)
}

/**
 * Validate a bundle package name and reject path-like input.
 * @param {unknown} name
 * @returns {string}
 */
export function validateBundleName(name) {
  if (typeof name !== 'string' || name.length === 0 || name.length > 214) {
    throw new ExternalToolsError('INVALID_BUNDLE', '插件名无效')
  }
  if (!/^(?:@[a-z0-9][a-z0-9._-]*\/)?[a-z0-9][a-z0-9._-]*$/i.test(name)) {
    throw new ExternalToolsError('INVALID_BUNDLE', `插件名无效：${name}`)
  }
  return name
}

/**
 * Path of a package manifest inside the profile's node_modules.
 * @param {string} profileDir
 * @param {string} name
 * @returns {string}
 */
export function packageManifestPath(profileDir, name) {
  return join(profileDir, 'node_modules', ...name.split('/'), 'package.json')
}

/**
 * Installed version of a bundle, when the profile holds its package.
 * @param {string} profileDir
 * @param {string} name
 * @returns {string}
 */
export function installedVersion(profileDir, name) {
  try {
    const manifest = JSON.parse(readFileSync(packageManifestPath(profileDir, name), 'utf8'))
    return typeof manifest.version === 'string' ? manifest.version : ''
  } catch {
    return ''
  }
}

/**
 * Whether a bundle is present in the profile's own dependency tree.
 * @param {Record<string, any>} doc
 * @param {string} profileDir
 * @param {string} name
 * @returns {boolean}
 */
function isProfileInstalled(doc, profileDir, name) {
  const dependencies = doc?.dependencies
  const declared = dependencies !== null && typeof dependencies === 'object' && Object.hasOwn(dependencies, name)
  return declared || existsSync(packageManifestPath(profileDir, name))
}

/**
 * Whether a bundle can be selected. Official installation bundles resolve from
 * the DSH installation rather than the profile, so a `@deepseek-ai/` name is
 * available even when the profile has no copy.
 * @param {Record<string, any>} doc
 * @param {string} profileDir
 * @param {string} name
 * @returns {boolean}
 */
function isInstalled(doc, profileDir, name) {
  return name.startsWith('@deepseek-ai/') || isProfileInstalled(doc, profileDir, name)
}

/** How many rotated profile backups exist. */
function countBackups(profileDir) {
  try {
    const directory = join(profileDir, '.dsh-external-tools', 'backups')
    return readdirSync(directory).filter((name) => name.startsWith('package.backup-') && name.endsWith('.json')).length
  } catch {
    return 0
  }
}

/**
 * Describe the profile, this plugin, and every selectable bundle.
 * @param {string} profileDir
 */
export function describeProfile(profileDir) {
  const { path, doc } = readManifest(profileDir)
  const selected = selectedBundles(doc)
  const dependencies = doc?.dependencies
  const dependencyNames = dependencies !== null && typeof dependencies === 'object' ? Object.keys(dependencies) : []
  const names = [...new Set([...selected, ...dependencyNames])]

  const bundles = names.map((name) => ({
    name,
    enabled: selected.includes(name),
    installed: isInstalled(doc, profileDir, name),
    bundled: name.startsWith('@deepseek-ai/') && !isProfileInstalled(doc, profileDir, name),
    protected: isProtected(name),
    official: name.startsWith('@deepseek-ai/'),
    self: name === BUNDLE_NAME,
    version: installedVersion(profileDir, name),
  })).sort((left, right) => {
    if (left.self !== right.self) return left.self ? -1 : 1
    if (left.enabled !== right.enabled) return left.enabled ? -1 : 1
    return left.name.localeCompare(right.name)
  })

  const enabled = selected.includes(BUNDLE_NAME)

  return {
    version: readPluginVersion(),
    bundle: BUNDLE_NAME,
    profileDir,
    manifestPath: path,
    enabled,
    installed: isInstalled(doc, profileDir, BUNDLE_NAME),
    protected: isProtected(BUNDLE_NAME),
    // Running while the manifest no longer selects this bundle means a restart is pending.
    pendingRestart: !enabled,
    dshRunning: true,
    pid: process.pid,
    platform: process.platform,
    cliAvailable: process.platform === 'win32',
    cliPath: join(PACKAGE_ROOT, 'bin', 'dshx.cmd'),
    exePath: join(PACKAGE_ROOT, 'dsh-external-tools.exe'),
    exeAvailable: process.platform === 'win32' && existsSync(join(PACKAGE_ROOT, 'dsh-external-tools.exe')),
    backupCount: countBackups(profileDir),
    packageDir: PACKAGE_ROOT,
    bundles,
  }
}

/** Write the manifest through a same-directory temporary file. */
function writeManifest(path, doc) {
  const json = `${JSON.stringify(doc, undefined, 2)}\n`
  const temporary = `${path}.dshx-${process.pid}.tmp`
  writeFileSync(temporary, json, 'utf8')
  try {
    renameSync(temporary, path)
  } catch (error) {
    const code = error !== null && typeof error === 'object' ? error.code : undefined
    if (code === 'EEXIST' || code === 'EPERM' || code === 'EACCES') {
      writeFileSync(path, json, 'utf8')
      try {
        unlinkSync(temporary)
      } catch {
        /* The write already succeeded. */
      }
      return
    }
    try {
      unlinkSync(temporary)
    } catch {
      /* Nothing else to clean up. */
    }
    throw new ExternalToolsError('WRITE_FAILED', `写入 profile 配置失败：${error instanceof Error ? error.message : String(error)}`)
  }
}

/** Copy the manifest aside and prune old copies. */
function backupManifest(profileDir, manifestPath) {
  const directory = join(profileDir, '.dsh-external-tools', 'backups')
  mkdirSync(directory, { recursive: true })
  const stamp = new Date().toISOString().replace(/[:.]/g, '-')
  const target = join(directory, `package.backup-${stamp}-${process.pid}.json`)
  writeFileSync(target, readFileSync(manifestPath))
  try {
    const files = readdirSync(directory)
      .filter((name) => name.startsWith('package.backup-'))
      .sort()
    for (const name of files.slice(0, Math.max(0, files.length - BACKUP_KEEP))) {
      unlinkSync(join(directory, name))
    }
  } catch {
    /* Pruning is best effort. */
  }
  return target
}

/** Append one operation to the profile's audit log. */
function appendLog(profileDir, line) {
  try {
    const directory = join(profileDir, '.dsh-external-tools')
    mkdirSync(directory, { recursive: true })
    writeFileSync(join(directory, 'operations.log'), `${new Date().toISOString()} ${line}\n`, { flag: 'a' })
  } catch {
    /* Logging must not fail the operation. */
  }
}

/**
 * Enable or disable one bundle by editing `dsh.profile.bundles`.
 * @param {string} profileDir
 * @param {string} name
 * @param {boolean} enabled
 * @returns {{ changed: boolean, bundle: string, enabled: boolean, manifestPath: string, backupPath: string | null }}
 */
export function setBundleEnabled(profileDir, name, enabled) {
  validateBundleName(name)
  if (typeof enabled !== 'boolean') {
    throw new ExternalToolsError('INVALID_BODY', 'enabled 必须是布尔值')
  }
  const { path, doc } = readManifest(profileDir)
  const selected = selectedBundles(doc)
  const current = selected.includes(name)

  if (!enabled && isProtected(name)) {
    throw new ExternalToolsError('PROTECTED_BUNDLE', `${name} 是受保护的内置插件，不能禁用`)
  }
  if (current === enabled) {
    return { changed: false, bundle: name, enabled: current, manifestPath: path, backupPath: null }
  }
  if (enabled && !isInstalled(doc, profileDir, name)) {
    throw new ExternalToolsError('NOT_INSTALLED', `${name} 尚未安装，无法启用`)
  }

  const next = enabled ? [...selected, name] : selected.filter((item) => item !== name)
  const nextDoc = JSON.parse(JSON.stringify(doc))
  if (nextDoc.dsh === null || typeof nextDoc.dsh !== 'object' || Array.isArray(nextDoc.dsh)) nextDoc.dsh = {}
  if (nextDoc.dsh.profile === null || typeof nextDoc.dsh.profile !== 'object' || Array.isArray(nextDoc.dsh.profile)) {
    nextDoc.dsh.profile = {}
  }
  nextDoc.dsh.profile.bundles = next

  const backupPath = backupManifest(profileDir, path)
  writeManifest(path, nextDoc)

  const verification = selectedBundles(readManifest(profileDir).doc)
  if (verification.includes(name) !== enabled) {
    throw new ExternalToolsError('WRITE_VERIFY', `写入后校验失败：${name} 的状态不是 ${enabled ? '启用' : '禁用'}（备份：${backupPath}）`)
  }

  appendLog(profileDir, `${enabled ? 'enable' : 'disable'} ${name} changed=true pid=${process.pid}`)
  return { changed: true, bundle: name, enabled, manifestPath: path, backupPath }
}
