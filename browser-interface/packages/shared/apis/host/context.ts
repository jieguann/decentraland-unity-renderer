import type { ILogger } from 'lib/logger'
import type { EntityAction, LoadableScene } from 'shared/types'
import type { PermissionItem } from '@dcl/protocol/out-ts/decentraland/kernel/apis/permissions.gen'
import type { EventData } from '@dcl/protocol/out-ts/decentraland/kernel/apis/engine_api.gen'
import type { RpcClientPort } from '@dcl/rpc'
import type { RpcSceneControllerServiceDefinition } from '@dcl/protocol/out-ts/decentraland/renderer/renderer_services/scene_controller.gen'
import type { RpcClientModule } from '@dcl/rpc/dist/codegen'

type WithRequired<T, K extends keyof T> = T & { [P in K]-?: T[P] }

export type PortContextService<K extends keyof PortContext> = WithRequired<PortContext, K>

export type PortContext = {
  sdk7: boolean
  permissionGranted: Set<PermissionItem>
  sceneData: LoadableScene & {
    readonly sceneNumber: number
  }
  subscribedEvents: Set<string>
  events: EventData[]

  // @deprecated
  sendBatch(actions: EntityAction[]): void
  sendSceneEvent<K extends keyof IEvents>(id: K, event: IEvents[K]): void
  sendProtoSceneEvent(event: EventData): void
  logger: ILogger

  // port used for this specific scene in the renderer
  scenePort: RpcClientPort
  rpcSceneControllerService: RpcClientModule<RpcSceneControllerServiceDefinition, unknown>
}
