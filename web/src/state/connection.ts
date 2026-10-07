import { create } from 'zustand'
import { getToken } from '../api'
import { PROTOCOL_VERSION, type ClientMessage, type Gesture, type ServerMessage, type WelcomeMessage } from '../protocol'
import { useDeck } from './deck'

export type ConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'unpaired'
export type ClientRole = 'deck' | 'editor'

interface ConnectionState {
  status: ConnectionStatus
  server: WelcomeMessage | null
  lastError: string | null
}

export const useConnection = create<ConnectionState>(() => ({
  status: 'disconnected',
  server: null,
  lastError: null,
}))

type ConfigListener = (message: Extract<ServerMessage, { type: 'configChanged' }>) => void

const MIN_RETRY_MS = 500
const MAX_RETRY_MS = 5000

/** Single WebSocket to the OVSD host with automatic reconnection and exponential backoff. */
class DeckSocket {
  private socket: WebSocket | null = null
  private retryMs = MIN_RETRY_MS
  private retryTimer: number | undefined
  private role: ClientRole = 'deck'
  private started = false
  private readonly configListeners = new Set<ConfigListener>()

  start(role: ClientRole) {
    if (this.started) return
    this.started = true
    this.role = role
    this.connect()
    // Phones suspend sockets in the background; reconnect as soon as the page is visible again.
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible' && this.socket?.readyState !== WebSocket.OPEN) this.reconnectNow()
    })
  }

  /** Drops the current socket and connects again immediately (e.g. after pairing). */
  reconnectNow() {
    window.clearTimeout(this.retryTimer)
    this.retryMs = MIN_RETRY_MS
    const old = this.socket
    this.socket = null
    if (old) {
      old.onclose = null
      old.close()
    }
    this.connect()
  }

  send(message: ClientMessage) {
    if (this.socket?.readyState === WebSocket.OPEN) this.socket.send(JSON.stringify(message))
  }

  input(controlId: string, gesture: Gesture, value?: number) {
    this.send({ type: 'input', controlId, gesture, value })
  }

  onConfigChanged(listener: ConfigListener) {
    this.configListeners.add(listener)
    return () => {
      this.configListeners.delete(listener)
    }
  }

  private connect() {
    const url = `${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/ws`
    if (useConnection.getState().status !== 'unpaired') useConnection.setState({ status: 'connecting' })
    const socket = new WebSocket(url)
    this.socket = socket

    socket.onopen = () => {
      this.retryMs = MIN_RETRY_MS
      socket.send(
        JSON.stringify({
          type: 'hello',
          protocolVersion: PROTOCOL_VERSION,
          token: getToken(),
          role: this.role,
          device: { name: navigator.platform || 'browser', userAgent: navigator.userAgent },
          viewport: { width: innerWidth, height: innerHeight, pixelRatio: devicePixelRatio },
        } satisfies ClientMessage),
      )
    }
    socket.onmessage = (event) => this.handle(JSON.parse(event.data as string) as ServerMessage)
    socket.onclose = () => {
      if (this.socket !== socket) return
      this.socket = null
      if (useConnection.getState().status === 'unpaired') return // wait for pairing, then reconnectNow()
      useConnection.setState({ status: 'disconnected' })
      this.retryTimer = window.setTimeout(() => this.connect(), this.retryMs)
      this.retryMs = Math.min(this.retryMs * 2, MAX_RETRY_MS)
    }
  }

  private handle(message: ServerMessage) {
    const deck = useDeck.getState()
    switch (message.type) {
      case 'welcome':
        useConnection.setState({ status: 'connected', server: message, lastError: null })
        useDeck.setState({ settings: message.settings })
        break
      case 'error':
        if (message.code === 'unpaired') useConnection.setState({ status: 'unpaired', lastError: message.message })
        else if (message.code === 'protocol_mismatch') location.reload()
        else useConnection.setState({ lastError: `${message.code}: ${message.message}` })
        break
      case 'layout':
        deck.setLayout(message)
        break
      case 'tiles':
        deck.applyTiles(message)
        break
      case 'notify':
        deck.toast(message.level, message.message)
        break
      case 'deckSettings':
        useDeck.setState({ settings: message.settings })
        break
      case 'configChanged':
        for (const listener of this.configListeners) listener(message)
        break
    }
  }
}

export const deckSocket = new DeckSocket()
