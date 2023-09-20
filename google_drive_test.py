from __future__ import annotations

import io
import json
import os.path
import string
import sys
import time
from os import path, makedirs
from pathlib import Path

from getfilelistpy import getfilelist
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build
from googleapiclient.http import MediaIoBaseDownload

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
            folder_ids = res["folderTree"]["id"]
            folder_names = res["folderTree"]["names"]
            folder_names = get_folder_names(folder_ids, folder_names)
            for _ in range(4):
                download_files(file_list, folder_names, destination_path)
                if len(__recursively_get_files(destination_path)) != num_files:
                    print("Rate Limit Reached | Retrying in 5 min...")
                    time.sleep(5 * 60)
                    download_files(file_list, folder_names, destination_path)
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


def download_files(file_list: list[dict], folder_names: list[str], destination_path: str):
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
    FILE = "drive.google.com_links.txt"  # "gdriveLinks.txt"
    with open(FILE, "r", encoding="utf-16") as f:
        links = f.readlines()

    base_folder = "testRips"
    for i, link in enumerate(links):
        if not link:
            continue
        url = link.strip()
        print(url)
        try:
            if download_googledrive_folder(url, base_folder, key):
                links[i] = ""
        except Exception as e:
            print(e)
            print(link)
            continue

    count = 0
    while count < len(links):
        if links[count] == "":
            links.pop(count)
        else:
            if links[count][-1] != '\n':
                links[count] += '\n'
            count += 1

    with open(FILE, "w", encoding="utf-16") as f:
        f.writelines(links)


def extract_id(url: str) -> tuple[str, bool]:
    parts = url.split("/")
    if "/d/" in url:
        return parts[-2], True
    else:
        id_ = parts[-1].split('?')[0]
        if id_ in ("open", "folderview"):
            id_ = parts[-1].split('?id=')[-1]
        return id_, False


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


def get_files(url: str):
    id_, single_file = extract_id(url)
    resource = {
        # "api_key": gdrive_api_key,
        "id": id_,
        "oauth2": authenticate(),
        "fields": "files(name,id)",
    }
    res = getfilelist.GetFileList(resource)
    with open("test.json", "w") as f:
        json.dump(res, f, indent=4)


def get_folder_names(folder_ids: list[list[str]], names: list[str]) -> list[str]:
    hierarchy = {}
    for i, name in enumerate(names):
        folder_id = folder_ids[i]
        complete = False
        for id_ in folder_id:
            parent = hierarchy.get(id_, None)
            if not parent:
                hierarchy[id_] = name
                complete = True
                break
        if complete:
            continue
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


if __name__ == "__main__":
    local = "Temp"
    key = Config.config["Keys"]["Google"]
    authenticate()
    download_links()
    #remote = "https://drive.google.com/file/d/1uzBOJZ5e8vtkIA73q7PFIoOrVsJ50Ofi/view?usp=sharing"
    #print(extract_id(remote))
    #print(download_googledrive_folder(sys.argv[1], local, key))
    # test()
    # get_files(sys.argv[1])
