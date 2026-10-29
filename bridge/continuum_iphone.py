"""Continuum iPhone data sync.

Copies only what the Messages, Phone and Contacts apps need from the iPhone
over USB (Apple's own backup protocol, filtered): sms.db with attachments,
the call history and the address book with photos. The first run streams the
whole backup from the phone (only the selected files are kept); later runs are
incremental.

Built on pymobiledevice3. Needs Apple's USB driver, which comes with the
Apple Devices app (or iTunes).
"""

from __future__ import annotations

import asyncio
import hashlib
import os
import sqlite3
import sys
from pathlib import Path

from pymobiledevice3.lockdown import create_using_usbmux
from pymobiledevice3.services.mobilebackup2 import Mobilebackup2Service

DATA_DIR = Path(os.environ.get("LOCALAPPDATA", Path.home())) / "Continuum"
BACKUP_DIR = DATA_DIR / "backup"
PAIR_DIR = DATA_DIR / "pair"

KEEP = (
    r"^HomeDomain/Library/SMS/",
    r"^MediaDomain/Library/SMS/Attachments/",
    r"^HomeDomain/Library/CallHistoryDB/",
    r"^HomeDomain/Library/AddressBook/",
)

FILES = {
    "sms": ("HomeDomain", "Library/SMS/sms.db"),
    "calls": ("HomeDomain", "Library/CallHistoryDB/CallHistory.storedata"),
    "contacts": ("HomeDomain", "Library/AddressBook/AddressBook.sqlitedb"),
}


def backup_file(device_dir: Path, domain: str, relative_path: str) -> Path:
    file_id = hashlib.sha1(f"{domain}-{relative_path}".encode()).hexdigest()
    return device_dir / file_id[:2] / file_id


def count(path: Path, sql: str) -> int | None:
    if not path.exists():
        return None
    with sqlite3.connect(f"file:{path}?mode=ro", uri=True) as db:
        return db.execute(sql).fetchone()[0]


async def sync() -> int:
    BACKUP_DIR.mkdir(parents=True, exist_ok=True)
    PAIR_DIR.mkdir(parents=True, exist_ok=True)

    print("À procura do iPhone por USB (se o iPhone perguntar, carrega em Confiar)...")
    lockdown = await create_using_usbmux(pairing_records_cache_folder=PAIR_DIR)
    print(f"Ligado a: {lockdown.display_name} - iOS {lockdown.product_version}\n")

    async with Mobilebackup2Service(lockdown) as backup:
        if await backup.get_will_encrypt():
            print("RESULTADO: o iPhone está configurado para encriptar os backups locais.")
            print("Diz-me se queres que o Continuum peça essa palavra-passe, ou desliga a encriptação")
            print("na app Apple Devices (iPhone > Backups > Encriptar backup local).")
            return 1

        last = -1

        def progress(percent: float) -> None:
            nonlocal last
            if int(percent) // 5 != last:
                last = int(percent) // 5
                print(f"  {int(percent):3d}%", flush=True)

        print("A copiar mensagens, chamadas e contactos (a primeira vez demora, depois é rápido).")
        print("Mantém o iPhone desbloqueado e ligado.")
        await backup.backup(
            full=False,
            backup_directory=str(BACKUP_DIR),
            progress_callback=progress,
            filter_callback=Mobilebackup2Service.regex_filter_callback(KEEP),
        )

    device_dir = BACKUP_DIR / lockdown.udid
    sms = backup_file(device_dir, *FILES["sms"])
    calls = backup_file(device_dir, *FILES["calls"])
    contacts = backup_file(device_dir, *FILES["contacts"])

    chats = count(sms, "SELECT COUNT(*) FROM chat")
    messages = count(sms, "SELECT COUNT(*) FROM message")
    attachments = sum(1 for _ in (device_dir).rglob("*")) if device_dir.exists() else 0
    call_count = count(calls, "SELECT COUNT(*) FROM ZCALLRECORD")
    people = count(contacts, "SELECT COUNT(*) FROM ABPerson")

    print()
    print(
        f"RESULTADO: OK - {chats} conversas, {messages} mensagens, "
        f"{call_count} chamadas, {people} contactos ({attachments} ficheiros guardados)."
    )
    print(f"Guardado em {device_dir}")
    return 0


def main() -> int:
    print("Continuum - sincronizar dados do iPhone")
    print("=======================================\n")
    try:
        return asyncio.run(sync())
    except Exception as error:  # noqa: BLE001 - show the user what failed
        print(f"\nRESULTADO: falhou - {type(error).__name__}: {error}")
        print("Confirma que a app Apple Devices (ou o iTunes) está instalada e que o iPhone está ligado por cabo.")
        return 1
    finally:
        if sys.stdin.isatty():
            input("\nCopia o texto desta janela e envia-mo. Enter para sair.")


if __name__ == "__main__":
    sys.exit(main())
