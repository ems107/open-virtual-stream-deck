import type { ActionDescriptor as GeneratedActionDescriptor } from './generated/ActionDescriptor'
import type { ClientMessage } from './generated/ClientMessage'
import type { Profile, Step as GeneratedStep } from './generated/Profile'
import type { ServerMessage } from './generated/ServerMessage'

export type { ClientMessage, ServerMessage, Profile }
export type { AppSettings } from './generated/AppSettings'
export type { BackupInfo } from './generated/BackupInfo'
export type { DeviceView } from './generated/DeviceView'
export type { IntegrationStatus } from './generated/IntegrationStatus'
export type { OptionItem } from './generated/OptionItem'
export type { PairInfo } from './generated/PairInfo'
export type { PairingResult } from './generated/PairingResult'
export type { ProfileListItem } from './generated/ProfileListItem'
export type { ServerInfo } from './generated/ServerInfo'

/** Must match OVSD.Core.Protocol.ProtocolInfo.Version. */
export const PROTOCOL_VERSION = 1

export type Page = Profile['pages'][number]
export type Control = Page['controls'][number]
export type Cell = Control['position']
export type Appearance = Control['appearance']
export type StateConfig = NonNullable<Control['state']>
export type Bindings = Control['bindings']
export type SliderConfig = NonNullable<Control['slider']>
export type WidgetConfig = NonNullable<Control['widget']>
export type Theme = Profile['theme']
export type MatchRule = Profile['matchRules'][number]
export type Step = GeneratedStep
export type ActionStep = Extract<Step, { type: 'action' }>
export type ActionDescriptor = GeneratedActionDescriptor
export type ParamDescriptor = ActionDescriptor['params'][number]

export type Gesture = Extract<ClientMessage, { type: 'input' }>['gesture']
export type BindingGesture = keyof Bindings
export type ControlKind = Control['kind']

export type WelcomeMessage = Extract<ServerMessage, { type: 'welcome' }>
export type LayoutMessage = Extract<ServerMessage, { type: 'layout' }>
export type TilesMessage = Extract<ServerMessage, { type: 'tiles' }>
export type NotifyMessage = Extract<ServerMessage, { type: 'notify' }>
export type TileState = TilesMessage['tiles'][number]
export type DeckSettings = WelcomeMessage['settings']
export type NotifyLevel = NotifyMessage['level']
