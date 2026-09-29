#!/usr/bin/env python3
"""Stand-in for one eShop backend, used by canary/tests/e2e.sh.

Serves /api/health, /api/health/detailed and 200s for everything else. Behaviour can be changed
while running through files in CONTROL_DIR:
  <name>.fail   -> non-health requests return HTTP 500
  <name>.delay  -> non-health requests sleep for the number of seconds in the file
"""
import json
import os
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def main() -> None:
    port, name, control_dir = int(sys.argv[1]), sys.argv[2], sys.argv[3]

    class Handler(BaseHTTPRequestHandler):
        def _send(self, code: int, body: str, content_type: str = "text/plain") -> None:
            data = body.encode()
            self.send_response(code)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(data)))
            self.send_header("X-Served-By", name)
            self.end_headers()
            self.wfile.write(data)

        def _handle(self) -> None:
            if self.path.startswith("/api/health/detailed"):
                self._send(200, json.dumps({"status": "Healthy", "services": {"database": {"status": "Healthy", "type": "SQL Server"}}}), "application/json")
                return
            if self.path.startswith("/api/health"):
                self._send(200, json.dumps({"status": "Healthy"}), "application/json")
                return
            delay_file = os.path.join(control_dir, f"{name}.delay")
            if os.path.exists(delay_file):
                with open(delay_file) as f:
                    time.sleep(float(f.read().strip() or 0))
            if os.path.exists(os.path.join(control_dir, f"{name}.fail")):
                self._send(500, f"{name}: injected failure\n")
                return
            self._send(200, f"served by {name}\n")

        do_GET = _handle
        do_POST = _handle

        def log_message(self, fmt: str, *args: object) -> None:
            pass

    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()


if __name__ == "__main__":
    main()
