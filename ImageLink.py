from __future__ import annotations

import hashlib
import os
import re
from urllib.parse import urlparse, unquote

from Enums import FilenameScheme, LinkInfo
from RipperExceptions import RipperError


class ImageLink:
    def __init__(self, url: str, filename_scheme: FilenameScheme, index: int, filename: str = "",
                 link_info: LinkInfo = LinkInfo.NONE):
        self.referer: str = ""  # Needs to be declared before url
        self.link_info: LinkInfo = link_info
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
    
    def rename(self, new_stem: str | int):
        if isinstance(new_stem, int):
            new_stem = str(new_stem)
        ext = os.path.splitext(self.filename)[1]
        self.filename = new_stem + ext

    def __generate_url(self, url: str) -> str:
        url = url.replace("\n", "")
        if "iframe.mediadelivery.net" in url:
            split = url.split("}")
            playlist_url = split[0]
            self.referer = split[1]
            self.link_info = LinkInfo.IFRAME_MEDIA
            link_url = playlist_url.split("{")[0]
            return link_url
        elif "drive.google.com" in url:
            self.link_info = LinkInfo.GDRIVE
            return url
        elif "mega.nz" in url:
            self.link_info = LinkInfo.MEGA
            return url
        elif "saint.to" in url:
            self.referer = "https://saint.to/"
            return url
        # elif "redgifs.com" in url:
        #     self.link_info = LinkInfo.M3U8
        #     id_ = url.split("/")[3].split(".")[0].lower() # Redgifs IDs are case-sensitive
        #     return f"https://api.redgifs.com/v2/gifs/{id_}/hd.m3u8"
        else:
            return url

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
        elif ("kemono.su/" in url or "coomer.su/" in url) and "?f=" in url:
            file_name = url.split("?f=")[-1]
            if "http" in file_name:
                file_name = url.split("?f=")[0].split("/")[-1]
        elif "phncdn.com" in url:
            file_name = url.split("/")[8]
            self.link_info = LinkInfo.M3U8
        elif "artstation.com" in url:
            file_name = url.split("/")[-1].split("?")
            file_name = f"{file_name[1]}{file_name[0]}"
            self.link_info = LinkInfo.ARTSTATION
        elif "pbs.twimg.com" in url:
            file_name = url.split("/")[-1].split("?")[0]
            ext = re.search(r"format=(\w+)", url)
            if ext:
                file_name = f"{file_name}.{ext.group(1)}"
        else:
            file_name = os.path.basename(urlparse(url).path)
        return file_name


if __name__ == "__main__":
    pass
