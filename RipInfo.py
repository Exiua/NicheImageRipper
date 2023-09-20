from __future__ import annotations

import os
import string
from typing import Iterator

import google.auth.exceptions
import googleapiclient.errors
from getfilelistpy import getfilelist
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow

from Enums import FilenameScheme
from ImageLink import ImageLink

SCOPES = ['https://www.googleapis.com/auth/drive.readonly']


class RipInfo:
    """Ripped Site Information"""

    translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*.'), None)
    gdrive_creds: Credentials = None

    def __init__(self, urls: list[str | ImageLink] | str, dir_name: str = "",
                 filename_scheme: FilenameScheme = FilenameScheme.ORIGINAL,
                 generate: bool = False, num_urls: int = 0, filenames: list[str] = None):
        if isinstance(urls, str) or isinstance(urls, ImageLink):
            urls = [urls]
        self.filename_scheme: FilenameScheme = filename_scheme
        self.__dir_name: str = dir_name
        self.__filenames: list[str] = filenames
        if self.__dir_name:
            self.__dir_name = self.__clean_dir_name(self.__dir_name)
        self.urls: list[ImageLink] = self.__convert_urls_to_image_link(urls)
        self.must_generate_manually: bool = generate
        self.url_count = num_urls if generate else len(self.urls)

    def __str__(self) -> str:
        return f"({[str(url) for url in self.urls]}, {self.num_urls}, {self.dir_name})"

    def __iter__(self) -> Iterator[ImageLink]:
        return iter(self.urls)

    @property
    def num_urls(self):
        return self.url_count

    @property
    def dir_name(self):
        return self.__dir_name

    @dir_name.setter
    def dir_name(self, value):
        self.__dir_name = value
        self.__dir_name = self.__clean_dir_name(self.__dir_name)

    def __convert_urls_to_image_link(self, urls: list[str | ImageLink]) -> list[ImageLink]:
        image_links = []
        link_counter = 0
        filename_counter = 0
        urls = self.__remove_duplicates(urls)
        for url in urls:
            if isinstance(url, ImageLink):
                image_links.append(url)
                continue
            if "drive.google.com" in url:
                try:
                    image_link, link_counter = self.__query_gdrive_links(url, link_counter)
                    image_links.extend(image_link)
                except googleapiclient.errors.HttpError:
                    pass
            else:
                filename = self.__filenames[filename_counter] if self.__filenames else ""
                filename_counter += 1
                image_link = ImageLink(url, self.filename_scheme, link_counter, filename=filename)
                image_links.append(image_link)
                link_counter += 1
        return image_links

    @staticmethod
    def __remove_duplicates(urls: list[str | ImageLink]) -> list[str | ImageLink]:
        unique_urls: list[str | ImageLink] = []
        seen_urls = set()
        for url in urls:
            if isinstance(url, ImageLink):
                link = url.url
            else:
                link = url

            if link in seen_urls:
                continue
            else:
                unique_urls.append(url)
                seen_urls.add(link)
        return unique_urls

    def __query_gdrive_links(self, gdrive_url: str, index: int) -> tuple[list[ImageLink], int]:
        self.authenticate()
        id_, single_file = self.__extract_id(gdrive_url)
        resource = {
            "id": id_,
            "oauth2": self.gdrive_creds,
            "fields": "files(name,id)",
        }
        res = getfilelist.GetFileList(resource)
        if not self.__dir_name:
            dir_name = res["searchedFolder"]["name"] if not single_file else res["searchedFolder"]["id"]
            self.__dir_name = self.__clean_dir_name(dir_name)
        links: list[ImageLink] = []
        counter = index
        if single_file:
            filename = res["searchedFolder"]["name"]
            file_id = id_
            img_link = ImageLink(file_id, self.filename_scheme, counter, filename=filename, gdrive=True)
            links.append(img_link)
            counter += 1
        else:
            file_lists = res["fileList"]
            folder_ids = res["folderTree"]["id"]
            folder_names = res["folderTree"]["names"]
            folder_names = self.get_gdrive_folder_names(folder_ids, folder_names)
            for i, file_list in enumerate(file_lists):
                files = file_list["files"]
                parent_folder = folder_names[i]
                for file in files:
                    file_id = file["id"]
                    filename = os.path.join(parent_folder, file["name"])
                    img_link = ImageLink(file_id, self.filename_scheme, counter, filename=filename, gdrive=True)
                    links.append(img_link)
                    counter += 1
        return links, counter

    def get_gdrive_folder_names(self, folder_ids: list[list[str]], names: list[str]) -> list[str]:
        # Stores the mapping from folder id to folder name
        hierarchy = {}
        for i, name in enumerate(names):
            folder_id = folder_ids[i]
            complete = False
            for id_ in folder_id:
                parent = hierarchy.get(id_, None)
                if not parent:
                    hierarchy[id_] = self.__clean_dir_name(name)
                    complete = True
                    break
            if complete:
                continue

        # Using the id -> name mapping, resolve the full path of each file
        folder_names = []
        for id_set in folder_ids:
            path_ = ""
            for id_ in id_set:
                if not path_:
                    path_ = hierarchy[id_]
                else:
                    path_ = os.path.join(path_, hierarchy[id_])
            folder_names.append(path_)
        return folder_names

    @staticmethod
    def __extract_id(url: str) -> tuple[str, bool]:
        parts = url.split("/")
        if "/d/" in url:
            return parts[-2], True
        else:
            id_ = parts[-1].split('?')[0]
            if id_ in ("open", "folderview"):
                id_ = parts[-1].split('?id=')[-1]
            return id_, False

    @staticmethod
    def authenticate():
        if os.path.exists('token.json'):
            RipInfo.gdrive_creds = Credentials.from_authorized_user_file('token.json', SCOPES)

        # If there are no (valid) credentials available, let the user log in.
        if not RipInfo.gdrive_creds or not RipInfo.gdrive_creds.valid:
            if RipInfo.gdrive_creds and RipInfo.gdrive_creds.expired and RipInfo.gdrive_creds.refresh_token:
                try:
                    RipInfo.gdrive_creds.refresh(Request())
                except google.auth.exceptions.RefreshError:
                    flow = InstalledAppFlow.from_client_secrets_file("credentials.json", SCOPES)
                    RipInfo.gdrive_creds = flow.run_local_server(port=0)
            else:
                flow = InstalledAppFlow.from_client_secrets_file("credentials.json", SCOPES)
                RipInfo.gdrive_creds = flow.run_local_server(port=0)

            # Save the credentials for the next run
            with open("token.json", "w") as token:
                token.write(RipInfo.gdrive_creds.to_json())

    def __clean_dir_name(self, dir_name: str) -> str:
        """
            Remove forbidden characters from path
        """
        dir_name = dir_name.translate(self.translation_table).strip().replace("\n", "")
        if dir_name[-1] not in (")", "]", "}"):
            dir_name.rstrip(string.punctuation)
        if dir_name[0] not in ("(", "[", "{"):
            dir_name.lstrip(string.punctuation)
        return dir_name

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
