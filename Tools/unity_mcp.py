"""Talks to the open editor's MCP bridge (com.coplaydev.unity-mcp, TCP 6400) without an MCP client.

    python Tools/unity_mcp.py <command> ['<json params>']      e.g.  ping · refresh_unity · read_console '{"types":["error"]}'
"""
import json, socket, struct, sys

def call(cmd, params=None, timeout=600):
    s = socket.create_connection(("127.0.0.1", 6400), timeout=10)
    s.settimeout(timeout)
    hello = s.recv(256).decode("ascii", "replace")
    framed = "FRAMING=1" in hello
    payload = json.dumps({"type": cmd, "params": params or {}}).encode("utf-8")
    if framed:
        s.sendall(struct.pack(">Q", len(payload)) + payload)
        def read(n):
            buf = b""
            while len(buf) < n:
                chunk = s.recv(n - len(buf))
                if not chunk: raise IOError("closed")
                buf += chunk
            return buf
        size = struct.unpack(">Q", read(8))[0]
        data = read(size)
    else:
        s.sendall(payload)
        data = s.recv(1 << 22)
    s.close()
    return hello.strip(), data.decode("utf-8", "replace")

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    hello, out = call(sys.argv[1], json.loads(sys.argv[2]) if len(sys.argv) > 2 else None)
    print(out[:int(sys.argv[3]) if len(sys.argv) > 3 else 6000])
