from __future__ import annotations

import subprocess


def mega_login(email: str, password: str) -> bool:
    cmd = ["mega-login", email, password]
    out = subprocess.run(cmd, shell=True, capture_output=True)
    return out.stderr.decode() == ""


def mega_logout():
    cmd = ["mega-logout"]
    subprocess.run(cmd, shell=True)


def mega_download(url: str, dest: str):
    cmd = ["mega-get", url, dest]
    subprocess.run(cmd, shell=True)


if __name__ == "__main__":
    pass
