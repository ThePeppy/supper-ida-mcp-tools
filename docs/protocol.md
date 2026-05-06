# Center <-> IDA Plugin Protocol

Transport: TCP long connection.

Framing: 4-byte big-endian unsigned payload length followed by UTF-8 JSON.

Message model: JSON-RPC style request/response with typed notifications.

Initial message types:

- `hello`
- `heartbeat`
- `target_update`
- `tool_call`
- `tool_result`
- `log_event`
- `shutdown`
- `error`
