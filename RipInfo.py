import string


class RipInfo:
    """Ripped Site Information"""

    def __init__(self, urls: list[str], dir_name: str, generate: bool = False, num_urls: int = 0):
        self.urls: list[str] = urls
        self.dir_name: str = dir_name
        self.must_generate_manually: bool = generate
        self.url_count = num_urls if generate else len(urls)
        self.clean_dir_name()

    @property
    def num_urls(self):
        return self.url_count

    def clean_dir_name(self):
        """Remove forbidden characters from name"""
        translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*.'), None)
        self.dir_name = self.dir_name.translate(translation_table).strip().replace("\n", "")
        if self.dir_name[-1] not in (")", "]", "}"):
            self.dir_name.rstrip(string.punctuation)
        if self.dir_name[0] not in ("(", "[", "{"):
            self.dir_name.lstrip(string.punctuation)

    @classmethod
    def deserialize(cls, object_data: dict):
        rip_info = cls([], "", )
        rip_info.urls = object_data["urls"]
        rip_info.url_count = object_data["url_count"]
        rip_info.dir_name = object_data["dir_name"]
        rip_info.must_generate_manually = object_data["must_generate_manually"]
        return rip_info

    def serialize(self):
        object_data = {
            "urls": self.urls,
            "url_count": self.url_count,
            "dir_name": self.dir_name,
            "must_generate_manually": self.must_generate_manually
        }
        return object_data
