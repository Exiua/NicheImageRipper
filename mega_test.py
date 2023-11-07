from __future__ import annotations

import sys
import subprocess
from pathlib import Path

DEST_DIR = Path("./Temp/Mega")
DEST_DIR.mkdir(parents=True, exist_ok=True)


def mega_download(url: str):
    cmd = ["mega-logout"]
    subprocess.run(cmd, shell=True)
    cmd = ["mega-login", url]
    subprocess.run(cmd, shell=True)
    cmd = ["mega-pwd"]
    out = subprocess.run(cmd, shell=True, capture_output=True)
    out_dir = DEST_DIR / out.stdout.decode().strip().replace("/", "")
    out_dir.mkdir(parents=True, exist_ok=True)
    cmd = ["mega-get", "-m", url, out_dir]
    subprocess.run(cmd, shell=True)


def main():
    with open("mega_out.txt", "r") as f:
        links = f.readlines()
    for l in links:
        print(l)


if __name__ == "__main__":
   # main()
    mega_download(sys.argv[1])
