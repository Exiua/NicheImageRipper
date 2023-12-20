from __future__ import annotations

import hashlib
import os
import re
from urllib.parse import urlparse, unquote

from Enums import FilenameScheme, LinkInfo
from RipperExceptions import RipperError


class ImageLink:
    def __init__(self, url: str, filename_scheme: FilenameScheme, index: int, filename: str = "", gdrive: bool = False):
        self.referer: str = ""  # Needs to be declared before url
        self.link_info: LinkInfo = LinkInfo.GDRIVE if gdrive else LinkInfo.NONE
        self.url: str = self.__generate_url(url)
        self.filename: str = self.__generate_filename(url, filename_scheme, index, filename)

    def __str__(self):
        if self.referer:
            return f"({self.url}, {self.filename}, {self.referer}, {self.link_info})"
        return f"({self.url}, {self.filename}, {self.link_info})"

    def __eq__(self, other):
        if isinstance(other, str):
            return self.url == other
        if not isinstance(other, ImageLink):
            return False
        return self.url == other.url

    @property
    def is_blob(self) -> bool:
        return self.url.startswith("data:")

    @classmethod
    def deserialize(cls, object_data: dict) -> ImageLink:
        image_link = cls("", FilenameScheme.ORIGINAL, 0)
        image_link.url = object_data["url"]
        image_link.filename = object_data["filename"]
        image_link.referer = object_data["referer"]
        image_link.link_info = LinkInfo(object_data["link_info"])
        return image_link

    def serialize(self) -> dict[str, str | bool]:
        object_data = {
            "url": self.url,
            "filename": self.filename,
            "referer": self.referer,
            "link_info": self.link_info
        }
        return object_data

    def __generate_url(self, url: str) -> str:
        if "iframe.mediadelivery.net" in url:
            split = url.split("}")
            playlist_url = split[0]
            self.referer = split[1]
            # match = re.search(r"[^{]+{(\d+)", playlist_url)
            # resolution = self.resolution_lookup(match.group(1))
            self.link_info = LinkInfo.IFRAME_MEDIA
            # link_url = playlist_url.split("{")[0].replace("/playlist.drm", f"/{resolution}/video.drm")
            link_url = playlist_url.split("{")[0]
            return link_url  # f"https://iframe.mediadelivery.net/{guid}/{resolution}/video.drm?contextId={context_id}"
        elif "drive.google.com" in url:
            self.link_info = LinkInfo.GDRIVE
            return url
        elif "mega.nz" in url:
            self.link_info = LinkInfo.MEGA
            return url
        else:
            return url

    @staticmethod
    def resolution_lookup(resolution: str) -> str:
        if resolution == "360":
            return "640x360"
        if resolution == "480":
            return "640x480"
        if resolution == "720":
            return "1280x720"
        if resolution == "1080":
            return "1920x1080"
        if resolution == "1280":
            return "720x1280"
        if resolution == "1440":
            return "2560x1440"
        if resolution == "1920":
            return "1080x1920"
        if resolution == "2160":
            return "3840x2160"
        if resolution == "2560":
            return "1440x2560"
        if resolution == "3840":
            return "2160x3840"
        raise Exception(f"Invalid Resolution: {resolution}")

    def __generate_filename(self, url: str, filename_scheme: FilenameScheme, index: int, filename: str = "") -> str:
        if not filename:
            filename = self.__extract_filename(url)

        if filename_scheme == FilenameScheme.ORIGINAL:
            if "%" in filename:
                filename = unquote(filename)
            return filename
        else:
            ext = os.path.splitext(filename)[1]  # Contains '.' delimiter
            if filename_scheme == FilenameScheme.HASH:
                md5hash = hashlib.md5(url.encode("utf-8"))
                hash5 = md5hash.hexdigest()
                return hash5 + ext
            elif filename_scheme == FilenameScheme.CHRONOLOGICAL:
                return str(index) + ext
            else:
                raise RipperError(f"FilenameScheme out of bounds: {filename_scheme}")

    def __extract_filename(self, url: str) -> str:
        if "https://titsintops.com/" in url and url[-1] == "/":
            file_name = url.split("/")[-2]
            file_name = re.sub(r"-(jpg|png|webp|mp4|mov|avi|wmv)\.\d+/?", r".\1", file_name)
        elif "sendvid.com" in url and ".m3u8" in url:
            file_name = url.split("/")[6]
            self.link_info = LinkInfo.M3U8
        elif "iframe.mediadelivery.net" in url:
            file_name = url.split("/")[-2]
            # file_name += ".mp4"
        elif "erocdn.co" in url:
            parts = url.split("/")
            ext = parts[-1].split(".")[-1]
            file_name = f"{parts[-2]}.{ext}"
        elif "thothub.lol/" in url and "/?rnd=" in url:
            file_name = url.split("/")[-2]
        elif ("kemono.party/" in url or "coomer.party/" in url) and "?f=" in url:
            file_name = url.split("?f=")[-1]
            if "http" in file_name:
                file_name = url.split("?f=")[0].split("/")[-1]
        else:
            file_name = os.path.basename(urlparse(url).path)
        return file_name


if __name__ == "__main__":
    pass
