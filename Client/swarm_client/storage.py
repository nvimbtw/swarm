import ipaddress
import json
import os
from pathlib import Path
import socket
import tempfile
from urllib.parse import urlsplit
import uuid


def backend_origin(value):
    parsed = urlsplit(value)
    if (parsed.scheme not in {"http", "https"} or not parsed.hostname
            or parsed.username or parsed.password or parsed.query or parsed.fragment
            or parsed.path not in {"", "/"}):
        raise ValueError("Backend must be an HTTP(S) origin without a path or credentials.")
    try:
        loopback = ipaddress.ip_address(parsed.hostname).is_loopback
    except ValueError:
        loopback = parsed.hostname.lower() == "localhost"
    if parsed.scheme == "http" and not loopback:
        raise ValueError("Remote backends require HTTPS. HTTP is allowed only on loopback.")
    _ = parsed.port  # Validate the port before persisting it.
    return parsed.geturl().rstrip("/")


def read_json(path, default):
    try:
        return json.loads(path.read_text())
    except FileNotFoundError:
        return default


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    fd, temporary = tempfile.mkstemp(dir=path.parent)
    try:
        with os.fdopen(fd, "w") as stream:
            json.dump(data, stream, indent=2)
            stream.write("\n")
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


class Storage:
    def __init__(self, home):
        self.home = Path(home).expanduser().resolve()
        self.home.mkdir(parents=True, exist_ok=True, mode=0o700)
        self.home.chmod(0o700)
        self.socket = self.home / "connector.sock"
        self.config = read_json(self.home / "config.json", {})
        if not self.config:
            self.config = {"version": 1, "machine_id": str(uuid.uuid4()),
                           "machine_name": socket.gethostname()}
            write_json(self.home / "config.json", self.config)
        self.credentials = read_json(self.home / "credentials.json", {})

    def save_credentials(self):
        write_json(self.home / "credentials.json", self.credentials)


def project_settings(workspace):
    # Explicit workspace root: do not accidentally inherit a neighbouring project's identity.
    return read_json(Path(workspace) / ".swarm" / "config.json", {})
