import type { PairingResult } from './protocol'

const TOKEN_KEY = 'ovsd.token'

export function getToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

export function setToken(token: string | null) {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token)
    else localStorage.removeItem(TOKEN_KEY)
  } catch {
    // Private mode: the device will have to pair again next time.
  }
}

export class ApiError extends Error {
  readonly status: number
  readonly body: unknown

  constructor(status: number, message: string, body: unknown) {
    super(message)
    this.status = status
    this.body = body
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  const token = getToken()
  if (token) headers.authorization = `Bearer ${token}`
  let payload: BodyInit | undefined
  if (body instanceof FormData) payload = body
  else if (body !== undefined) {
    headers['content-type'] = 'application/json'
    payload = JSON.stringify(body)
  }

  const response = await fetch(path, { method, headers, body: payload })
  const text = await response.text()
  const data: unknown = text ? safeJson(text) : undefined
  if (!response.ok) {
    const detail = (data as { detail?: string; title?: string } | undefined)?.detail ?? (data as { title?: string } | undefined)?.title
    throw new ApiError(response.status, detail ?? `${response.status} ${response.statusText}`, data)
  }
  return data as T
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  delete: <T = void>(path: string) => request<T>('DELETE', path),
}

export async function uploadImage(file: File): Promise<string> {
  const form = new FormData()
  form.append('file', file)
  return (await api.post<{ url: string }>('/api/media', form)).url
}

/** Downloads a file from an authenticated endpoint. */
export async function download(path: string, fileName: string) {
  const token = getToken()
  const response = await fetch(path, { headers: token ? { authorization: `Bearer ${token}` } : {} })
  if (!response.ok) throw new ApiError(response.status, response.statusText, undefined)
  const url = URL.createObjectURL(await response.blob())
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export async function claimPairing(code: string, name: string): Promise<PairingResult> {
  const result = await api.post<PairingResult>('/api/pair/claim', { code, name })
  setToken(result.token)
  return result
}

/** A readable default name for this device ("Android tablet", "iPhone"...). */
export function guessDeviceName(): string {
  const ua = navigator.userAgent
  if (/iPad/.test(ua)) return 'iPad'
  if (/iPhone/.test(ua)) return 'iPhone'
  if (/Android/.test(ua)) return /Mobile/.test(ua) ? 'Android phone' : 'Android tablet'
  if (/Windows/.test(ua)) return 'Windows browser'
  if (/Mac OS/.test(ua)) return 'Mac browser'
  if (/Linux/.test(ua)) return 'Linux browser'
  return 'Browser'
}
