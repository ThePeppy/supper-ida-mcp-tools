import json
import unittest
from unittest.mock import patch

from ida_pro_mcp import registry_server as registry


class _FakeResponse:
    def __init__(self, body: dict, status: int = 200, reason: str = "OK"):
        self.status = status
        self.reason = reason
        self._body = json.dumps(body).encode("utf-8")

    def read(self):
        return self._body

    def getheaders(self):
        return []


class _BaseFakeConnection:
    instances = []

    def __init__(self, host, port, timeout=30):
        self.host = host
        self.port = port
        self.timeout = timeout
        self.requests = []
        self.closed = False
        type(self).instances.append(self)

    @classmethod
    def reset(cls):
        cls.instances = []

    def request(self, method, path, body=None, headers=None):
        self.requests.append(
            {
                "method": method,
                "path": path,
                "body": body,
                "headers": headers or {},
            }
        )

    def close(self):
        self.closed = True


class _ToolsAndCallConnection(_BaseFakeConnection):
    def getresponse(self):
        request = self.requests[-1]
        payload = json.loads(request["body"].decode("utf-8"))
        if payload["method"] == "tools/list":
            return _FakeResponse(
                {
                    "jsonrpc": "2.0",
                    "result": {
                        "tools": [
                            {
                                "name": "decompile",
                                "description": "Decompile function.",
                                "inputSchema": {
                                    "type": "object",
                                    "properties": {"addr": {"type": "string"}},
                                    "required": ["addr"],
                                },
                            },
                            {
                                "name": "list_instances",
                                "description": "Legacy local tool.",
                                "inputSchema": {"type": "object"},
                            },
                        ]
                    },
                    "id": payload.get("id"),
                }
            )
        return _FakeResponse(
            {
                "jsonrpc": "2.0",
                "result": {
                    "content": [{"type": "text", "text": "{\"ok\":true}"}],
                    "structuredContent": {"ok": True, "arguments": payload["params"]["arguments"]},
                    "isError": False,
                },
                "id": payload.get("id"),
            }
        )


class _DockerFallbackConnection(_ToolsAndCallConnection):
    def request(self, method, path, body=None, headers=None):
        super().request(method, path, body, headers)
        if self.host == "127.0.0.1":
            raise ConnectionRefusedError("container loopback")


class RegistryServerTests(unittest.TestCase):
    def setUp(self):
        self._old_ttl = registry.TARGET_TTL_SEC
        registry._targets.clear()
        registry._session_target_ids.clear()
        registry._session_target_last_seen.clear()
        registry._output_proxy_targets.clear()
        registry._default_target_id = None
        registry.TARGET_TTL_SEC = 0
        self._probe_patch = patch.object(registry, "probe_instance", lambda *args, **kwargs: True)
        self._probe_patch.start()
        _ToolsAndCallConnection.reset()

    def tearDown(self):
        registry.TARGET_TTL_SEC = self._old_ttl
        registry._targets.clear()
        registry._session_target_ids.clear()
        registry._session_target_last_seen.clear()
        registry._output_proxy_targets.clear()
        registry._default_target_id = None
        self._probe_patch.stop()

    def _register(self, port=13337, binary="auth_server"):
        return registry._register_target(
            {
                "host": "127.0.0.1",
                "port": port,
                "pid": port,
                "binary": binary,
                "idb_path": f"/tmp/{binary}.i64",
                "input_path": f"/tmp/{binary}",
                "metadata": {"processor": "metapc"},
            }
        )

    def test_register_target_assigns_alias_and_lists_target(self):
        first = self._register()
        second = self._register(port=13338, binary="auth_server")

        listed = registry.list_targets()
        aliases = [target["alias"] for target in listed["targets"]]

        self.assertEqual(listed["count"], 2)
        self.assertEqual(first["alias"], "auth_server")
        self.assertEqual(second["alias"], "auth_server_2")
        self.assertEqual(aliases, ["auth_server", "auth_server_2"])

    def test_tools_list_injects_optional_target_argument(self):
        self._register()
        with patch("ida_pro_mcp.registry_server.http.client.HTTPConnection", _ToolsAndCallConnection):
            response = registry.mcp.registry.dispatch(
                {"jsonrpc": "2.0", "method": "tools/list", "id": 1}
            )

        tools = response["result"]["tools"]
        names = [tool["name"] for tool in tools]
        decompile = next(tool for tool in tools if tool["name"] == "decompile")

        self.assertIn("list_targets", names)
        self.assertIn("decompile", names)
        self.assertNotIn("list_instances", names)
        self.assertIn("target", decompile["inputSchema"]["properties"])
        self.assertNotIn("target", decompile["inputSchema"].get("required", []))

    def test_tool_call_routes_by_target_and_strips_target_argument(self):
        target = self._register(port=15555, binary="gateway")
        with patch("ida_pro_mcp.registry_server.http.client.HTTPConnection", _ToolsAndCallConnection):
            response = registry.mcp.registry.dispatch(
                {
                    "jsonrpc": "2.0",
                    "method": "tools/call",
                    "params": {
                        "name": "decompile",
                        "arguments": {"addr": "main", "target": target["alias"]},
                    },
                    "id": 2,
                }
            )

        conn = _ToolsAndCallConnection.instances[0]
        forwarded = json.loads(conn.requests[0]["body"].decode("utf-8"))

        self.assertEqual((conn.host, conn.port), ("127.0.0.1", 15555))
        self.assertEqual(conn.requests[0]["path"], "/mcp")
        self.assertEqual(conn.requests[0]["headers"].get(registry.PROXY_HEADER), "1")
        self.assertEqual(forwarded["params"]["arguments"], {"addr": "main"})
        self.assertEqual(response["result"]["structuredContent"]["ok"], True)
        self.assertEqual(
            response["result"]["structuredContent"]["arguments"],
            {"addr": "main"},
        )

    def test_tool_call_falls_back_to_docker_host_for_loopback_targets(self):
        target = self._register(port=15555, binary="gateway")
        with patch("ida_pro_mcp.registry_server.http.client.HTTPConnection", _DockerFallbackConnection):
            response = registry.mcp.registry.dispatch(
                {
                    "jsonrpc": "2.0",
                    "method": "tools/call",
                    "params": {
                        "name": "decompile",
                        "arguments": {"addr": "main", "target": target["alias"]},
                    },
                    "id": 2,
                }
            )

        hosts = [conn.host for conn in _DockerFallbackConnection.instances]
        self.assertEqual(hosts, ["127.0.0.1", "host.docker.internal"])
        self.assertEqual(
            _DockerFallbackConnection.instances[1].requests[0]["headers"].get("Host"),
            "127.0.0.1:15555",
        )
        self.assertEqual(response["result"]["structuredContent"]["ok"], True)

    def test_multiple_targets_require_explicit_selection_or_target_argument(self):
        self._register(port=13337, binary="auth")
        self._register(port=13338, binary="billing")

        response = registry.mcp.registry.dispatch(
            {
                "jsonrpc": "2.0",
                "method": "tools/call",
                "params": {"name": "decompile", "arguments": {"addr": "main"}},
                "id": 3,
            }
        )

        self.assertIn("error", response)
        self.assertIn("Multiple IDA targets", response["error"]["data"]["error"])


if __name__ == "__main__":
    unittest.main()
