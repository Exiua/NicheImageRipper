from __future__ import annotations

import base64
import functools
import io
import logging.handlers
import os
import pickle
import re
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path
from time import sleep
from typing import BinaryIO
from urllib.parse import urlparse

import PIL
import ffmpeg
import requests
import yt_dlp
from PIL import Image
from googleapiclient.discovery import build
from googleapiclient.http import MediaIoBaseDownload
from natsort import natsorted
from requests import Response
from selenium import webdriver
from selenium.webdriver.common.by import By

from Config import Config
from Enums import FilenameScheme, UnzipProtocol, LinkInfo
from HtmlParser import HtmlParser
from ImageLink import ImageLink
from MegaApi import mega_login, mega_whoami, mega_download
from RipInfo import RipInfo
from RipperExceptions import BadSubdomain, WrongExtension, RipperError, FileNotFoundAtUrl, ImproperlyFormattedSubdomain
from StatusSync import StatusSync
from Util import url_check, SCHEME, get_login_creds
from b_cdn_drm_vod_dl import BunnyVideoDRM

SESSION_HEADERS: dict[str, str] = {
    "User-Agent":
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36"
}
CYBERDROP_DOMAINS: tuple[str, str, str, str] = ("cyberdrop.me", "cyberdrop.cc", "cyberdrop.to", "cyberdrop.nl")

# Setup Logging
handler = logging.handlers.WatchedFileHandler(os.environ.get("LOGFILE", "error.log"))
formatter = logging.Formatter(logging.BASIC_FORMAT)
handler.setFormatter(formatter)
log = logging.getLogger()
log.setLevel(os.environ.get("LOGLEVEL", "INFO"))
log.addHandler(handler)


class ImageRipper:
    """Image Ripper Class"""

    status_sync: StatusSync = None

    def __init__(self, filename_scheme: FilenameScheme = FilenameScheme.ORIGINAL,
                 unzip_protocol: UnzipProtocol = UnzipProtocol.NONE):
        self.requests_header: dict[str, str] = {
            'User-Agent':
                'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36',
            'referer':
                'https://imhentai.xxx/',
            'cookie':
                '',
            'Authorization':
                ''
        }
        self.filename_scheme: FilenameScheme = filename_scheme
        self.unzip_protocol: UnzipProtocol = unzip_protocol
        self.folder_info: RipInfo = None
        self.given_url: str = ""
        self.interrupted: bool = False
        self.auto_extract: bool = False
        self.logins: dict[str, dict[str, str]] = Config.config.logins
        self.logged_in: bool = os.path.isfile("cookies.pkl")
        self.persistent_logins: dict[str, bool] = {}
        self.save_path: str = Config.config['SavePath']
        self.session: requests.Session = requests.Session()
        self.site_name: str = ""
        self.sleep_time: float = 0.2
        self.current_index: int = 0

        # region Extra Initialization

        self.session.headers.update(SESSION_HEADERS)
        flag: int = 0x08000000  # No-Window flag
        webdriver.common.service.subprocess.Popen = functools.partial(subprocess.Popen, creationflags=flag)
        Path("./Temp").mkdir(parents=True, exist_ok=True)

        # endregion

    @property
    def pause(self):
        return ImageRipper.status_sync.pause

    @pause.setter
    def pause(self, value: bool):
        ImageRipper.status_sync.pause = value

    def rip(self, url: str):
        """Download images from given URL"""
        self.sleep_time = 0.2  # Reset sleep time to 0.2
        self.given_url = url.replace("members.", "www.")  # Replace is done to properly parse hanime pages
        self.site_name = self.__site_check()
        if self.__cookies_needed():
            self.__add_cookies()
        self.__file_getter()

    def __add_cookies(self):
        if not os.path.isfile("cookies.pkl"):
            return
        cookies = pickle.load(open("cookies.pkl", "rb"))
        cookie: dict
        for cookie in cookies:
            if cookie['name'] != 'xf_user':
                continue
            if self.requests_header["cookie"] and self.requests_header["cookie"][-1] != ";":
                self.requests_header["cookie"][-1] += ";"
            self.requests_header["cookie"] += f'{cookie["name"]}={cookie["value"]};'
            self.session.cookies.set(cookie['name'], cookie['value'], domain=cookie['domain'])

    def __cookies_needed(self) -> bool:
        return self.site_name == "titsintops"

    def __site_check(self) -> str:
        """
            Check which site the url is from while also updating requests_header['referer'] to match the domain that
            hosts the files
        """
        if url_check(self.given_url):
            domain = urlparse(self.given_url).netloc
            self.requests_header['referer'] = "".join([SCHEME, domain, "/"])
            domain = "inven" if "inven.co.kr" in domain else domain.split(".")[-2]
            # Hosts images on a different domain
            if any(url in self.given_url for url in ("https://members.hanime.tv/", "https://hanime.tv/")):
                self.requests_header['referer'] = "https://cdn.discordapp.com/"
            elif "https://kemono.party/" in self.given_url:
                self.requests_header['referer'] = "https://kemono.party/"
            elif "https://e-hentai.org/" in self.given_url:
                self.sleep_time = 5
            return domain
        raise RipperError(f"Not a support site: {self.given_url}")

    def __file_getter(self):
        """
            Download files from URL
        """
        html_parser = HtmlParser(self.requests_header, self.site_name, self.filename_scheme)
        self.folder_info = html_parser.parse_site(self.given_url)  # Gets image url, number of images, and name of album
        # Save location of this album
        full_path = os.path.join(self.save_path, self.folder_info.dir_name)
        if self.interrupted and self.filename_scheme != FilenameScheme.HASH:
            pass  # TODO: self.folder_info.urls = self.get_incomplete_files(full_path)

        # Checks if the dir path of this album exists
        Path(full_path).mkdir(parents=True, exist_ok=True)

        rip_index = Path(".ripIndex")
        if rip_index.exists():
            with rip_index.open("r") as f:
                start = int(f.read())
            rip_index.unlink()
        else:
            if self.folder_info.must_generate_manually:
                start = 1
            else:
                start = 0

        # Can get the image through numerically ascending url for imhentai and hentairox
        #   (hard to account for gifs and other extensions otherwise)
        if self.folder_info.must_generate_manually:
            # Gets the general url of all images in this album
            trimmed_url = self.__trim_url(self.folder_info.urls[0].url)
            extensions = (".jpg", ".gif", ".png", "mp4", "t.jpg")

            # Downloads all images from the general url by incrementing the file number
            #   (eg. https://domain/gallery/##.jpg)
            for index in range(start, int(self.folder_info.num_urls) + 1):
                self.current_index = index
                file_num = str(index)

                while self.pause:
                    sleep(1)

                # Go through file extensions to find the correct extension (generally will be jpg)
                for i, ext in enumerate(extensions):
                    try:
                        self.__download_from_url(trimmed_url, file_num, full_path, ext)
                        break  # Correct extension was found
                    except (PIL.UnidentifiedImageError, WrongExtension):
                        if i == 3:
                            print("Image not found")
        # Easier to put all image url in a list and then download for these sites
        else:
            # Easier to use cyberdrop-dl for downloading from cyberdrop to avoid image corruption
            if "cyberdrop" == self.site_name:
                self.__cyberdrop_download(full_path, [ImageLink(self.given_url, FilenameScheme.ORIGINAL, 0)])
            elif "deviantart" == self.site_name:
                self.__deviantart_download(full_path, self.folder_info.urls[0].url)
            else:
                cyberdrop_files: list[ImageLink] = []
                for i, link in enumerate(self.folder_info.urls[start:]):
                    i = start + i
                    self.current_index = i
                    while self.pause:
                        sleep(1)
                    sleep(self.sleep_time)
                    if any(domain in link.url for domain in CYBERDROP_DOMAINS):
                        cyberdrop_files.append(link)
                        continue
                    try:
                        self.__download_from_list(link, full_path, i)
                    except PIL.UnidentifiedImageError:
                        pass  # No image exists, probably
                    except requests.exceptions.ChunkedEncodingError:
                        sleep(10)
                        self.__download_from_list(link, full_path, i)
                    except FileNotFoundError:
                        if link.link_info == LinkInfo.IFRAME_MEDIA:
                            with open("failed_iframe.txt", "a") as f:
                                f.write(f"{link.url} {link.referer}\n")
                    except Exception:
                        with open(".ripIndex", "w") as f:
                            f.write(str(self.current_index))
                        raise
                if cyberdrop_files:
                    self.__cyberdrop_download(full_path, cyberdrop_files)

        if self.unzip_protocol != UnzipProtocol.NONE:
            self.__unzip_files(full_path)
        print("{#00FF00}Download Complete")

    def __unzip_files(self, dir_path: str):
        """
            Recursively unzip all files in a given directory
        :param dir_path: Path of directory to unzip files in
        """
        path = Path(dir_path)
        content = path.rglob("*")
        count = 0
        error = 0
        for item in content:
            if item.is_dir():
                continue
            if item.suffix == ".zip":
                try:
                    self.__unzip_file(item)
                    count += 1
                except RuntimeError:
                    print(f"Failed to extract: {item}")
                    error += 1
                    continue
        print(f"Results:\n\tExtracted: {count}\n\tFailed: {error}")

    def __unzip_file(self, zip_path: Path):
        """
            Unzip a given file
        :param zip_path: File to unzip
        """
        extract_path = zip_path.with_suffix("")
        extract_path.mkdir(parents=True, exist_ok=True)
        try:
            with zipfile.ZipFile(zip_path, "r") as f:
                f.extractall(extract_path)
        except zipfile.BadZipfile:
            ext = self.__get_correct_ext(str(zip_path))
            new_path = zip_path.with_suffix(ext)
            zip_path.rename(new_path)
            return
        if self.unzip_protocol == UnzipProtocol.EXTRACT_DELETE:
            zip_path.unlink()

    def __cyberdrop_download(self, full_path: str, cyberdrop_files: list[ImageLink]):
        cmd = ["gallery-dl", "-D", full_path]
        cmd.extend([file.url for file in cyberdrop_files])
        print(cmd)
        self.__run_subprocess(cmd, "Starting gallery-dl", "Gallery-dl finished")

    def __deviantart_download(self, full_path: str, url: str):
        cmd = ["gallery-dl", "-D", full_path, "-u", self.logins["DeviantArt"]["Username"], "-p",
               self.logins["DeviantArt"]["Password"], "--write-log", "log.txt", url]
        # cmd = " ".join(cmd)
        self.__run_subprocess(cmd, start_message="Starting Deviantart download")

    @staticmethod
    def __run_subprocess(cmd: list[str], start_message: str = None, end_message: str = None):
        """
            Run the given subprocess
        :param cmd: List of commands and arguments to run
        :param start_message: Message to display before running subprocess
        :param end_message: Message to display after running subprocess
        """
        if start_message:
            print(start_message)
        subprocess.run(cmd, stdout=sys.stdout, stderr=subprocess.STDOUT)
        if end_message:
            print(end_message)

    def __download_from_url(self, url_name: str, file_name: str, full_path: str, ext: str):
        """"
            Download image from image url
        :param url_name: Base url to use to download the file from
        :param file_name: Name of the file to download
        :param full_path: Full path of the directory to download the file to
        :param ext: Extension of the file to download
        """
        num_files = self.folder_info.num_urls
        # Completes the specific image URL from the general URL
        rip_url = "".join([url_name, str(file_name), ext])
        num_progress = f"({file_name}/{str(num_files)})"  # "".join(["(", file_name, "/", str(num_files), ")"])
        print("    ".join([rip_url, num_progress]))
        image_path = os.path.join(full_path, file_name + ext)  # "".join([full_path, "/", str(file_name), ext])
        self.__download_file(image_path, rip_url)
        sleep(0.05)

    def __download_from_list(self, image_link: ImageLink, full_path: str, current_file_num: int):
        """
            Download images from url supplied from a list of image urls
        :param image_link: ImageLink containing data on the file to download
        :param full_path: Full path of the directory to save the file to
        :param current_file_num: Number of the file being downloaded
        """
        num_files = self.folder_info.num_urls
        rip_url = image_link.url
        num_progress = f"({str(current_file_num + 1)}/{str(num_files)})"
        print("    ".join([rip_url, num_progress]))
        file_name = image_link.filename
        image_path = os.path.join(full_path, file_name)
        if image_link.link_info == LinkInfo.M3U8:
            self.__download_m3u8_to_mp4(image_path, rip_url)
        elif image_link.link_info == LinkInfo.GDRIVE:
            self.__download_gdrive_file(image_path, image_link)
        elif image_link.link_info == LinkInfo.IFRAME_MEDIA:
            self.__download_iframe_media(image_path, image_link)
        elif image_link.link_info == LinkInfo.MEGA:
            self.__download_mega_files(image_path, image_link)
        elif image_link.link_info == LinkInfo.PIXELDRAIN:
            self.__download_pixeldrain_files(image_path, image_link)
        else:
            self.__download_file(image_path, rip_url)
        sleep(0.05)

    @staticmethod
    def __download_m3u8_to_mp4(video_path: str, video_url: str):
        input_stream = ffmpeg.input(video_url, protocol_whitelist="file,http,https,tcp,tls,crypto")
        output_stream = ffmpeg.output(input_stream, video_path, c="copy")
        ffmpeg.run(output_stream)

    @staticmethod
    def __download_gdrive_file(folder_path: str, image_link: ImageLink):
        destination_file = os.path.join(folder_path, image_link.filename)
        Path(destination_file).parent.mkdir(parents=True, exist_ok=True)
        RipInfo.authenticate()
        drive_service = build("drive", "v3", credentials=RipInfo.gdrive_creds)
        request = drive_service.files().get_media(fileId=image_link.url)  # Url will be the file id when handling gdrive
        fh = io.FileIO(destination_file, "wb")
        downloader = MediaIoBaseDownload(fh, request)
        done = False
        while done is False:
            status, done = downloader.next_chunk()
            print(f"Downloaded {int(status.progress() * 100):d}%", end="\r")
        print()

    def __download_mega_files(self, folder_path: str, image_link: ImageLink):
        email, password = get_login_creds("Mega")
        if "Mega" not in self.persistent_logins:
            if mega_whoami() == email:
                self.persistent_logins["Mega"] = True
            else:
                self.persistent_logins["Mega"] = mega_login(email, password)
        else:
            if not self.persistent_logins["Mega"]:
                if mega_whoami() == email:
                    self.persistent_logins["Mega"] = True
                else:
                    self.persistent_logins["Mega"] = mega_login(email, password)

        if not self.persistent_logins["Mega"]:
            raise Exception("Unable to login to MegaCmd")

        mega_download(image_link.url, folder_path)

    def __download_pixeldrain_files(self, image_path: str, image_link: ImageLink):
        api_key = Config.config.keys["Pixeldrain"]
        auth_string = f":{api_key}"
        base64_auth = base64.b64encode(auth_string.encode()).decode()
        headers = {
            "User-Agent": ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
                           "Chrome/107.0.0.0 Safari/537.36"),
            "Authorization": f"Basic {base64_auth}",
        }
        response = requests.get(f"https://pixeldrain.com/api/file/{image_link.url}", headers=headers, stream=True)
        with open(image_path, "wb") as f:
            for block in response.iter_content(chunk_size=10240):
                if block:
                    f.write(block)

    def __download_iframe_media(self, folder_path: str, image_link: ImageLink):
        for i in range(4):
            try:
                destination_file = Path(folder_path)
                destination_file.parent.mkdir(parents=True, exist_ok=True)
                video = BunnyVideoDRM(
                    # insert the referer between the quotes below (address of your webpage)
                    referer=image_link.url,
                    # paste your embed link
                    embed_url=image_link.referer,
                    # you can override file name, no extension
                    name=destination_file.name,
                    # you can override download path
                    path=str(destination_file.parent))
                video.download()
                for f in destination_file.parent.glob(".*"):
                    shutil.rmtree(f)
                break
            except (yt_dlp.utils.DownloadError, PermissionError):
                if i == 3:
                    self.__log_failed_url(image_link.url)

    def __download_file(self, image_path: str, rip_url: str):
        """Download the given file"""
        if image_path[-1] == "/":
            image_path = image_path[:-2]

        try:
            for _ in range(4):  # Try 4 times before giving up
                if self.__download_file_helper(rip_url, image_path):
                    break
            return  # Failed to download file
        # If unable to download file due to multiple subdomains (e.g. data1, data2, etc.)
        # Context:
        #   https://c1.kemono.party/data/95/47/95477512bd8e042c01d63f5774cafd2690c29e5db71e5b2ea83881c5a8ff67ad.gif]
        #   will fail, however, changing the subdomain to c5 will allow requests to download the file
        #   given that there are correct cookies in place
        except BadSubdomain:
            self.__dot_party_subdomain_handler(rip_url, image_path)

        # If the downloaded file doesn't have an extension for some reason, search for correct ext
        if os.path.splitext(image_path)[-1] == "":
            ext = self.__get_correct_ext(image_path)
            os.replace(image_path, image_path + ext)

    def __download_file_helper(self, url: str, image_path: str) -> bool:
        bad_cert = False
        sleep(self.sleep_time)

        try:
            response = self.session.get(url, headers=self.requests_header, stream=True, allow_redirects=True)
        except requests.exceptions.SSLError:
            try:
                response = self.session.get(url, headers=self.requests_header, stream=True, verify=False)
                bad_cert = True
            except requests.exceptions.SSLError:
                return False
        except requests.exceptions.ConnectionError:
            log.error("Unable to establish connection to " + url)
            return False

        if not response.ok and not bad_cert:
            if response.status_code == 403 and self.site_name == "kemono" and ".psd" not in url:
                raise BadSubdomain

            print(response)

            if response.status_code == 404:
                self.__log_failed_url(url)

                if self.site_name != "imhentai":
                    return False
                else:
                    raise WrongExtension

        if not self.__write_to_file(response, image_path):
            self.__log_failed_url(url)
            return False

        return True

    def __dot_party_subdomain_handler(self, url: str, image_path: str):
        subdomain_search = re.search(r"//c(\d)+", url)

        if subdomain_search:
            subdomain_num = int(subdomain_search.group(1))
        else:
            self.__print_debug_info("bad_subdomain", url)
            raise ImproperlyFormattedSubdomain

        for i in range(1, 100):
            if i == subdomain_num:
                continue

            rip_url = re.sub(r"//c\d+", f"//c{str(i)}", url)

            try:
                self.__download_party_file(image_path, rip_url)
            except BadSubdomain:
                print(f"\rTrying subdomain c{str(i)}...", end="")
                if i == 99:
                    self.__log_failed_url(re.sub(r"//c\d+", f"//c{str(subdomain_num)}", url))
                    return
        print(url)

    def __download_party_file(self, image_path: str, rip_url: str):
        sleep(self.sleep_time)

        try:
            response = self.session.get(rip_url, headers=self.requests_header, stream=True, allow_redirects=True)
        except requests.exceptions.ConnectionError:
            log.error("Unable to establish connection to " + rip_url)
            return

        if not response.ok:
            if response.status_code == 403 and ".psd" not in rip_url:
                raise BadSubdomain

            print(response)

            if response.status_code == 404:
                self.__log_failed_url(rip_url)
                raise FileNotFoundAtUrl(rip_url)

        if not self.__write_to_file(response, image_path):
            self.__log_failed_url(rip_url)

    @staticmethod
    def __write_to_file(response: Response, file_path: str) -> bool:
        """
            Write response data to file
        :param response: Response to write to file
        :param file_path: Filepath to write to
        :return: Boolean based on successfulness
        """
        save_path = Path(file_path).expanduser().absolute()
        for _ in range(4):
            try:
                with save_path.open("wb") as f:
                    for block in response.iter_content(chunk_size=50000):
                        if block:
                            f.write(block)
                return True
            except ConnectionResetError:
                print("Connection Reset, Retrying...")
                sleep(1)
            except OSError:
                print(f"Failed to open file: {str(save_path)}")
        return False

    @staticmethod
    def __get_correct_ext(filepath: str) -> str:
        """
            Get correct extension for a file based on file signature
        :param filepath: Path to the file to analyze
        :return: True extension of the file or the original extension if the file signature is unknown (will default to
        .bin if the file does not have a file extension)
        """
        orig_filepath = Path(filepath)
        with orig_filepath.open("rb") as f:
            file_sig = f.read(8)
            if file_sig[:6] == b"\x52\x61\x72\x21\x1A\x07":  # is rar
                return ".rar"
            elif file_sig == b"\x89\x50\x4E\x47\x0D\x0A\x1A\x0A":  # is png
                return ".png"
            elif file_sig[:3] == b"\xff\xd8\xff":  # is jpg
                return ".jpg"
            elif file_sig[:4] in b"\x47\x49\x46\x38":  # is gif
                return ".gif"
            elif file_sig[:4] == b"\x50\x4B\x03\x04":  # is zip
                return ".zip"
            elif file_sig[:4] == b"\x38\x42\x50\x53":  # is psd
                return ".psd"
            elif file_sig[:4] == b"\x25\x50\x44\x46":  # is pdf
                return ".pdf"
            elif file_sig[:6] == b"\x37\x7A\xBC\xAF\x27\x1C":  # is 7z
                return ".7z"
            elif file_sig[:4] == b"\x1A\x45\xDF\xA3":
                return ".webm"
            elif file_sig[:4] == b"\x52\x49\x46\x46":
                return ".webp"
            elif file_sig[4:] == b"\x66\x74\x79\x70":
                return ".mp4"
            elif file_sig == b"\x43\x53\x46\x43\x48\x55\x4E\x4B":
                return ".clip"
            else:
                print(f"Unable to identify file {filepath} with signature {file_sig}")
                if orig_filepath.suffix == "":
                    return ".bin"
                else:
                    return orig_filepath.suffix

    def __verify_files(self, full_path: str):
        """
            Check for truncated and corrupted files
        """
        _, _, files = next(os.walk(full_path))
        files = natsorted(files)
        image_links = []  # self.read_partial_save()["https://forum.sexy-egirls.com/threads/ashley-tervort.36594/"][0]
        log_file = open("error.log", "w")
        log_file.close()
        vid_ext = (".m4v", ".mp4", ".mov", ".webm")
        vid_cmd = ["ffmpeg.exe", "-v", "error", "-i", None, "-f", "null", "-", ">error.log", "2>&1"]  # Change idx 4
        for i, f in enumerate(files):
            filename = os.path.join(full_path, f)
            vid_cmd[4] = f"{filename}"
            stat_file = os.stat(filename)
            filesize = stat_file.st_size
            if filesize == 0:
                print("0b filesize")
                print(f)
                self.__redownload_files(filename, image_links[i])
            else:
                if any(ext in f for ext in vid_ext):
                    subprocess.call(vid_cmd, stderr=open("error.log", "a"))
                    with open("error.log", "rb") as log_file:
                        cmd_out = self.__tail(log_file).decode()
                        if "[NULL @" not in cmd_out:
                            print(cmd_out)
                            print(f)
                            self.__redownload_files(filename, image_links[i])
                else:
                    try:
                        with Image.open(filename) as im:
                            im.verify()
                        with Image.open(filename) as im:  # Image reloading may be needed
                            im.transpose(PIL.Image.FLIP_LEFT_RIGHT)
                    except Exception as e:
                        print(e)
                        print(f)
                        self.__redownload_files(filename, image_links[i])

    def __redownload_files(self, filename: str, url: str):
        """
            Re-download damaged files
        """
        response = requests.get(url, headers=self.requests_header, stream=True)
        self.__write_to_file(response, filename)

    @staticmethod
    def __tail(f: BinaryIO, lines=2) -> bytes:
        total_lines_wanted = lines

        BLOCK_SIZE = 1024
        f.seek(0, os.SEEK_END)
        block_end_byte = f.tell()
        lines_to_go = total_lines_wanted
        block_number = -1
        blocks = []
        while lines_to_go > 0 and block_end_byte > 0:
            if block_end_byte - BLOCK_SIZE > 0:
                f.seek(block_number * BLOCK_SIZE, os.SEEK_END)
                blocks.append(f.read(BLOCK_SIZE))
            else:
                f.seek(0, os.SEEK_SET)
                blocks.append(f.read(block_end_byte))
            lines_found = blocks[-1].count(b'\n')
            lines_to_go -= lines_found
            block_end_byte -= BLOCK_SIZE
            block_number -= 1
        all_read_text = b''.join(reversed(blocks))
        return b'\n'.join(all_read_text.splitlines()[-total_lines_wanted:])

    @staticmethod
    def __print_debug_info(title: str, *data, fd="output.txt", clear=False):
        with open(fd, "w" if clear else "a") as f:
            f.write(f"[{title}]\n")
            for d in data:
                f.write(f"\t{str(d).strip()}\n")

    @staticmethod
    def __log_failed_url(url: str):
        with open("failed.txt", "a", encoding="unicode_escape") as f:
            f.write(f"{url}\n")

    @staticmethod
    def __trim_url(given_url: str) -> str:
        """Return the URL without the filename attached."""
        given_url = "".join([str("/".join(given_url.split("/")[0:-1])), "/"])
        return given_url


if __name__ == "__main__":
    pass
