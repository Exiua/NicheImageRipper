from enum import IntEnum


class CustomIntEnum(IntEnum):
    def __str__(self):
        return self.name


class FilenameScheme(CustomIntEnum):
    ORIGINAL = 0
    HASH = 1
    CHRONOLOGICAL = 2


class UnzipProtocol(CustomIntEnum):
    NONE = 0
    EXTRACT = 1
    EXTRACT_DELETE = 2


class LinkInfo(CustomIntEnum):
    NONE = 0
    M3U8 = 1
    GDRIVE = 2
    IFRAME_MEDIA = 3
    MEGA = 4


class QueueResult(CustomIntEnum):
    SUCCESS = 0
    ALREADY_QUEUED = 1
    NOT_SUPPORTED = 2


if __name__ == "__main__":
    raise Exception("This file is not meant to be ran directly")
