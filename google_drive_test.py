from __future__ import annotations

import pickle
import os.path
import json
import io
import string
import urllib.request
import time
from os import path, makedirs, remove, rename
from pathlib import Path

from getfilelistpy import getfilelist
from googleapiclient.discovery import build
from googleapiclient.http import MediaIoBaseDownload
from google_auth_oauthlib.flow import InstalledAppFlow
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials

from Config import Config

# If modifying these scopes, delete the file token.pickle.
SCOPES = ['https://www.googleapis.com/auth/drive.readonly']


def download_googledrive_folder(gdrive_url, local_dir, gdrive_api_key):
    success = True
    try:
        id_, single_file = extract_id(gdrive_url)
        resource = {
            "api_key": gdrive_api_key,
            "id": id_,
            "oauth2": authenticate(),
            "fields": "files(name,id)",
        }
        res = getfilelist.GetFileList(resource)
        with open("test.json", "w") as f:
            json.dump(res, f, indent=4)
        destination = local_dir
        base_folder_name = res["searchedFolder"]["name"] if not single_file else res["searchedFolder"]["id"]
        base_folder_name = __clean_dir_name(base_folder_name)
        destination_path = path.join(destination, base_folder_name)
        makedirs(destination, exist_ok=True)
        if single_file:
            filename = res["searchedFolder"]["name"]
            file_id = id_
            download_file(destination_path, filename, file_id)
        else:
            num_files = res['totalNumberOfFiles']
            print(f"Found {num_files} files")
            file_list = res["fileList"]
            folder_names = res["folderTree"]["names"]
            for _ in range(4):
                download_files(file_list, folder_names, gdrive_api_key, destination_path)
                if len(__recursively_get_files(destination_path)) != num_files:
                    print("Rate Limit Reached | Retrying in 5 min...")
                    time.sleep(5 * 60)
                    download_files(file_list, folder_names, gdrive_api_key, destination_path)
                else:
                    break
            if len(__recursively_get_files(destination_path)) != num_files:
                with open("gdrive_incomplete.log", "a") as f:
                    f.write(gdrive_url + "\n")

    except Exception as err:
        print(err)
        with open("gdrive_error.log", "a") as f:
            f.write(gdrive_url + "\n")
        success = False

    return success


def download_files(file_list: list[dict], folder_names: list[str], gdrive_api_key: str, destination_path: str):
    for i, file in enumerate(file_list):
        folder_name = __clean_dir_name(folder_names[i])
        print(folder_name)
        for file_dict in file["files"]:
            folder_path = path.join(destination_path, folder_name)
            filename = file_dict["name"]
            file_id = file_dict["id"]
            download_file(folder_path, filename, file_id)


def download_file(folder_path: str, filename: str, file_id: str):
    print(f"Downloading {filename}")
    destination_file = path.join(folder_path, filename)
    Path(destination_file).parent.mkdir(parents=True, exist_ok=True)
    drive_service = build("drive", "v3", credentials=authenticate())
    request = drive_service.files().get_media(fileId=file_id)
    fh = io.FileIO(destination_file, "wb")
    downloader = MediaIoBaseDownload(fh, request)
    done = False
    while done is False:
        status, done = downloader.next_chunk()
        print(f"Download {int(status.progress() * 100):d}%.")


def __clean_dir_name(dir_name: str) -> str:
    """Remove forbidden characters from name"""
    translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*.'), None)
    dir_name = dir_name.translate(translation_table).strip().replace("\n", "")
    if dir_name[-1] not in (")", "]", "}"):
        dir_name.rstrip(string.punctuation)
    if dir_name[0] not in ("(", "[", "{"):
        dir_name.lstrip(string.punctuation)
    return dir_name


def __recursively_get_files(filepath: str) -> list[str]:
    return [os.path.join(dirpath, f) for (dirpath, _, filenames) in os.walk(filepath) for f in filenames]


def download_links():
    with open("drive.google.com_links.txt", "r", encoding="utf-16") as f:
        links = f.readlines()

    for i, link in enumerate(links):
        base_folder = "testRips"
        url = link.strip()
        print(url)
        try:
            if download_googledrive_folder(url, base_folder, key):
                links[i] = ""
        except Exception as e:
            print(e)
            continue

    collapsed_links = "".join(links)
    expanded_links = collapsed_links.split("\n")
    expanded_links = [link + "\n" for link in expanded_links]

    with open("drive.google.com_links.txt", "w", encoding="utf-16") as f:
        f.writelines(expanded_links)


def extract_id(url: str) -> tuple[str, bool]:
    parts = url.split("/")
    if "/d/" in url:
        return parts[-2], True
    else:
        return parts[-1].split('?')[0], False


def test():
    id_ = "1pTw2Rf1XYkDOlQ5p-T2IQBap5lVBGm6d"
    source = f"https://www.googleapis.com/drive/v3/files/{id_}?alt=media&key={key}"
    destination_file = "./Temp/202005FanBox reward.zip"
    urllib.request.urlretrieve(source, destination_file)


def authenticate():
    creds = None

    if os.path.exists('token.json'):
        creds = Credentials.from_authorized_user_file('token.json', SCOPES)

    # If there are no (valid) credentials available, let the user log in.
    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            creds.refresh(Request())
        else:
            flow = InstalledAppFlow.from_client_secrets_file("credentials.json", SCOPES)
            creds = flow.run_local_server(port=0)
        # Save the credentials for the next run
        with open("token.json", "w") as token:
            token.write(creds.to_json())

    return creds


if __name__ == "__main__":
    remote = "https://drive.google.com/file/d/1pTw2Rf1XYkDOlQ5p-T2IQBap5lVBGm6d/view?usp=sharing"
    # remote = "https://drive.google.com/drive/folders/1QXl1IH5v0TvZ5VqO7nj8TiZToeRgn0xt"
    local = "Temp"
    key = Config.config["Keys"]["Google"]
    download_links()
    # print(extract_id(remote))
    # print(download_googledrive_folder(remote, local, key))
    # test()
