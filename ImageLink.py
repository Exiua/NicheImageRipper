from __future__ import annotations

import hashlib
import os
import re
from urllib.parse import urlparse

from FilenameScheme import FilenameScheme
from RipperExceptions import RipperError

class ImageLink:
    def __init__(self, url: str, filename_scheme: FilenameScheme, index: int):
        self.url: str = url
        self.is_m3u8: bool = False
        self.filename: str = self.__generate_filename(url, filename_scheme, index)

    def __str__(self):
        return f"({self.url}, {self.filename}, {self.is_m3u8})"

    @classmethod
    def deserialize(cls, object_data: dict) -> ImageLink:
        image_link = cls("", FilenameScheme.ORIGINAL, 0)
        image_link.url = object_data["url"]
        image_link.filename = object_data["filename"]
        image_link.is_m3u8 = object_data["is_m3u8"]
        return image_link

    def serialize(self) -> dict[str, str | bool]:
        object_data = {
            "url": self.url,
            "filename": self.filename,
            "is_m3u8": self.is_m3u8
        }
        return object_data

    def __generate_filename(self, url: str, filename_scheme: FilenameScheme, index: int) -> str:
        filename = self.__extract_filename(url)
        if filename_scheme == FilenameScheme.ORIGINAL:
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
            self.is_m3u8 = True
        elif "erocdn.co" in url:
            parts = url.split("/")
            ext = parts[-1].split(".")[-1]
            file_name = f"{parts[-2]}.{ext}"
        elif "thothub.lol/" in url and "/?rnd=" in url:
            file_name = url.split("/")[-2]
        else:
            file_name = os.path.basename(urlparse(url).path)
        return file_name


if __name__ == "__main__":
    pass
