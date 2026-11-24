/**
 * Same-origin HTTP routes used by the plugin page.
 *
 * Security model (default deny, matching the other local plugins):
 * 1. Every request carries `x-dsh-external-tools: 1`; a cross-site simple
 *    request cannot set a custom header.
 * 2. The Host header must be loopback; an Origin, when present, must be the
 *    same loopback authority or the desktop shell protocol.
 * 3. No CORS headers are returned.
 * 4. Request bodies are capped at 64 KiB.
 *
 *   GET  /.dsh-external-tools/status → plugin + profile + bundle state
 *   POST /.dsh-external-tools/set    → { bundle?, enabled }
 *   POST /.dsh-external-tools/open   → { target: 'folder' | 'cli' | 'readme' }
 */

import { CLIENT_HEADER, BUNDLE_NAME, ROUTE_PREFIX, describeProfile, ExternalToolsError, setBundleEnabled } from './profile.js'
import { openTarget } from './open.js'

/** Request bodies are tiny; anything larger is a mistake or an attack. */
const BODY_LIMIT_BYTES = 64 * 1024

/** Loopback host names accepted in the Host and Origin headers. */
const LOOPBACK_HOSTNAMES = new Set(['localhost', '127.0.0.1', '[::1]'])

/**
 * Normalize `host[:port]` to `loopback:port`.
 * @param {string} authority
 * @returns {string | null}
 */
function loopbackAuthority(authority) {
  try {
    const parsed = new URL(`http://${authority}`)
    if (!LOOPBACK_HOSTNAMES.has(parsed.hostname.toLowerCase())) return null
    return `loopback:${parsed.port === '' ? '80' : parsed.port}`
  } catch {
    return null
  }
}

/**
 * Whether the request comes from a local page.
 * @param {import('node:http').IncomingMessage} request
 * @returns {boolean}
 */
function isTrustedRequest(request) {
  const host = request.headers.host
  const hostKey = host === undefined ? null : loopbackAuthority(host)
  if (hostKey === null) return false
  const origin = request.headers.origin
  if (origin === undefined || origin === '') return true
  try {
    const parsed = new URL(origin)
    if (parsed.protocol === 'dsh-app:') return true
    return loopbackAuthority(parsed.host) === hostKey
  } catch {
    return false
  }
}

/**
 * Whether the local-client marker is present.
 * @param {import('node:http').IncomingMessage} request
 * @returns {boolean}
 */
function hasClientHeader(request) {
  const value = request.headers[CLIENT_HEADER]
  return typeof value === 'string' && value === '1'
}

/**
 * Write a JSON response.
 * @param {import('node:http').ServerResponse} response
 * @param {number} status
 * @param {unknown} body
 * @param {Record<string, string>} [extraHeaders]
 */
function sendJson(response, status, body, extraHeaders) {
  const payload = JSON.stringify(body)
  response.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'cache-control': 'no-store',
    'x-content-type-options': 'nosniff',
    'referrer-policy': 'no-referrer',
    'content-length': Buffer.byteLength(payload),
    ...(extraHeaders ?? {}),
  })
  response.end(payload)
}

/**
 * Reject requests that are not from the local page.
 * @param {import('node:http').IncomingMessage} request
 * @param {import('node:http').ServerResponse} response
 * @returns {boolean}
 */
function requireLocalClient(request, response) {
  if (!hasClientHeader(request)) {
    sendJson(response, 403, {
      ok: false,
      error: { code: 'FORBIDDEN_CLIENT', message: '缺少本地客户端请求头' },
    })
    return false
  }
  if (!isTrustedRequest(request)) {
    sendJson(response, 403, {
      ok: false,
      error: { code: 'UNTRUSTED_ORIGIN', message: '只接受来自本机页面的请求' },
    })
    return false
  }
  return true
}

/**
 * Read a JSON object body.
 * @param {import('node:http').IncomingMessage} request
 * @returns {Promise<Record<string, unknown>>}
 */
async function readJsonBody(request) {
  const chunks = []
  let size = 0
  for await (const chunk of request) {
    const buffer = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk)
    size += buffer.length
    if (size > BODY_LIMIT_BYTES) throw new ExternalToolsError('BODY_TOO_LARGE', '请求体过大')
    chunks.push(buffer)
  }
  if (chunks.length === 0) return {}
  let parsed
  try {
    parsed = JSON.parse(Buffer.concat(chunks).toString('utf8'))
  } catch {
    throw new ExternalToolsError('INVALID_BODY', '请求体不是合法 JSON')
  }
  return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {}
}

/**
 * HTTP status for a stable error code.
 * @param {string} code
 * @returns {number}
 */
function statusForCode(code) {
  if (code === 'INVALID_BODY' || code === 'INVALID_BUNDLE' || code === 'INVALID_TARGET' || code === 'NOT_INSTALLED' || code === 'BODY_TOO_LARGE') {
    return 400
  }
  if (code === 'PROTECTED_BUNDLE') return 409
  if (code === 'PROFILE_NOT_FOUND') return 404
  return 500
}

/**
 * Turn an exception into a safe response body.
 * @param {unknown} error
 * @returns {{ code: string, message: string }}
 */
function describeError(error) {
  const code = error !== null && typeof error === 'object' && typeof error.code === 'string' ? error.code : 'INTERNAL_ERROR'
  const message = error instanceof Error && error.message !== '' ? error.message : String(error)
  return { code, message }
}

/**
 * Create the route handler for one profile.
 * @param {{ profileDir: string }} options
 */
export function createRouteHandler({ profileDir }) {
  /** Serialize mutations so two overlapping /set calls cannot lose an update. */
  let mutation = Promise.resolve()
  const serialize = (task) => {
    const run = mutation.then(task, task)
    mutation = run.then(() => undefined, () => undefined)
    return run
  }

  return async function handler(request, response) {
    if (!requireLocalClient(request, response)) return

    const url = new URL(request.url ?? '/', 'http://localhost')
    const pathname = url.pathname

    if (pathname === `${ROUTE_PREFIX}/status`) {
      if (request.method !== 'GET') {
        sendJson(response, 405, { ok: false, error: { code: 'METHOD_NOT_ALLOWED', message: '只支持 GET' } }, { allow: 'GET' })
        return
      }
      try {
        sendJson(response, 200, { ok: true, ...describeProfile(profileDir) })
      } catch (error) {
        const described = describeError(error)
        sendJson(response, statusForCode(described.code), { ok: false, error: described })
      }
      return
    }

    if (pathname === `${ROUTE_PREFIX}/set`) {
      if (request.method !== 'POST') {
        sendJson(response, 405, { ok: false, error: { code: 'METHOD_NOT_ALLOWED', message: '只支持 POST' } }, { allow: 'POST' })
        return
      }
      try {
        const body = await readJsonBody(request)
        const bundle = body.bundle === undefined ? BUNDLE_NAME : body.bundle
        if (typeof body.enabled !== 'boolean') {
          throw new ExternalToolsError('INVALID_BODY', 'enabled 必须是布尔值')
        }
        const result = await serialize(() => setBundleEnabled(profileDir, bundle, body.enabled))
        sendJson(response, 200, {
          ok: true,
          result,
          state: describeProfile(profileDir),
        })
      } catch (error) {
        const described = describeError(error)
        sendJson(response, statusForCode(described.code), { ok: false, error: described })
      }
      return
    }

    if (pathname === `${ROUTE_PREFIX}/open`) {
      if (request.method !== 'POST') {
        sendJson(response, 405, { ok: false, error: { code: 'METHOD_NOT_ALLOWED', message: '只支持 POST' } }, { allow: 'POST' })
        return
      }
      try {
        const body = await readJsonBody(request)
        const target = typeof body.target === 'string' ? body.target : ''
        if (target !== 'folder' && target !== 'cli' && target !== 'readme' && target !== 'exe') {
          throw new ExternalToolsError('INVALID_TARGET', 'target 必须是 folder / cli / readme / exe')
        }
        const path = await openTarget(target)
        sendJson(response, 200, { ok: true, target, path })
      } catch (error) {
        const described = describeError(error)
        sendJson(response, statusForCode(described.code), { ok: false, error: described })
      }
      return
    }

    sendJson(response, 404, { ok: false, error: { code: 'NOT_FOUND', message: '未知接口' } })
  }
}
