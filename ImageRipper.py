import functools
import hashlib
import json
import os
from os import path, walk
import subprocess
import sys
from pathlib import Path
from time import sleep
from typing import Callable
from urllib.parse import urlparse

import PIL
import m3u8_To_MP4
import requests
from PIL import Image
from natsort import natsorted
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.firefox.options import Options
from tldextract import tldextract

from HtmlParser import HtmlParser
from RipInfo import RipInfo
from RipperExceptions import WrongExtension, RipperError
from StatusSync import StatusSync
from rippers import FilenameScheme, read_config, SESSION_HEADERS, DRIVER_HEADER, trim_url, CYBERDROP_DOMAINS, \
    requests_header, log, mark_as_failed, _print_debug_info, url_check, SCHEME, tail

logged_in: bool


class ImageRipper:
    """Image Ripper Class"""

    def __init__(self, filename_scheme: FilenameScheme = FilenameScheme.ORIGINAL):
        self.folder_info: RipInfo = None
        self.status_sync: StatusSync = None
        self.given_url: str = ""
        self.save_path: str = read_config('DEFAULT', 'SavePath')
        self.filename_scheme: FilenameScheme = filename_scheme
        self.site_name: str = ""
        self.session: requests.Session = requests.Session()
        self.session.headers.update(SESSION_HEADERS)
        self.sleep_time: float = 0.2
        self.interrupted: bool = False
        self.logins: dict[str, tuple[str, str]] = {
            "sexy-egirls": (read_config('LOGINS', 'Sexy-EgirlsU'), read_config('LOGINS', 'Sexy-EgirlsP')),
            "deviantart": (read_config('LOGINS', 'DeviantArtU'), read_config('LOGINS', 'DeviantArtP')),
            "porn3dxu": (read_config("LOGINS", "Porn3dxU"), read_config("LOGINS", "Porn3dxP"))
        }
        global logged_in
        logged_in = os.path.isfile("cookies.pkl")
        self.cookies: dict[str, list[str]] = {
            "v2ph": [],
            "fantia": []
        }
        flag: int = 0x08000000  # No-Window flag
        webdriver.common.service.subprocess.Popen = functools.partial(subprocess.Popen, creationflags=flag)
        Path("./Temp").mkdir(parents=True, exist_ok=True)

    @property
    def pause(self):
        # TODO: Remove later
        if self.status_sync is not None:
            return self.status_sync.pause
        return False

    @pause.setter
    def pause(self, value: bool):
        self.status_sync.pause = value

    def rip(self, url: str):
        """Download images from given URL"""
        self.sleep_time = 0.2  # Reset sleep time to 0.2
        self.given_url = url.replace("members.", "www.")
        self.site_name = self.site_check()
        self._image_getter()

    def custom_rip(self, url: str, parser: Callable[[webdriver.Firefox], RipInfo]):
        options = Options()
        options.headless = True
        options.add_argument = DRIVER_HEADER
        driver = webdriver.Firefox(options=options)
        driver.get(url)
        self.folder_info = parser(driver)
        full_path = "".join([self.save_path, self.folder_info.dir_name])
        for i in range(int(self.folder_info.num_urls)):
            sleep(0.2)
            try:
                self.download_from_list(self.session, self.folder_info.urls[i], full_path, i)
            except PIL.UnidentifiedImageError:
                pass  # No image exists, probably
            except requests.exceptions.ChunkedEncodingError:
                sleep(10)
                self.download_from_list(self.session, self.folder_info.urls[i], full_path, i)
        print("Download Complete")

    def set_filename_scheme(self, filename_scheme: FilenameScheme):
        """Set the filename scheme to use when naming downloaded files"""
        self.filename_scheme = filename_scheme

    def _image_getter(self):
        """Download images from URL."""
        html_parser = HtmlParser(self.site_name)
        self.folder_info = html_parser.parse_site(self.given_url)  # Gets image url, number of images, and name of album

        # Save location of this album
        full_path = "".join([self.save_path, self.folder_info.dir_name])
        if self.interrupted and self.filename_scheme != FilenameScheme.HASH:
            pass  # TODO: self.folder_info.urls = self.get_incomplete_files(full_path)

        # Checks if the dir path of this album exists
        Path(full_path).mkdir(parents=True, exist_ok=True)

        # Can get the image through numerically ascending url for imhentai and hentairox
        #   (hard to account for gifs otherwise)
        if self.folder_info.must_generate_manually:
            # Gets the general url of all images in this album
            trimmed_url = trim_url(self.folder_info.urls[0])
            exts = (".jpg", ".gif", ".png", "t.jpg")

            # Downloads all images from the general url by incrementing the file number
            #   (eg. https://domain/gallery/##.jpg)
            for index in range(1, int(self.folder_info.num_urls) + 1):
                file_num = str(index)

                while self.pause:
                    sleep(1)

                # Go through file extensions to find the correct extension (generally will be jpg)
                for i, ext in enumerate(exts):
                    try:
                        self.download_from_url(self.session, trimmed_url, file_num, full_path, ext)
                        break  # Correct extension was found
                    except (PIL.UnidentifiedImageError, WrongExtension):
                        image_path = "".join([full_path, "/", file_num, ext])
                        os.remove(image_path)  # Remove temp file if wrong file extension
                        if i == 3:
                            print("Image not found")
        # Easier to put all image url in a list and then download for these sites
        else:
            # Easier to use cyberdrop-dl for downloading from cyberdrop to avoid image corruption
            if "cyberdrop" == self.site_name:
                cmd = ["cyberdrop-dl", "-o", full_path]
                cmd.extend(self.folder_info.urls)
                print("Starting cyberdrop-dl")
                subprocess.run(cmd)
                print("Cyberdrop-dl finished")
            elif "deviantart" == self.site_name:
                cmd = ["gallery-dl", "-D", full_path, "-u", self.logins["deviantart"][0], "-p",
                       self.logins["deviantart"][1], "--write-log", "log.txt", self.folder_info.urls[0]]
                cmd = " ".join(cmd)
                print("Starting Deviantart download")
                subprocess.run(cmd, stdout=sys.stdout, stderr=subprocess.STDOUT)
            else:
                cyberdrop_files: list[str] = []
                m3u8_files: list[tuple[str, str, str]] = []
                for i, link in enumerate(self.folder_info.urls):
                    while self.pause:
                        sleep(1)
                    sleep(self.sleep_time)
                    if any(domain in link for domain in CYBERDROP_DOMAINS):
                        cyberdrop_files.append(link)
                        continue
                    if ".m3u8" in link:
                        m3u8_files.append((link, full_path, str(i) + ".mp4"))
                        continue
                    try:
                        self.download_from_list(self.session, link, full_path, i)
                    except PIL.UnidentifiedImageError:
                        pass  # No image exists, probably
                    except requests.exceptions.ChunkedEncodingError:
                        sleep(10)
                        self.download_from_list(self.session, link, full_path, i)
                if cyberdrop_files:
                    cmd = ["cyberdrop-dl", "-o", full_path]
                    cmd.extend(cyberdrop_files)
                    print("Starting cyberdrop-dl")
                    subprocess.run(cmd)
                    print("Cyberdrop-dl finished")
                if m3u8_files:
                    # Path(path.join(full_path, "Temp")).mkdir(parents=True, exist_ok=True)
                    for f in m3u8_files:
                        open(path.join(f[1], f[2]), "w").close()
                        for i in range(3):
                            try:
                                # TODO: Use a different library as this can infinite loop
                                #  (and filenames don't get assigned properly)
                                m3u8_To_MP4.multithread_download(f[0], mp4_file_dir=f[1], mp4_file_name=f[2])
                                break
                            except Exception:  # idk wtf gets thrown from the above method
                                sleep(5)
                                if i == 2:
                                    os.remove(path.join(f[1], f[2]))
                                    with open("failed.txt", "a") as file:
                                        file.write(str(f[0]))
                        sleep(0.5)
                    dir_path = m3u8_files[0][1]
                    files = [f for f in os.listdir(dir_path) if path.isfile(path.join(dir_path, f)) and ".mp4" in f]
                    empty_files = []
                    rename_files = []
                    for f in files:
                        if "m3u8_To_Mp4" in f:
                            rename_files.append(f)
                        elif not path.getsize(path.join(dir_path, f)):
                            empty_files.append(f)
                    for f in empty_files:
                        file = path.join(dir_path, f)
                        os.remove(file)
                    if empty_files:
                        if self.filename_scheme == FilenameScheme.CHRONOLOGICAL:
                            for i, f in enumerate(rename_files):
                                old_name = path.join(dir_path, f)
                                new_name = path.join(dir_path, empty_files[i])
                                os.rename(old_name, new_name)
                        elif self.filename_scheme == FilenameScheme.HASH:
                            for f in rename_files:
                                file = path.join(dir_path, f)
                                self.rename_file_to_hash(file, dir_path, ".mp4")
        print("Download Complete")

    def get_incomplete_files(self, dir_path: str) -> list[str]:
        files = [f for f in os.listdir(dir_path) if path.isfile(path.join(dir_path, f))]
        incomplete_files = []
        if self.filename_scheme == FilenameScheme.CHRONOLOGICAL:
            completed_files = [int(f.split(".")[0]) for f in files]
            for i, f in enumerate(self.folder_info.urls):
                if i not in completed_files:
                    incomplete_files.append(f)
        elif self.filename_scheme == FilenameScheme.ORIGINAL:
            for f in self.folder_info.urls:
                filename = f.split("/")[-1]
                if filename not in files:
                    incomplete_files.append(f)
        return incomplete_files if incomplete_files else self.folder_info.urls

    def download_from_url(self, session: requests.Session, url_name: str, file_name: str, full_path: str, ext: str):
        """"Download image from image url"""
        num_files = self.folder_info.num_urls
        # Completes the specific image URL from the general URL
        rip_url = "".join([url_name, str(file_name), ext])
        num_progress = "".join(["(", file_name, "/", str(num_files), ")"])
        print("    ".join([rip_url, num_progress]))
        image_path = "".join([full_path, "/", str(file_name), ext])
        self.download_file(session, image_path, rip_url)
        if self.filename_scheme == FilenameScheme.HASH:
            self.rename_file_to_hash(image_path, full_path, ext)
        # Filenames are chronological by default on imhentai
        sleep(0.05)

    def download_from_list(self, session: requests.Session, image_url: str, full_path: str, current_file_num: int):
        """Download images from url supplied from a list of image urls"""
        num_files = self.folder_info.num_urls
        rip_url = image_url.strip('\n')
        num_progress = "".join(["(", str(current_file_num + 1), "/", str(num_files), ")"])
        print("    ".join([rip_url, num_progress]))
        if "https://forum.sexy-egirls.com/" in rip_url and rip_url[-1] == "/":
            file_name = rip_url.split("/")[-2].split(".")[0].replace("-", ".")
            ext = file_name.split(".")[-1]
            file_name = "".join([hashlib.md5(file_name.encode()).hexdigest(), ".", ext])
        else:
            file_name = os.path.basename(urlparse(rip_url).path)
        image_path = os.path.join(full_path, file_name)
        ext = path.splitext(image_path)[1]
        self.download_file(session, image_path, rip_url)
        if self.filename_scheme == FilenameScheme.HASH:
            self.rename_file_to_hash(image_path, full_path, ext)
        elif self.filename_scheme == FilenameScheme.CHRONOLOGICAL:
            self.rename_file_chronologically(image_path, full_path, ext, current_file_num)
        sleep(0.05)

    def download_file(self, session: requests.Session, image_path: str, rip_url: str):
        """Download the given file"""
        if image_path[-1] == "/":
            image_path = image_path[:-2]
        bad_subdomain = False
        for _ in range(4):
            with open(image_path, "wb") as handle:
                bad_cert = False
                sleep(self.sleep_time)
                try:
                    response = session.get(rip_url, headers=requests_header, stream=True, allow_redirects=True)
                except requests.exceptions.SSLError:
                    response = session.get(rip_url, headers=requests_header, stream=True, verify=False)
                    bad_cert = True
                except requests.exceptions.ConnectionError:
                    log.error("Unable to establish connection to " + rip_url)
                    break
                if not response.ok and not bad_cert:
                    print(self.site_name)
                    if response.status_code == 403 and self.site_name == "kemono" and ".psd" not in rip_url:
                        bad_subdomain = True
                        break
                    print(response)
                    if response.status_code == 404:
                        mark_as_failed(rip_url)
                        if self.site_name != "imhentai":
                            return
                        else:
                            raise WrongExtension
                try:
                    for block in response.iter_content(chunk_size=50000):
                        if not block:
                            break
                        handle.write(block)
                    break
                except ConnectionResetError:
                    print("Connection Reset, Retrying...")
                    sleep(1)
                    continue
        # If unable to download file due to multiple subdomains (e.g. data1, data2, etc.)
        # Context:
        #   https://data1.kemono.party//data/95/47/95477512bd8e042c01d63f5774cafd2690c29e5db71e5b2ea83881c5a8ff67ad.gif]
        #   will fail, however, changing the subdomain to data5 will allow requests to download the file
        #   given that there are generally correct cookies in place
        if bad_subdomain:
            url_parts = tldextract.extract(rip_url)
            subdomain = url_parts.subdomain
            try:
                subdomain_num = int(subdomain[-1])
            except ValueError:
                _print_debug_info("bad_subdomain", url_parts, subdomain, rip_url)
                raise
            if subdomain_num > 20:
                mark_as_failed(rip_url)
                return
            rip_url = rip_url.replace(subdomain, "".join(["data", str(subdomain_num + 1)]))
            print(rip_url)
            self.download_file(session, image_path, rip_url)
        # If the downloaded file doesn't have an extension for some reason, append jpg to filename
        if path.splitext(image_path)[-1] == "":
            os.replace(image_path, image_path + ".jpg")

    def rename_file_chronologically(self, image_path: str, full_path: str, ext: str, curr_num: str | int):
        """Rename the given file to the number of the order it was downloaded in"""
        curr_num = str(curr_num)
        chronological_image_name = "".join([full_path, "/", curr_num, ext])
        # Rename the image with the chronological image name
        try:
            os.replace(image_path, chronological_image_name)
        except OSError:
            with open("failed.txt", "a") as f:
                f.write("".join([self.folder_info.urls[int(curr_num)], "\n"]))

    @staticmethod
    def rename_file_to_hash(image_path: str, full_path: str, ext: str):
        """Rename the given file to the hash of the given file"""
        # md5 hash is used as image name to avoid duplicate names
        md5hash = hashlib.md5(Image.open(image_path).tobytes())
        hash5 = md5hash.hexdigest()
        image_hash_name = "".join([full_path, "/", hash5, ext])
        # If duplicate exists, remove the duplicate
        if os.path.exists(image_hash_name):
            os.remove(image_path)
        else:
            # Otherwise, rename the image with the md5 hash
            os.rename(image_path, image_hash_name)

    def verify_files(self, full_path: str):
        """Check for truncated and corrupted files"""
        _, _, files = next(walk(full_path))
        files = natsorted(files)
        image_links = self.read_partial_save()["https://forum.sexy-egirls.com/threads/ashley-tervort.36594/"][0]
        log_file = open("error.log", "w")
        log_file.close()
        vid_ext = (".m4v", ".mp4", ".mov", ".webm")
        vid_cmd = ["ffmpeg.exe", "-v", "error", "-i", None, "-f", "null", "-", ">error.log", "2>&1"]  # Change idx 4
        for i, f in enumerate(files):
            filename = os.path.join(full_path, f)
            vid_cmd[4] = "".join(['', filename, ''])
            statfile = os.stat(filename)
            filesize = statfile.st_size
            if filesize == 0:
                print("0b filesize")
                print(f)
                self.redownload_files(filename, image_links[i])
            else:
                if any(ext in f for ext in vid_ext):
                    subprocess.call(vid_cmd, stderr=open("error.log", "a"))
                    with open("error.log", "rb") as log_file:
                        cmd_out = tail(log_file).decode()
                        if "[NULL @" not in cmd_out:
                            print(cmd_out)
                            print(f)
                            self.redownload_files(filename, image_links[i])
                else:
                    try:
                        with Image.open(filename) as im:
                            im.verify()
                        with Image.open(filename) as im:  # Image reloading may be needed
                            im.transpose(PIL.Image.FLIP_LEFT_RIGHT)
                    except Exception as e:
                        print(e)
                        print(f)
                        self.redownload_files(filename, image_links[i])

    @staticmethod
    def redownload_files(filename: str, url: str):
        """Redownload damaged files"""
        with open(filename, "wb") as handle:
            response = requests.get(url, headers=requests_header, stream=True)
            if response.ok:
                for block in response.iter_content(chunk_size=50000):
                    if not block:
                        break
                    handle.write(block)

    def site_login(self, driver: webdriver.Firefox):
        curr_url = driver.current_url
        if self.site_name == "sexy-egirls" and "forum." in self.given_url:
            driver.implicitly_wait(10)
            driver.get("https://forum.sexy-egirls.com/login/")
            driver.find_element(By.XPATH, "//input[@type='text']").send_keys(self.logins["sexy-egirls"][0])
            driver.find_element(By.XPATH, "//input[@type='password']").send_keys(self.logins["sexy-egirls"][1])
            driver.find_element(By.XPATH, "//button[@type='submit']").click()
        driver.get(curr_url)

    def write_partial_save(self, site_info: RipInfo):
        """Saves parsed site data to quickly retrieve in event of a failure"""
        # TODO
        data: dict[str, RipInfo | str] = {self.given_url: site_info.serialize(),
                                          "cookies": requests_header["cookie"]}
        with open("partial.json", 'w') as save_file:
            json.dump(data, save_file, indent=4)

    @staticmethod
    def read_partial_save() -> dict[str, RipInfo | str]:
        """Read site_info from partial save file"""
        try:
            with open("partial.json", 'r') as load_file:
                data: dict = json.load(load_file)
                key: str
                for key in data:
                    if isinstance(data[key], dict):
                        data[key] = RipInfo.deserialize(data[key])
                return data
        except FileNotFoundError:
            pass  # Doesn't matter if the cached data doesn't exist, will regen instead

    def site_check(self) -> str:
        """
            Check which site the url is from while also updating requests_header['referer'] to match the domain that
            hosts the files
        """
        if url_check(self.given_url):
            domain = urlparse(self.given_url).netloc
            requests_header['referer'] = "".join([SCHEME, domain, "/"])
            domain = "inven" if "inven.co.kr" in domain else domain.split(".")[-2]
            # Hosts images on a different domain
            if "https://members.hanime.tv/" in self.given_url or "https://hanime.tv/" in self.given_url:
                requests_header['referer'] = "https://cdn.discordapp.com/"
            elif "https://kemono.party/" in self.given_url:
                requests_header['referer'] = ""
            elif "https://e-hentai.org/" in self.given_url:
                self.sleep_time = 5
            return domain
        raise RipperError("Not a support site")


if __name__ == "__main__":
    pass
