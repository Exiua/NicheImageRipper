from __future__ import annotations

import re
import sys


def extract_url(url: str) -> str:
    url = url.replace("</a>", "")
    if "drive.google.com" in url:
        return gdrive_link_parse(url)
    elif "mega.nz" in url:
        return mega_link_parse(url)
    else:
        start = url.find("https:")
        return url[start:] if start != -1 else url


def gdrive_link_parse(url: str) -> str:
    start = url.find("https:")
    if start == -1:
        return ""
    m = re.search(r"(\?usp=sharing|\?usp=share_link|\?id=)", url)
    if m is not None:
        match m.group(1):
            case "?usp=sharing":
                end = m.span(1)[0] + len("?usp=sharing")
            case "?usp=share_link":
                end = m.span(1)[0] + len("?usp=share_link")
            case "?id=":
                end = m.span(1)[0] + len("?id=") + 33
            case _:
                print(f"Unable to identify gdrive url by parameter: {url}", file=sys.stderr)
                return ""
        
        return url[start:end] if len(url) >= end else ""
    
    m = re.search(r"(/folders/|/file/d/)", url)
    if m is not None:
        match m.group(1):
            case "/folders/":
                end = m.span(1)[0] + len("/folders/") + 33
            case "/file/d/":
                end = m.span(1)[0] + len("/file/d/") + 33
            case _:
                print(f"Unable to identify gdrive url by path: {url}", file=sys.stderr)
                return ""

        return url[start:end] if len(url) >= end else ""
    
    print(f"Unable to identify gdrive url: {url}", file=sys.stderr)
    return ""


def mega_link_parse(url: str) -> str:
    start = url.find("https:")
    if start == -1:
        return ""
    m = re.search(r"(/folder/|/#F!|/#!|/file/)", url)
    if m is None:
        print("Unable to identify mega url", file=sys.stderr)
        return ""
    match m.group(1):
        case "/folder/":
            end = m.span(1)[0] + len("/folder/") + 31
        case "/#F!":
            end = m.span(1)[0] + len("/#F!") + 31
        case "/#!":
            end = m.span(1)[0] + len("/#!") + 52
        case "/file/":
            end = m.span(1)[0] + len("/file/") + 52
        case _:
            print(f"Unable to identify mega url by path: {url}", file=sys.stderr)
            return ""

    return url[start:end] if len(url) >= end else ""


if __name__ == "__main__":
    pass
