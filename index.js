/**
 * dsh-external-tools 的 Host 入口。
 *
 * 提供：
 * - 同源 HTTP 接口 `/.dsh-external-tools/*`：给插件页读取状态、启停插件；
 * - 与 `bin/dshx.ps1` 相同的 profile 编辑规则（备份 + 写后校验）。
 *
 * 启停动作刻意只对用户开放（页面和外部程序），不注册 Agent 工具：让模型
 * 具备“关掉任意插件”的能力没有必要的收益，反而扩大提示注入的影响面。
 */

import { createRouteHandler } from './lib/routes.js'
import { ROUTE_PREFIX, resolveProfileDir } from './lib/profile.js'

/** Loader 行 id、包名、Client 模块 id 三者一致。 */
export const name = 'dsh-external-tools'

/**
 * 挂载插件。
 * @param {object} ctx Host Cordis 上下文。
 */
export function apply(ctx) {
  const profileDir = resolveProfileDir()

  ctx.inject(['webServer'], (host) => {
    host.effect(() => {
      try {
        return host.webServer.register({
          kind: 'prefix',
          path: ROUTE_PREFIX,
          handler: createRouteHandler({ profileDir }),
        })
      } catch (error) {
        console.error('[dsh-external-tools] HTTP 路由注册失败：', error)
        return () => {}
      }
    }, `${name}: http routes`)
  })
}
