from __future__ import annotations

import string

from FilenameScheme import FilenameScheme
from ImageLink import ImageLink


class RipInfo:
    """Ripped Site Information"""

    translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*.'), None)

    def __init__(self, urls: list[str] | str, dir_name: str = "", filename_scheme: FilenameScheme = FilenameScheme.ORIGINAL,
                 generate: bool = False, num_urls: int = 0):
        if isinstance(urls, str):
            urls = [urls]
        self.filename_scheme: FilenameScheme = filename_scheme
        self.urls: list[ImageLink] = self.__convert_str_to_image_link(urls)
        self._dir_name: str = dir_name
        self.must_generate_manually: bool = generate
        self.url_count = num_urls if generate else len(urls)
        if self._dir_name:
            self.__clean_dir_name()

    def __str__(self) -> str:
        return f"({self.urls}, {self.num_urls}, {self.dir_name})"

    @property
    def num_urls(self):
        return self.url_count

    @property
    def dir_name(self):
        return self._dir_name

    @dir_name.setter
    def dir_name(self, value):
        self._dir_name = value
        self.__clean_dir_name()

    def __convert_str_to_image_link(self, urls: list[str]) -> list[ImageLink]:
        return [ImageLink(url, self.filename_scheme, i) for i, url in enumerate(urls)]

    def __clean_dir_name(self):
        """Remove forbidden characters from name"""
        self._dir_name = self._dir_name.translate(self.translation_table).strip().replace("\n", "")
        if self._dir_name[-1] not in (")", "]", "}"):
            self._dir_name.rstrip(string.punctuation)
        if self._dir_name[0] not in ("(", "[", "{"):
            self._dir_name.lstrip(string.punctuation)

    @classmethod
    def deserialize(cls, object_data: dict) -> RipInfo:
        rip_info = cls([], "")
        rip_info.urls = [ImageLink.deserialize(data) for data in object_data["urls"]]
        rip_info.url_count = object_data["url_count"]
        rip_info.dir_name = object_data["dir_name"]
        rip_info.must_generate_manually = object_data["must_generate_manually"]
        return rip_info

    def serialize(self) -> dict[str, any]:
        object_data = {
            "urls": [img_lnk.serialize() for img_lnk in self.urls],
            "url_count": self.url_count,
            "dir_name": self.dir_name,
            "must_generate_manually": self.must_generate_manually
        }
        return object_data


if __name__ == "__main__":
    pass
