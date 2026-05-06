# Center <-> IDA Plugin Protocol

Transport: TCP long connection.

Framing: 4-byte big-endian unsigned payload length followed by UTF-8 JSON.

Message model: typed JSON messages. Request/response pairs use the `id` field.

## Message Types

- `hello`: plugin registers the current IDA window.
- `heartbeat`: plugin keeps the target health fresh.
- `tool_call`: center asks a plugin executor to run a center-owned tool.
- `tool_result`: plugin returns the result for a `tool_call`.
- `shutdown`: center asks a plugin executor to stop.

## Registration

The plugin sends `hello` immediately after connecting:

```json
{
  "type": "hello",
  "instanceId": "uuid",
  "payload": {
    "instanceId": "uuid",
    "alias": "ida-12345",
    "processId": 12345,
    "binaryName": "service-a",
    "inputPath": "/path/service-a",
    "databasePath": "/path/service-a.i64",
    "idaVersion": "9.x",
    "platform": "darwin"
  }
}
```

If the TCP connection closes, the center removes the target registration. This
prevents stale IDA windows from accumulating in long-running center processes.

## Tool Calls

The center sends:

```json
{
  "type": "tool_call",
  "id": "request-id",
  "instanceId": "uuid",
  "payload": {
    "tool": "target.get_metadata",
    "arguments": {}
  }
}
```

The plugin returns:

```json
{
  "type": "tool_result",
  "id": "request-id",
  "instanceId": "uuid",
  "payload": {
    "ok": true,
    "result": {},
    "error": null
  }
}
```

The center owns tool registration and schemas. The plugin only dispatches the
tool command and returns data.
