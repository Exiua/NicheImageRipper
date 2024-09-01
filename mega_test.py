from __future__ import annotations

import sys
import subprocess
from pathlib import Path

from Config import Config

DEST_DIR = Path("./Temp/Mega")
DEST_DIR.mkdir(parents=True, exist_ok=True)


def mega_download(url: str):
    # cmd = ["mega-logout"]
    # subprocess.run(cmd, shell=True)
    # cmd = ["mega-login", url]
    # subprocess.run(cmd, shell=True)
    # cmd = ["mega-pwd"]
    # out = subprocess.run(cmd, shell=True, capture_output=True)
    # out_dir = DEST_DIR / out.stdout.decode().strip().replace("/", "")
    # out_dir.mkdir(parents=True, exist_ok=True)
    cmd = ["mega-get", url, DEST_DIR]
    subprocess.run(cmd, shell=True)


def mega_download2(url: str):
    creds = Config.config.logins["Mega"]
    username = creds["Username"]
    password = creds["Password"]
    cmd = ["mega-login", username, f'"""{password}"""']
    subprocess.run(cmd, shell=True)
    cmd = ["mega-whoami"]
    out = subprocess.run(cmd, shell=True, capture_output=True)
    print(out.stdout.decode())


def main():
    with open("mega_out.txt", "r") as f:
        links: list[str] = f.readlines()
    success = []
    try:
        link: str
        for i, link in enumerate(links):
            mega_download(link.strip())
            success.append(i)
    finally:
        for i in reversed(success):
            links.pop(i)
        with open("mega_out.txt", "w") as f:
            f.writelines(links)



if __name__ == "__main__":
    main()
    # mega_download2(sys.argv[1])
