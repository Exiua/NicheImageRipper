from __future__ import annotations

import functools
import hashlib
import logging.handlers
import json
import os
import re
import subprocess
import sys
import pickle
from pathlib import Path
from time import sleep
from typing import BinaryIO
from urllib.parse import urlparse

import PIL
import requests
from PIL import Image
from natsort import natsorted
from requests import Response
from selenium import webdriver
from selenium.webdriver.common.by import By

from FilenameScheme import FilenameScheme
from Config import Config
from HtmlParser import HtmlParser
from RipInfo import RipInfo
from RipperExceptions import BadSubdomain, WrongExtension, RipperError, FileNotFoundAtUrl, ImproperlyFormattedSubdomain
from StatusSync import StatusSync
from Util import url_check, SCHEME

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

    def __init__(self, filename_scheme: FilenameScheme = FilenameScheme.ORIGINAL):
        self.cookies: dict[str, list[str]] = {
            "v2ph": [],
            "fantia": []
        }
        self.requests_header: dict[str, str] = {
            'User-Agent':
                'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36',
            'referer':
                'https://imhentai.xxx/',
            'cookie':
                ''
        }
        self.filename_scheme: FilenameScheme = filename_scheme
        self.folder_info: RipInfo = RipInfo("")
        self.given_url: str = ""
        self.interrupted: bool = False
        self.logins: dict[str, dict[str, str]] = Config.config.logins
        self.logged_in: bool = os.path.isfile("cookies.pkl")
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
        self._image_getter()

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

    def _image_getter(self):
        """Download images from URL."""
        html_parser = HtmlParser(self.requests_header, self.site_name)
        self.folder_info = html_parser.parse_site(self.given_url)  # Gets image url, number of images, and name of album

        # Save location of this album
        full_path = os.path.join(self.save_path, self.folder_info.dir_name)
        if self.interrupted and self.filename_scheme != FilenameScheme.HASH:
            pass  # TODO: self.folder_info.urls = self.get_incomplete_files(full_path)

        # Checks if the dir path of this album exists
        Path(full_path).mkdir(parents=True, exist_ok=True)

        if os.path.exists(".ripIndex"):
            with open(".ripIndex", "r") as f:
                start = int(f.read())
            os.remove(".ripIndex")
        else:
            if self.folder_info.must_generate_manually:
                start = 1
            else:
                start = 0

        # Can get the image through numerically ascending url for imhentai and hentairox
        #   (hard to account for gifs otherwise)
        if self.folder_info.must_generate_manually:
            # Gets the general url of all images in this album
            trimmed_url = self.__trim_url(self.folder_info.urls[0])
            extensions = (".jpg", ".gif", ".png", "t.jpg")

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
                        #image_path = os.path.join(full_path, file_num + ext)  # "".join([full_path, "/", file_num, ext])
                        #os.remove(image_path)  # Remove temp file if wrong file extension
                        if i == 3:
                            print("Image not found")
        # Easier to put all image url in a list and then download for these sites
        else:
            # Easier to use cyberdrop-dl for downloading from cyberdrop to avoid image corruption
            if "cyberdrop" == self.site_name:
                self.__cyberdrop_download(full_path, self.folder_info.urls)
            elif "deviantart" == self.site_name:
                self.__deviantart_download(full_path, self.folder_info.urls[0])
            else:
                cyberdrop_files: list[str] = []
                for i, link in enumerate(self.folder_info.urls[start:]):
                    self.current_index = i
                    while self.pause:
                        sleep(1)
                    sleep(self.sleep_time)
                    if any(domain in link for domain in CYBERDROP_DOMAINS):
                        cyberdrop_files.append(link)
                        continue
                    try:
                        self.__download_from_list(link, full_path, i)
                    except PIL.UnidentifiedImageError:
                        pass  # No image exists, probably
                    except requests.exceptions.ChunkedEncodingError:
                        sleep(10)
                        self.__download_from_list(link, full_path, i)
                if cyberdrop_files:
                    self.__cyberdrop_download(full_path, cyberdrop_files)
        print("Download Complete")

    @staticmethod
    def __cyberdrop_download(full_path: str, cyberdrop_files: list[str]):
        cmd = ["cyberdrop-dl", "-o", full_path]
        cmd.extend(cyberdrop_files)
        print("Starting cyberdrop-dl")
        subprocess.run(cmd)
        print("Cyberdrop-dl finished")

    def __deviantart_download(self, full_path: str, url: str):
        cmd = ["gallery-dl", "-D", full_path, "-u", self.logins["deviantart"][0], "-p",
               self.logins["deviantart"][1], "--write-log", "log.txt", url]
        cmd = " ".join(cmd)
        print("Starting Deviantart download")
        subprocess.run(cmd, stdout=sys.stdout, stderr=subprocess.STDOUT)

    def __download_from_url(self, url_name: str, file_name: str, full_path: str, ext: str):
        """"Download image from image url"""
        num_files = self.folder_info.num_urls
        # Completes the specific image URL from the general URL
        rip_url = "".join([url_name, str(file_name), ext])
        num_progress = f"({file_name}/{str(num_files)})"  # "".join(["(", file_name, "/", str(num_files), ")"])
        print("    ".join([rip_url, num_progress]))
        image_path = os.path.join(full_path, file_name + ext)  # "".join([full_path, "/", str(file_name), ext])
        self.__download_file(image_path, rip_url)

        if self.filename_scheme == FilenameScheme.HASH:
            self.__rename_file_to_hash(image_path, full_path, ext)

        # Filenames are chronological by default on imhentai
        sleep(0.05)

    def __download_from_list(self, image_url: str, full_path: str, current_file_num: int):
        """Download images from url supplied from a list of image urls"""
        num_files = self.folder_info.num_urls
        rip_url = image_url.strip('\n')
        num_progress = f"({str(current_file_num + 1)}/{str(num_files)})"
        print("    ".join([rip_url, num_progress]))

        if "https://titsintops.com/" in rip_url and rip_url[-1] == "/":
            file_name = rip_url.split("/")[-2]#.split(".")[0].replace("-", ".")
            #print(file_name)
            file_name = re.sub(r"-(jpg|png|webp)\.\d+\/?", r".\1", file_name)
            #print(file_name)
        else:
            file_name = os.path.basename(urlparse(rip_url).path)

        image_path = os.path.join(full_path, file_name)
        ext = os.path.splitext(image_path)[1]
        self.__download_file(image_path, rip_url)

        if self.filename_scheme == FilenameScheme.HASH:
            self.__rename_file_to_hash(image_path, full_path, ext)
        elif self.filename_scheme == FilenameScheme.CHRONOLOGICAL:
            self.__rename_file_chronologically(image_path, full_path, ext, current_file_num)

        sleep(0.05)

    def __download_file(self, image_path: str, rip_url: str):
        """Download the given file"""
        if image_path[-1] == "/":
            image_path = image_path[:-2]

        try:
            for _ in range(4):  # Try 4 times before giving up
                if self.__download_file_helper(rip_url, image_path):
                    break
        # If unable to download file due to multiple subdomains (e.g. data1, data2, etc.)
        # Context:
        #   https://data1.kemono.party//data/95/47/95477512bd8e042c01d63f5774cafd2690c29e5db71e5b2ea83881c5a8ff67ad.gif]
        #   will fail, however, changing the subdomain to data5 will allow requests to download the file
        #   given that there are generally correct cookies in place
        except BadSubdomain:
            self.__dot_party_subdomain_handler(rip_url, image_path)

        # If the downloaded file doesn't have an extension for some reason, default to jpg
        if os.path.splitext(image_path)[-1] == "":
            ext = self.__get_correct_ext(image_path)
            os.replace(image_path, image_path + ext)

    def __download_file_helper(self, url: str, image_path: str) -> bool:
        bad_cert = False
        sleep(self.sleep_time)

        try:
            response = self.session.get(url, headers=self.requests_header, stream=True, allow_redirects=True)
        except requests.exceptions.SSLError:
            response = self.session.get(url, headers=self.requests_header, stream=True, verify=False)
            bad_cert = True
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
        subdomain_search = re.search(r"data(\d)+", url)

        if subdomain_search:
            subdomain_num = int(subdomain_search.group(1))
        else:
            self.__print_debug_info("bad_subdomain", url)
            raise ImproperlyFormattedSubdomain

        for i in range(1, 100):
            if i == subdomain_num:
                continue

            rip_url = re.sub(r"data\d+", f"data{str(i)}", url)

            try:
                self.__download_party_file(image_path, rip_url)
            except BadSubdomain:
                print(f"\rTrying subdomain data{str(i)}...", end="")
                if i == 99:
                    self.__log_failed_url(re.sub(r"data\d+", f"data{str(subdomain_num)}", url))
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

    def __rename_file_chronologically(self, image_path: str, full_path: str, ext: str, curr_num: str | int):
        """Rename the given file to the number of the order it was downloaded in"""
        curr_num = str(curr_num)
        chronological_image_name = os.path.join(full_path, curr_num + ext)  # "".join([full_path, "/", curr_num, ext])
        # Rename the image with the chronological image name
        try:
            os.replace(image_path, chronological_image_name)
        except OSError:
            with open("failed.txt", "a") as f:
                f.write("".join([url + "\n" for url in self.folder_info.urls[int(curr_num)]]))

    @staticmethod
    def __rename_file_to_hash(image_path: str, full_path: str, ext: str):
        """Rename the given file to the hash of the given file"""
        # md5 hash is used as image name to avoid duplicate names
        md5hash = hashlib.md5(Image.open(image_path).tobytes())
        hash5 = md5hash.hexdigest()
        image_hash_name = os.path.join(full_path, hash5 + ext)  # "".join([full_path, "/", hash5, ext])

        # If duplicate exists, remove the duplicate
        if os.path.exists(image_hash_name):
            os.remove(image_path)
        else:
            # Otherwise, rename the image with the md5 hash
            os.rename(image_path, image_hash_name)

    @staticmethod
    def __write_to_file(response: Response, file_path: str) -> bool:
        for _ in range(4):
            try:
                with open(file_path, "wb") as f:
                    for block in response.iter_content(chunk_size=50000):
                        if block:
                            f.write(block)
                    return True
            except ConnectionResetError:
                print("Connection Reset, Retrying...")
                sleep(1)
        return False

    @staticmethod
    def __get_correct_ext(filepath: str) -> str:
        with open(filepath, "rb") as f:
            file_sig = os.read(f.fileno(), 8)
            if file_sig[:6] == b"\x52\x61\x72\x21\x1A\x07":  # is rar
                return ".rar"
            elif file_sig == b"\x89\x50\x4E\x47\x0D\x0A\x1A\x0A":  # is png
                return ".png"
            elif file_sig[:3] == b"\xff\xd8\xff":  # is jpg
                return ".jpg"
            elif file_sig[:4] == b"\x47\x49\x46\x38":  # is gif
                return ".gif"
            elif file_sig[:4] == b"\x50\x4B\x03\x04":  # is zip
                return ".zip"
            elif file_sig[:4] == b"\x38\x42\x50\x53":  # is psd
                return ".psd"
            else:
                print(f"Unable to identify file {filepath} with signature {file_sig}")
                return ".jpg"

    def __verify_files(self, full_path: str):
        """Check for truncated and corrupted files"""
        _, _, files = next(os.walk(full_path))
        files = natsorted(files)
        image_links = []  # self.read_partial_save()["https://forum.sexy-egirls.com/threads/ashley-tervort.36594/"][0]
        log_file = open("error.log", "w")
        log_file.close()
        vid_ext = (".m4v", ".mp4", ".mov", ".webm")
        vid_cmd = ["ffmpeg.exe", "-v", "error", "-i", None, "-f", "null", "-", ">error.log", "2>&1"]  # Change idx 4
        for i, f in enumerate(files):
            filename = os.path.join(full_path, f)
            vid_cmd[4] = "".join(['', filename, ''])
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
        """Redownload damaged files"""
        response = requests.get(url, headers=self.requests_header, stream=True)
        self.__write_to_file(response, filename)

    @staticmethod
    def __tail(f: BinaryIO, lines=2) -> bytes:
        total_lines_wanted = lines

        BLOCK_SIZE = 1024
        f.seek(0, 2)
        block_end_byte = f.tell()
        lines_to_go = total_lines_wanted
        block_number = -1
        blocks = []
        while lines_to_go > 0 and block_end_byte > 0:
            if block_end_byte - BLOCK_SIZE > 0:
                f.seek(block_number * BLOCK_SIZE, 2)
                blocks.append(f.read(BLOCK_SIZE))
            else:
                f.seek(0, 0)
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
            f.write("".join([url, "\n"]))

    @staticmethod
    def __trim_url(given_url: str) -> str:
        """Return the URL without the filename attached."""
        given_url = "".join([str("/".join(given_url.split("/")[0:-1])), "/"])
        return given_url


if __name__ == "__main__":
    pass
