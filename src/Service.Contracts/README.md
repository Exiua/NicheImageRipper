# Service.Contracts

The types the [Service](../Service) API sends and receives: request and response models, the settings update
(`Config`), and the `/ws/events` WebSocket messages. Clients such as the [Gui](../Gui)'s thin mode reference this
instead of the Service itself.

The namespaces match the Service (`NicheImageRipper.Service.Models.*`), so moving a type here doesn't change code
that uses it.

## WebSocket messages

- Server to client: `WsEnvelope` with a `WsEventType` and a payload (`ProgressChangedEvent`, `LogLevelChangedEvent`,
  or a log event).
- Client to server: `WsClientMessage`; currently only `SetLogLevel`, which picks the lowest `WsLogLevel` the client
  receives.
