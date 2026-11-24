/**
 * Open local paths from the plugin page.
 *
 * The only side effect is handing a fixed package path to the platform file
 * manager; no user input reaches the command line.
 */

import { spawn } from 'node:child_process'
import { join } from 'node:path'
import { ExternalToolsError, PACKAGE_ROOT } from './profile.js'

/**
 * Reveal the tool folder, the CLI, the README, or launch the standalone EXE.
 * Resolves once the platform launcher has started, or rejects when it cannot.
 * @param {'folder' | 'cli' | 'readme' | 'exe'} target
 * @returns {Promise<string>} The path that was handed to the file manager.
 */
export function openTarget(target) {
  const binDir = join(PACKAGE_ROOT, 'bin')
  const readme = join(PACKAGE_ROOT, 'README.md')
  const cli = join(PACKAGE_ROOT, 'bin', 'dshx.cmd')
  const exe = join(PACKAGE_ROOT, 'dsh-external-tools.exe')

  let command
  let args
  let path
  if (target === 'folder') {
    path = binDir
    command = process.platform === 'win32' ? 'explorer.exe' : process.platform === 'darwin' ? 'open' : 'xdg-open'
    args = [binDir]
  } else if (target === 'exe') {
    if (process.platform !== 'win32') {
      throw new ExternalToolsError('INVALID_TARGET', '独立 EXE 目前只支持 Windows')
    }
    path = exe
    command = exe
    args = []
  } else if (target === 'cli') {
    path = cli
    command = process.platform === 'win32' ? 'explorer.exe' : process.platform === 'darwin' ? 'open' : 'xdg-open'
    args = process.platform === 'win32' ? ['/select,', cli] : [cli]
  } else if (target === 'readme') {
    path = readme
    command = process.platform === 'win32' ? 'explorer.exe' : process.platform === 'darwin' ? 'open' : 'xdg-open'
    args = process.platform === 'win32' ? ['/select,', readme] : [readme]
  } else {
    throw new ExternalToolsError('INVALID_TARGET', `未知的打开目标：${String(target)}`)
  }

  return new Promise((resolve, reject) => {
    let settled = false
    let timer
    const finish = (ok, value) => {
      if (settled) return
      settled = true
      clearTimeout(timer)
      if (ok) resolve(value)
      else reject(value)
    }

    let child
    try {
      child = spawn(command, args, { detached: true, stdio: 'ignore' })
    } catch (error) {
      reject(new ExternalToolsError('OPEN_FAILED', `打开失败：${error instanceof Error ? error.message : String(error)}`))
      return
    }

    // `spawn` fires when the launcher starts; explorer.exe exits immediately, so
    // do not wait for its exit code. The timeout covers unusual launchers.
    timer = setTimeout(() => finish(true, path), 1500)
    child.once('error', (error) => {
      finish(false, new ExternalToolsError('OPEN_FAILED', `打开失败：${error instanceof Error ? error.message : String(error)}`))
    })
    child.once('spawn', () => finish(true, path))
    child.unref()
  })
}
