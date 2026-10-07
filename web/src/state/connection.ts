import { create } from 'zustand'
import { PROTOCOL_VERSION, type ClientMessage, type ServerMessage } from '../protocol'

export type ConnectionStatus = 'connecting' | 'connected' | 'disconnected'

type ServerInfo = Extract<ServerMessage, { type: 'welcome' }>

interface ConnectionState {
  status: ConnectionStatus
  server: ServerInfo | null
  lastError: string | null
}

export const useConnection = create<ConnectionState>(() => ({
  status: 'disconnected',
  server: null,
  lastError: null,
}))

const MIN_RETRY_MS = 500
const MAX_RETRY_MS = 5000

/** Single WebSocket to the OVSD host with automatic reconnection and exponential backoff. */
class DeckSocket {
  private socket: WebSocket | null = null
  private retryMs = MIN_RETRY_MS
  private started = false

  start() {
    if (this.started) return
    this.started = true
    this.connect()
  }

  send(message: ClientMessage) {
    if (this.socket?.readyState === WebSocket.OPEN) this.socket.send(JSON.stringify(message))
  }

  private connect() {
    const url = `${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/ws`
    useConnection.setState({ status: 'connecting' })
    const socket = new WebSocket(url)
    this.socket = socket

    socket.onopen = () => {
      this.retryMs = MIN_RETRY_MS
      this.send({
        type: 'hello',
        protocolVersion: PROTOCOL_VERSION,
        token: localStorage.getItem('ovsd.token'),
        device: { name: navigator.platform || 'browser', userAgent: navigator.userAgent },
        viewport: { width: innerWidth, height: innerHeight, pixelRatio: devicePixelRatio },
      })
    }
    socket.onmessage = (event) => this.handle(JSON.parse(event.data) as ServerMessage)
    socket.onclose = () => {
      this.socket = null
      useConnection.setState({ status: 'disconnected' })
      window.setTimeout(() => this.connect(), this.retryMs)
      this.retryMs = Math.min(this.retryMs * 2, MAX_RETRY_MS)
    }
  }

  private handle(message: ServerMessage) {
    switch (message.type) {
      case 'welcome':
        useConnection.setState({ status: 'connected', server: message, lastError: null })
        break
      case 'error':
        useConnection.setState({ lastError: `${message.code}: ${message.message}` })
        break
    }
  }
}

export const deckSocket = new DeckSocket()
