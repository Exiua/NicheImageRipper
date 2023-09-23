from enum import IntEnum


class FilenameScheme(IntEnum):
    ORIGINAL = 0
    HASH = 1
    CHRONOLOGICAL = 2


class UnzipProtocol(IntEnum):
    NONE = 0,
    EXTRACT = 1,
    EXTRACT_DELETE = 2


class LinkInfo(IntEnum):
    NONE = 0,
    M3U8 = 1,
    GDRIVE = 2,
    IFRAME_MEDIA = 3

    def __str__(self):
        return self.name


if __name__ == "__main__":
    raise Exception("This file is not meant to be ran directly")
