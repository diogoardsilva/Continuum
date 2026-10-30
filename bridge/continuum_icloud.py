"""Continuum iCloud.com bridge.

Signs in to iCloud.com exactly like the browser does (Apple ID, password and
the 6-digit code) and reads contacts and photos. It is built on pyicloud,
which tracks Apple's web sign-in changes, so the Windows app does not have to
re-implement Apple's SRP and two-factor flow.

Two modes:
  continuum-icloud test            interactive check for the user
  continuum-icloud serve --port N  local JSON API for the Continuum app
"""

from __future__ import annotations

import argparse
import getpass
import json
import os
import secrets
import sys
import threading
import traceback
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, urlparse

from pyicloud import PyiCloudService
from pyicloud.exceptions import (
    PyiCloudException,
    PyiCloudFailedLoginException,
    PyiCloudServiceNotActivatedException,
)

DATA_DIR = Path(os.environ.get("LOCALAPPDATA", Path.home())) / "Continuum" / "icloud"


def label(value: str | None) -> str:
    """Apple sends labels such as 'MOBILE' or '_$!<Home>!$_'."""
    if not value:
        return "outro"
    value = value.strip("_$!<>")
    return {
        "mobile": "telemóvel",
        "iphone": "iPhone",
        "home": "casa",
        "work": "trabalho",
        "main": "principal",
        "other": "outro",
    }.get(value.lower(), value)


class Bridge:
    def __init__(self) -> None:
        DATA_DIR.mkdir(parents=True, exist_ok=True)
        self.api: PyiCloudService | None = None
        self.lock = threading.Lock()
        self._assets: dict[str, Any] = {}

    # --- sign-in -----------------------------------------------------------

    def login(self, apple_id: str, password: str) -> dict[str, Any]:
        try:
            self.api = PyiCloudService(
                apple_id,
                password,
                cookie_directory=str(DATA_DIR),
                with_family=False,
                refresh_interval=None,
            )
        except PyiCloudFailedLoginException:
            return {"status": "error", "message": "Apple ID ou palavra-passe errada."}
        return self._status()

    def resume(self, apple_id: str) -> dict[str, Any]:
        """Reuses the trusted session saved by an earlier sign-in, without the password."""
        try:
            self.api = PyiCloudService(
                apple_id,
                None,
                cookie_directory=str(DATA_DIR),
                with_family=False,
                refresh_interval=None,
            )
        except PyiCloudException:
            self.api = None
            return {"status": "signed_out"}
        if self.api.requires_2fa or self.api.requires_2sa:
            self.api = None
            return {"status": "signed_out"}
        return {"status": "ok", "name": self._my_name()}

    def submit_code(self, code: str) -> dict[str, Any]:
        api = self._require_api()
        if not api.validate_2fa_code(code.strip()):
            return {"status": "error", "message": "Código errado ou expirado."}
        if not api.is_trusted_session:
            api.trust_session()
        return self._status()

    def _status(self) -> dict[str, Any]:
        api = self._require_api()
        if api.requires_2fa:
            if api.security_key_names:
                return {"status": "error", "message": "Esta conta usa chaves de segurança físicas, que ainda não são suportadas."}
            api.request_2fa_code()
            return {"status": "code_required", "delivery": api.two_factor_delivery_method}
        if api.requires_2sa:
            return {"status": "error", "message": "Conta com verificação em dois passos antiga (2SA): ainda não suportada."}
        return {"status": "ok", "name": self._my_name()}

    def _my_name(self) -> str | None:
        try:
            me = self._require_api().contacts.me
            return f"{me.first_name} {me.last_name}".strip()
        except Exception:  # noqa: BLE001 - the name is cosmetic
            return None

    def _require_api(self) -> PyiCloudService:
        if self.api is None:
            raise RuntimeError("Ainda não iniciaste sessão.")
        return self.api

    # --- contacts ----------------------------------------------------------

    def contacts(self) -> list[dict[str, Any]]:
        result = []
        for c in self._require_api().contacts.all or []:
            photo = c.get("photo") or {}
            result.append({
                "id": c.get("contactId"),
                "firstName": c.get("firstName"),
                "lastName": c.get("lastName"),
                "company": c.get("companyName"),
                "nickname": c.get("nickName"),
                "phones": [{"label": label(p.get("label")), "value": p.get("field")} for p in c.get("phones", [])],
                "emails": [{"label": label(e.get("label")), "value": e.get("field")} for e in c.get("emailAddresses", [])],
                "photoUrl": photo.get("url"),
            })
        return result

    def download(self, url: str) -> bytes:
        response = self._require_api().session.get(url)
        response.raise_for_status()
        return response.content

    # --- photos ------------------------------------------------------------

    def photos(self, offset: int, limit: int) -> list[dict[str, Any]]:
        album = self._require_api().photos.all
        items = []
        for index, asset in enumerate(album.photos):
            if index < offset:
                continue
            if len(items) >= limit:
                break
            self._assets[asset.id] = asset
            items.append(self._asset(asset))
        return items

    def photo_bytes(self, asset_id: str, version: str) -> bytes:
        asset = self._assets.get(asset_id) or self._require_api().photos.all.get(asset_id)
        data = asset.download(version) if asset else None
        if data is None:
            raise KeyError(asset_id)
        return data

    @staticmethod
    def _asset(asset: Any) -> dict[str, Any]:
        created = asset.created
        return {
            "id": asset.id,
            "filename": asset.filename,
            "created": created.isoformat() if isinstance(created, datetime) else None,
            "type": asset.item_type,
            "width": (asset.dimensions or (None, None))[0],
            "height": (asset.dimensions or (None, None))[1],
            "size": asset.size,
            "versions": sorted(asset.versions.keys()),
        }


# --- interactive test -------------------------------------------------------

def run_test() -> int:
    print("Continuum - teste do iCloud.com")
    print("===============================\n")
    print("Inicia sessão como no iCloud.com. A palavra-passe não fica guardada;")
    print(f"a sessão confiável fica em {DATA_DIR}.\n")

    bridge = Bridge()
    apple_id = input("Apple ID (email): ").strip()
    password = getpass.getpass("Palavra-passe da Apple ID: ")

    status = bridge.login(apple_id, password)
    while status["status"] == "code_required":
        where = "por SMS" if status.get("delivery") == "sms" else "nos teus dispositivos Apple"
        code = input(f"Código de 6 dígitos enviado {where}: ")
        status = bridge.submit_code(code)

    if status["status"] != "ok":
        print(f"\nRESULTADO: falhou - {status['message']}")
        return 1
    print(f"\nSessão iniciada{(' como ' + status['name']) if status.get('name') else ''}.")

    out = Path.home() / "Documents" / "Continuum" / "iCloud"
    (out / "contactos").mkdir(parents=True, exist_ok=True)
    (out / "fotos").mkdir(parents=True, exist_ok=True)

    print("\nA ler os contactos...")
    contacts = bridge.contacts()
    with_photo = 0
    for i, c in enumerate(contacts):
        if c["photoUrl"]:
            try:
                data = bridge.download(c["photoUrl"])
                name = " ".join(filter(None, [c["firstName"], c["lastName"]])) or c["company"] or "sem-nome"
                safe = "".join(ch if ch.isalnum() or ch in " -_" else "_" for ch in name)[:40]
                (out / "contactos" / f"{i + 1:04d}-{safe}.jpg").write_bytes(data)
                with_photo += 1
            except Exception:  # noqa: BLE001 - one failed photo should not stop the test
                pass
    (out / "contactos" / "contactos.json").write_text(json.dumps(contacts, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"  {len(contacts)} contactos, {with_photo} fotos de contacto descarregadas.")

    print("\nA ler as fotos (as 20 mais recentes)...")
    try:
        photos = bridge.photos(0, 20)
        for p in photos:
            version = "thumb" if "thumb" in p["versions"] else p["versions"][0]
            data = bridge.photo_bytes(p["id"], version)
            stem = Path(p["filename"] or p["id"]).stem
            (out / "fotos" / f"{(p['created'] or 'sem-data')[:10]}-{stem}.jpg").write_bytes(data)
        print(f"  {len(photos)} miniaturas descarregadas.")
    except PyiCloudServiceNotActivatedException as error:
        print(f"  As Fotos em iCloud não estão disponíveis na web: {error}")
        print("  (Se tens a Proteção Avançada de Dados, ativa 'Aceder aos dados do iCloud na web'.)")
        photos = []

    print(f"\nRESULTADO: iCloud.com OK - {len(contacts)} contactos, {len(photos)} fotos lidas.")
    print(f"Ficheiros em {out}")
    return 0


# --- local API for the Continuum app ----------------------------------------

def run_server(port: int, token: str) -> int:
    bridge = Bridge()

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args: Any) -> None:  # quiet
            pass

        def _authorized(self) -> bool:
            if self.headers.get("Authorization") == f"Bearer {token}":
                return True
            self._send(401, {"error": "unauthorized"})
            return False

        def _send(self, code: int, body: Any, content_type: str = "application/json") -> None:
            data = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode()
            self.send_response(code)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def _json(self) -> dict[str, Any]:
            if "chunked" in (self.headers.get("Transfer-Encoding") or "").lower():
                body = b""
                while True:
                    size = int(self.rfile.readline().split(b";")[0].strip() or b"0", 16)
                    if size == 0:
                        self.rfile.readline()
                        break
                    body += self.rfile.read(size)
                    self.rfile.readline()
            else:
                body = self.rfile.read(int(self.headers.get("Content-Length") or 0))
            return json.loads(body or b"{}")

        def _handle(self, fn: Any) -> None:
            if not self._authorized():
                return
            try:
                with bridge.lock:
                    result = fn()
                if isinstance(result, bytes):
                    self._send(200, result, "application/octet-stream")
                else:
                    self._send(200, result)
            except KeyError:
                self._send(404, {"error": "not found"})
            except (PyiCloudException, RuntimeError) as error:
                self._send(400, {"error": str(error)})
            except Exception as error:  # noqa: BLE001 - always answer, never drop the connection
                with open(DATA_DIR / "bridge.log", "a", encoding="utf-8") as log:
                    log.write(traceback.format_exc() + "\n")
                self._send(500, {"error": f"{type(error).__name__}: {error}"})

        def do_POST(self) -> None:  # noqa: N802
            path = urlparse(self.path).path
            if path == "/resume":
                self._handle(lambda: bridge.resume(self._json()["apple_id"]))
            elif path == "/login":
                self._handle(lambda: (lambda b: bridge.login(b["apple_id"], b["password"]))(self._json()))
            elif path == "/code":
                self._handle(lambda: bridge.submit_code(self._json()["code"]))
            else:
                self._send(404, {"error": "not found"})

        def do_GET(self) -> None:  # noqa: N802
            url = urlparse(self.path)
            query = {k: v[0] for k, v in parse_qs(url.query).items()}
            if url.path == "/health":
                self._send(200, {"ok": True})
            elif url.path == "/contacts":
                self._handle(bridge.contacts)
            elif url.path == "/download":
                self._handle(lambda: bridge.download(query["url"]))
            elif url.path == "/photos":
                self._handle(lambda: bridge.photos(int(query.get("offset", 0)), int(query.get("limit", 100))))
            elif url.path.startswith("/photos/"):
                self._handle(lambda: bridge.photo_bytes(url.path.split("/")[2], query.get("version", "thumb")))
            else:
                self._send(404, {"error": "not found"})

    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(json.dumps({"ready": True, "port": server.server_address[1]}), flush=True)
    server.serve_forever()
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(prog="continuum-icloud")
    sub = parser.add_subparsers(dest="command")
    sub.add_parser("test")
    serve = sub.add_parser("serve")
    serve.add_argument("--port", type=int, default=0)
    args = parser.parse_args()

    if args.command == "serve":
        token = os.environ.get("CONTINUUM_BRIDGE_TOKEN") or secrets.token_urlsafe(24)
        return run_server(args.port, token)

    # Double-clicking the exe runs the test.
    try:
        return run_test()
    except Exception as error:  # noqa: BLE001 - show the user what failed
        print(f"\nRESULTADO: falhou - {type(error).__name__}: {error}")
        return 1
    finally:
        if sys.stdin.isatty():
            input("\nCopia o texto desta janela e envia-mo. Enter para sair.")


if __name__ == "__main__":
    sys.exit(main())
