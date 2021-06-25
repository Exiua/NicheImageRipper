"""This module downloads images from given URL"""
import hashlib
import os
from os import path
import sys
import string
import configparser
import time
from math import ceil
import functools
import subprocess
from pathlib import Path
from urllib.parse import urlparse
import PIL
from PIL import Image
import requests
import bs4
from bs4 import BeautifulSoup
from selenium import webdriver
from selenium.webdriver.firefox.options import Options

class RipperError(Exception):
    """General Ripper Exceptions"""

PROTOCOL = "https:"
CONFIG = 'config.ini'
PARSER = "html.parser"
DRIVER_HEADER = ("user-agent=Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z‡ Safari/537.36")
requests_header = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36',
                    'referer': 'https://imhentai.xxx/'}

class ImageRipper():
    """Image Ripper Class"""
    def __init__(self, given_url: str, hash_filenames: bool = True):
        self.folder_info: tuple[list, int, str] = ([], 0, "")
        self.given_url: str = given_url
        self.save_path: str = read_config('DEFAULT', 'SavePath')
        self.hash_filenames: bool = hash_filenames
        self.site_name: str = self.site_check()
        flag = 0x08000000  # No-Window flag
        webdriver.common.service.subprocess.Popen = functools.partial(subprocess.Popen, creationflags=flag)

    def image_getter(self):
        """Download images from URL."""
        self.folder_info = self.html_parse()  # Gets image url, number of images, and name of album
        # Save location of this album
        full_path = "".join([self.save_path, self.folder_info[2]])
        # Checks if the dir path of this album exists
        Path(full_path).mkdir(parents=True, exist_ok=True)
        headers = {
            "User-Agent":
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36"
        }
        session = requests.Session()
        session.headers.update(headers)
        # Can get the image through numerically acending url for these sites
        #TODO: #18 Change these from download-by-url types to download-by-list types
        if self.site_name == "imhentai":
            # Gets the general url of all images in this album
            trimmed_url = trim_url(self.folder_info[0])
            # Downloads all images from the general url (eg. https://domain/gallery/##.jpg)
            for index in range(1, int(self.folder_info[1]) + 1):
                file_num = index
                try:
                    # Most images will be .jpg
                    self.download_from_url(session, trimmed_url, file_num, full_path, ".jpg")
                except PIL.UnidentifiedImageError:
                    try:
                        os.remove("".join([full_path, "/pic1.jpg"]))
                        # Check if .gif
                        self.download_from_url(session, trimmed_url, file_num, full_path, ".gif")
                    except PIL.UnidentifiedImageError:
                        try:
                            os.remove("".join([full_path, "/pic1.gif"]))
                            # Check if .png
                            self.download_from_url(session, trimmed_url, file_num, full_path, ".png")
                        except PIL.UnidentifiedImageError:
                            try:
                                os.remove("".join([full_path, "/pic1.png"]))
                                # If all fails, download thumbnail
                                self.download_from_url(session, trimmed_url, file_num + "t", full_path, ".jpg")
                            except PIL.UnidentifiedImageError:
                                # No image exists, probably
                                os.remove("".join([full_path, "/pic1.jpg"]))
                except OSError:
                    pass
        # Easier to put all image url in a list and then download for these sites
        else:
            for index in range(int(self.folder_info[1])):
                time.sleep(0.2)
                try:
                    self.download_from_list(session, self.folder_info[0][index], full_path, index)
                except PIL.UnidentifiedImageError:
                    pass  # No image exists, probably
                except requests.exceptions.ChunkedEncodingError:
                    time.sleep(10)
                    self.download_from_list(session, self.folder_info[0][index], full_path, index)
        print("Download Complete")

    # TODO: Look into merging the 2 download methods if possible
    def download_from_url(self, session: requests.Session, url_name: str, file_name: str, full_path: str, ext: str):
        """"Download image from image url"""
        num_files = self.folder_info[1]
        # Completes the specific image URL from the general URL
        rip_url = "".join([url_name, str(file_name), ext])
        num_progress = "".join(["(", file_name, "/", str(num_files), ")"])
        print("    ".join([rip_url, num_progress]))
        image_url = "".join([full_path, "/", str(file_name), ext])
        self.download_file(session, image_url, rip_url, ext)
        if self.hash_filenames:
            self.rename_file_to_hash(image_url, full_path, ext)
        time.sleep(0.05)

    def download_from_list(self, session: requests.Session, given_url: str, full_path: str, current_file_num: int):
        """Download images from url supplied from a list of image urls"""
        num_files = self.folder_info[1]
        rip_url = given_url.strip('\n')
        num_progress = "".join(["(", str(current_file_num + 1), "/", str(num_files), ")"])
        print("    ".join([rip_url, num_progress]))
        file_name = os.path.basename(urlparse(rip_url).path)
        image_path = "".join([full_path, '/', file_name])
        ext = image_path.split(".")[-1]
        self.download_file(session, image_path, rip_url, ext)
        if self.hash_filenames:
            self.rename_file_to_hash(image_path, full_path, ext)
        time.sleep(0.05)

    def download_file(self, session: requests.Session, image_path: str, rip_url: str, ext: str):
        for _ in range(4):
            with open(image_path, "wb") as handle:
                bad_cert = False
                try:
                    response = requests.get(rip_url, headers=requests_header, stream=True)
                except requests.exceptions.SSLError:
                    response = session.get(rip_url, headers=requests_header, stream=True, verify=False)
                    bad_cert = True
                if not response.ok and not bad_cert:
                    print(response)
                try:
                    #handle.write(response.content)
                    #if ext in (".jpg", ".jpeg", ".png", ".webp"):
                    for block in response.iter_content(chunk_size=50000):
                        if not block:
                            break
                        handle.write(block)
                    #elif ext == ".gif":
                        #handle.write(response.content)
                    break
                except ConnectionResetError:
                    print("Conection Reset, Retrying...")
                    time.sleep(1)
                    continue
                
    def rename_file_to_hash(self, image_name: str, full_path: str, ext: str):
        if self.hash_filenames:
            # md5 hash is used as image name to avoid duplicate names
            md5hash = hashlib.md5(Image.open(image_name).tobytes())
            hash5 = md5hash.hexdigest()
            image_hash_name = "".join([full_path, "/", hash5, ext])
            # If duplicate exists, remove the duplicate
            if os.path.exists(image_hash_name):
                os.remove(image_name)
            else:
                # Otherwise, rename the image with the md5 hash
                os.rename(image_name, image_hash_name)

    def html_parse(self) -> list:
        """Return image URL, number of images, and folder name."""
        options = Options()
        options.headless = True
        options.add_argument = DRIVER_HEADER
        driver = webdriver.Firefox(options=options)
        driver.get(self.given_url)
        parser_switch = {
            "imhentai": imhentai_parse,
            "hotgirl": hotgirl_parse,
            "cup-e": cupe_parse,
            "girlsreleased": girlsreleased_parse,
            "bustybloom": bustybloom_parse,
            "morazzia": morazzia_parse,
            "novojoy": novojoy_parse,
            "hqbabes": hqbabes_parse,
            "silkengirl": silkengirl_parse,
            "babesandgirls": babesandgirls_parse,
            "babeimpact": babeimpact_parse,
            "100bucksbabes": hundredbucksbabes_parse,
            "sexykittenporn": sexykittenporn_parse,
            "babesbang": babesbang_parse,
            "exgirlfriendmarket": exgirlfriendmarket_parse,
            "novoporn": novoporn_parse,
            "hottystop": hottystop_parse,
            "babeuniversum": babeuniversum_parse,
            "babesandbitches": babesandbitches_parse,
            "chickteases": chickteases_parse,
            "wantedbabes": wantedbabes_parse,
            "cyberdrop": cyberdrop_parse,
            "sexy-egirls": sexyegirls_parse,
            "pleasuregirl": pleasuregirl_parse,
            "sexyaporno": sexyaporno_parse,
            "theomegaproject": theomegaproject_parse,
            "babesmachine": babesmachine_parse,
            "babesinporn": babesinporn_parse,
            "livejasminbabes": livejasminbabes_parse,
            "grabpussy": grabpussy_parse,
            "simply-cosplay": simplycosplay_parse,
            "simply-porn": simplyporn_parse,
            "pmatehunter": pmatehunter_parse,
            "elitebabes": elitebabes_parse,
            "xarthunter": xarthunter_parse,
            "joymiihub": joymiihub_parse,
            "metarthunter": metarthunter_parse,
            "femjoyhunter": femjoyhunter_parse,
            "ftvhunter": ftvhunter_parse,
            "hegrehunter": hegrehunter_parse,
            "hanime": hanime_parse,
            "babesaround": babesaround_parse,
            "8boobs": eightboobs_parse,
            "decorativemodels": decorativemodels_parse,
            "girlsofdesire": girlsofdesire_parse,
            "tuyangyan": tuyangyan_parse,
            "hqsluts": hqsluts_parse,
            "foxhq": foxhq_parse,
            "rabbitsfun": rabbitsfun_parse,
            "erosberry": erosberry_parse,
            "novohot": novohot_parse,
            "eahentai": eahentai_parse,
            "nightdreambabe": nightdreambabe_parse,
            "xmissy": xmissy_parse,
            "glam0ur": glam0ur_parse,
            "dirtyyoungbitches": dirtyyoungbitches_parse,
            "rossoporn": rossoporn_parse,
            "nakedgirls": nakedgirls_parse,
            "mainbabes": mainbabes_parse,
            "hotstunners": hotstunners_parse,
            "sexynakeds": sexynakeds_parse,
            "nudity911": nudity911_parse,
            "pbabes": pbabes_parse,
            "sexybabesart": sexybabesart_parse,
            "heymanhustle": heymanhustle_parse,
            "sexhd": sexhd_parse,
            "gyrls": gyrls_parse,
            "pinkfineart": pinkfineart_parse,
            "sensualgirls": sensualgirls_parse,
            "novoglam": novoglam_parse,
            "cherrynudes": cherrynudes_parse,
            "pics": pics_parse,
            "redpornblog": redpornblog_parse
        }
        site_parser = parser_switch.get(self.site_name)
        if self.site_name in ("hotgirl", "hottystop"):
            site_info = site_parser(driver, self.given_url)
        else:
            site_info = site_parser(driver)
        driver.quit()
        return site_info

    def site_check(self) -> str:
        """Check which site the url is from while also updating requests_header['referer'] to match the domain that hosts the files"""
        if url_check(self.given_url):
            domain = urlparse(self.given_url).netloc
            requests_header['referer'] = "".join(["https://", domain, "/"])
            domain = domain.split(".")[-2]
            if "https://members.hanime.tv/" in self.given_url or "https://hanime.tv/" in self.given_url:  # Hosts images on a different domain
                requests_header['referer'] = "https://cdn.discordapp.com/"
            return domain
        raise RipperError("Not a support site")

def babeimpact_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for babeimpact.com"""
    # Parses the html of the site
    soup = soupify(driver)
    title = soup.find("h1", class_="blockheader pink center lowercase").text
    sponsor = soup.find("div", class_="c").find_all("a")[1].text
    sponsor = "".join(["(", sponsor.strip(), ")"])
    dir_name = " ".join([sponsor, title])
    dir_name = clean_dir_name(dir_name)
    tags = soup.find_all("div", class_="list gallery")
    tag_list = []
    for tag in tags:
        tag_list.extend(tag.find_all("div", class_="item"))
    num_files = len(tag_list)
    image_list = ["".join(["https://babeimpact.com", tag.find("a").get("href")]) for tag in tag_list]
    images = []
    for image in image_list:
        driver.get(image)
        html = driver.page_source
        soup = BeautifulSoup(html, PARSER)
        images.append("".join([PROTOCOL, soup.find(
            "div", class_="image-wrapper").find("img").get("src")]))
    driver.quit()
    return (images, num_files, dir_name)

def babeuniversum_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for babeuniversum.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="title").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="three-column").find_all("div", class_="thumbnail")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def babesandbitches_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for babesandbitches.net"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", id="title").text.split()
    for i, word in enumerate(dir_name):
        if word == "picture":
            del dir_name[i:]
            break
    dir_name = clean_dir_name(" ".join(dir_name))
    images = soup.find_all("a", class_="gallery-thumb")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def babesandgirls_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for babesandgirls.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="block-post album-item").find_all("a", class_="item-post")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def babesaround_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for babesaround.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="ctitle2").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="inner_gallery_thumbs")
    images = [tag for im in images for tag in im.find_all("a", recursive=False)]
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def babesbang_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for babesbang.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="main-title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="gal-block")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def babesinporn_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for babesinporn.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="blockheader pink center lowercase").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="list gallery")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def babesmachine_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for babesmachine.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", id="gallery").find("h2").find("a").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", id="gallery").find("table").find_all("tr")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def bustybloom_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for bustybloom.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("img", title="Click To Enlarge!").get("alt").split()
    for i in range(len(dir_name)):
        if dir_name[i] == '-':
            del dir_name[i:]
            break
    dir_name = clean_dir_name(" ".join(dir_name))
    images = soup.find_all("div", class_="gallery_thumb")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def cherrynudes_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for cherrynudes.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("title").text.split("-")[0].strip()
    dir_name = clean_dir_name(dir_name)
    images = soup.find("ul", class_="photos").find_all("a")
    content_url = driver.current_url.replace("www", "cdn")
    images = ["".join([content_url, img.get("href")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def chickteases_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for chickteases.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", id="galleryModelName").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="minithumbs")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def cupe_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for hentai.cafe"""
    # Parses the html of the site
    soup = soupify(driver)
    image_list = soup.find_all("img", ["alignnone", "size-full"])
    del image_list[0]
    images = [image.get("src") for image in image_list]
    if len(images) == 0:
        image_list = soup.find_all("a", class_="ngg-fancybox")
        images = [image.get("data-src") for image in image_list]
    if len(images) == 0:
        soup.find_all("img", class_="attachment-full size-full wp-post-image")
        images = [image.get("src") for image in image_list]
    images = [x for x in images if x is not None]
    num_files = len(images)
    album_title = soup.find("h1", class_="entry-title").text
    album_info = soup.find("div", class_="entry-content").find("p").text
    album_info = album_info.split()
    shoot_theme = []
    model_index = 0
    theme_found = False
    if "Concept" in album_info:
        for index in range(len(album_info)):
            if theme_found:
                if not album_info[index].replace(":", "") == "Model":
                    shoot_theme.append(album_info[index])
                else:
                    model_index = index + 2
                    shoot_theme = " ".join(
                        shoot_theme).replace(":", "").strip()
                    break
            elif album_info[index] == "Concept":
                theme_found = True
    else:
        for index in range(len(album_info)):
            if not album_info[index] == "Model":
                shoot_theme.append(album_info[index])
            else:
                model_index = index + 2
                shoot_theme = " ".join(shoot_theme).replace(":", "").strip()
                break
    model_name = []
    for index in range(model_index, len(album_info)):
        if album_info[index] in ("Photographer", "Photo") or index == len(album_info) - 1:
            model_name = " ".join(model_name)
            model_name = "".join(["[", model_name, "]"])
            break
        else:
            model_name.append(album_info[index])
    if len(model_name) > 50:
        model_name = "".join([model_name[:51], "]"])
    dir_name = " ".join(["(Cup E)", album_title, "-", shoot_theme, model_name])
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_files, dir_name)

def cyberdrop_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for cyberdrop.me"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", id="title").text
    dir_name = clean_dir_name(dir_name)
    image_list = soup.find_all("div", class_="image-container column")
    images = [image.find("a", class_="image").get("href")
              for image in image_list]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def decorativemodels_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for decorativemodels.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="center").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="list gallery").find_all("div", class_="item")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def dirtyyoungbitches_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for dirtyyoungbitches.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="title-holder").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="container cont-light").find("div", class_="images").find_all("a", class_="thumb")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def eahentai_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for eahentai.com"""
    #Parses the html of the site
    time.sleep(1)
    #Scroll the page before parsing html to allow img tags to load (because images are set to be lazy loaded)
    SCROLL_PAUSE_TIME = 0.5
    last_height = driver.execute_script("return document.body.scrollHeight")
    while True:
        driver.execute_script("window.scrollTo(0, document.body.scrollHeight);")
        time.sleep(SCROLL_PAUSE_TIME)
        new_height = driver.execute_script("return document.body.scrollHeight")
        if new_height == last_height:
            break
        last_height = new_height
    driver.implicitly_wait(10)
    soup = soupify(driver)
    dir_name = soup.find("h2").text
    dir_name = clean_dir_name(dir_name)
    num_files = int(soup.find("h1", class_="type-pages").find("div").text)
    images = soup.find("div", class_="gallery").find_all("a")
    images = [img.find("img").get("src").replace("/thumbnail", "").replace("t.", ".") for img in images]
    driver.quit()
    return (images, num_files, dir_name)

def eightboobs_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for 8boobs.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="gallery clear").find_all("a", recursive=False)
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def elitebabes_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for elitebabes.com"""
    # Parses the html of the site
    soup = soupify(driver)
    image_list = soup.find("ul", class_="list-justified2").find_all("a")
    images = [image.get("href") for image in image_list]
    num_files = len(images)
    dir_name = image_list[0].find("img").get("alt")
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_files, dir_name)

def erosberry_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for erosberry.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="block-post three-post flex").find_all("a", recursive=False)
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def exgirlfriendmarket_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for exgirlfriendmarket.com"""
    # Parses the html of the site
    soup = soupify(driver)
    num_files = len(soup.find_all("div", class_="gallery_thumb"))
    dir_name = soup.find("div", class_="title-area").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="gallery").find_all("a", class_="thumb exo")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def femjoyhunter_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for femjoyhunter.com"""
    # Parses the html of the site
    soup = soupify(driver)
    image_list = soup.find("ul", class_="list-justified2").find_all("a")
    images = [image.get("href") for image in image_list]
    num_files = len(images)
    dir_name = image_list[0].find("img").get("alt")
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_files, dir_name)

def foxhq_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for foxhq.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1").text
    if dir_name == None:
        dir_name = soup.find("h2").text
    dir_name = clean_dir_name(dir_name)
    url = driver.current_url
    images = ["".join([url, td.find("a").get("href")]) for td in soup.find_all("td", align="center")[:-2]]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def ftvhunter_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for ftvhunter.com"""
    # Parses the html of the site
    soup = soupify(driver)
    image_list = soup.find("ul", class_="list-justified2").find_all("a")
    images = [image.get("href") for image in image_list]
    num_files = len(images)
    dir_name = image_list[0].find("img").get("alt")
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_files, dir_name)

def girlsofdesire_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for girlsofdesire.org"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("a", class_="albumName").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", id="gal_10").find_all("td", class_="vtop")
    images = ["".join(["https://girlsofdesire.org", img.find("img").get("src").replace("_thumb", "")]) for img in images]
    num_files = len(images)
    return (images, num_files, dir_name)

def girlsreleased_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for girlsreleased.com"""
    # Parses the html of the site
    soup = soupify(driver)
    set_name = soup.find("a", id="set_name").text
    model_name = soup.find_all("a", class_="button link")[1]
    model_name = model_name.find("span", recursive=False).text
    model_name = "".join(["[", model_name, "]"])
    dir_name = " ".join([set_name, model_name])
    dir_name = clean_dir_name(dir_name)
    images_source = soup.find_all("a", target="imageView")
    images_url = [image.get("href") for image in images_source]
    images = []
    headers = {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.141 Safari/537.36'}
    session = requests.Session()
    for url in images_url:
        response = session.get(url, stream=True, headers=headers)
        html = response.text
        soup = BeautifulSoup(html, PARSER)
        try:
            image = soup.find(
                "img", class_="pic img img-responsive").get("src")
            images.append(image)
        except AttributeError:
            pass  # Image may have been deleted from ImageTwist servers
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def glam0ur_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for glam0ur.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="picnav").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="center").find_all("a", recursive=False)
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def grabpussy_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for grabpussy.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find_all("div", class_="c-title")[1].find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="gal own-gallery-images").find_all("a", recursive=False)
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def gyrls_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for gyrls.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="single_title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", id="gallery-1").find_all("a")
    images = [img.get("href") for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def hanime_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for hanime.tv"""
    # Parses the html of the site
    time.sleep(1)  # Wait so images can load
    soup = soupify(driver)
    dir_name = "Hanime Images"
    image_list = soup.find(
        "div", class_="cuc_container images__content flex row wrap justify-center relative").find_all("a", recursive=False)
    images = [image.get("href") for image in image_list]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def hegrehunter_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for hegrehunter.com"""
    # Parses the html of the site
    soup = soupify(driver)
    image_list = soup.find("ul", class_="list-justified2").find_all("a")
    images = [image.get("href") for image in image_list]
    num_files = len(images)
    dir_name = image_list[0].find("img").get("alt")
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_files, dir_name)

#Cannot bypass captcha, so it doesn't work
def __hentaicosplays_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for hentai-cosplays.com"""
    #Parses the html of the site
    soup = soupify(driver)
    print_html(soup)
    dir_name = soup.find("div", id="main_contents").find("h2").text
    dir_name = clean_dir_name(dir_name)
    images = []
    while True:
        image_list = soup.find("div", id="display_image_detail").find_all("div", class_="icon-overlay")
        image_list = [img.find("img").get("src") for img in images]
        images.extend(image_list)
        next_page = soup.find("div", id="paginator").find_all("span")[-2].find("a")
        if next_page == None:
            break
        else:
            next_page = "".join(["https://hentai-cosplays.com", next_page.get("href")])
            driver.get(next_page)
            soup = soupify(driver)
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def heymanhustle_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for heymanhustle.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="entry-title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="galleria-thumbnails").find_all("img")
    images = [img.get("src").replace("/cache", "").split("-nggid")[0] for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def hotgirl_parse(driver: webdriver.Firefox, url: str) -> tuple:
    """Read the html for hotgirl.asia"""
    # Parses the html of the site
    soup = soupify(driver)
    # Gets the number of pages
    num_pages = soup.findAll("a", class_="page larger")
    if len(num_pages) > 0:
        num_pages = num_pages.pop().text
    else:
        num_pages = 1
    # Gets the folder name
    dir_name = soup.find("h3").text
    # Removes illegal characters from folder name
    dir_name = clean_dir_name(dir_name)
    images_html = soup.find_all('img', itemprop="image")
    del images_html[0]
    if int(num_pages) > 1:
        for index in range(2, int(num_pages) + 1):
            page_url = "".join([url, str(index), '/'])
            driver.get(page_url)
            soup = BeautifulSoup(driver.page_source, PARSER)
            images_list = soup.find_all("img", itemprop="image")
            del images_list[0]
            images_html.extend(images_list)
    images = [image.get("src") for image in images_html]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def hotstunners_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for hotstunners.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="title_content").find("h2").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="gallery_janna2").find_all("img")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def hottystop_parse(driver: webdriver.Firefox, url: str) -> tuple:
    """Read the html for hottystop.com"""
    # Parses the html of the site
    soup = soupify(driver)
    try:
        dir_name = soup.find("div", class_="Box_Large_Content").find("h1").text
    except AttributeError:
        dir_name = soup.find("div", class_="Box_Large_Content").find("u").text
    dir_name = clean_dir_name(dir_name)
    image_list = soup.find("table").find_all("a")
    images = ["".join([url, image.get("href")]) for image in image_list]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def hqbabes_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for hqbabes.com"""
    # Parses the html of the site
    soup = soupify(driver)
    model = soup.find("p", class_="desc").find("a").text
    model = "".join(["[", model, "]"])
    try:
        shoot = soup.find("p", class_="desc").find("span").text
    except AttributeError:
        shoot = "-"
    producer = soup.find("p", class_="details").find_all("a")[1].text
    producer = "".join(["(", producer, ")"])
    dir_name = " ".join([producer, shoot, model])
    dir_name = clean_dir_name(dir_name)
    ext = [".png", ".jpg", ".jpeg"]
    images = []
    image_list = soup.find_all("li", class_="item i p")
    for image in image_list:
        image_url = image.find("a").get("href")
        if any(x in image_url for x in ext):
            images.append("".join([PROTOCOL, image_url]))
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def hqsluts_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for hqsluts.com"""
    # Parses the html of the site
    soup = soupify(driver)
    model = soup.find("p", class_="desc").find("a").text
    model = "".join(["[", model, "]"])
    try:
        shoot = soup.find("p", class_="desc").find("span").text
    except AttributeError:
        shoot = "-"
    producer = soup.find("p", class_="details").find_all("b")[2].find("a").text
    producer = "".join(["(", producer, ")"])
    dir_name = " ".join([producer, shoot, model])
    dir_name = clean_dir_name(dir_name)
    images = [image.find("a").get("href") for image in soup.find_all("li", class_="item i p")]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def hundredbucksbabes_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for 100bucksbabes.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="main-col-2").find("h2", class_="heading").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="main-thumbs").find_all("img")
    images = ["".join([PROTOCOL, img.get("data-url")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def imhentai_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for imhentai.com"""
    # Parses the html of the site
    soup = soupify(driver)
    # Gets the image URL to be turned into the general image URL
    images = soup.find("img", class_="lazy preloader").get("data-src")
    # Gets the number of pages (images) in the album
    num_pages = soup.find("li", class_="pages")
    num_pages = num_pages.string.split()[1]
    dir_name = soup.find("h1").string
    # Removes illegal characters from folder name
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_pages, dir_name)

def joymiihub_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for joymiihub.com"""
    # Parses the html of the site
    soup = soupify(driver)
    image_list = soup.find("ul", class_="list-justified2").find_all("a")
    images = [image.get("href") for image in image_list]
    num_files = len(images)
    dir_name = image_list[0].find("img").get("alt")
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_files, dir_name)

def livejasminbabes_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for livejasminbabes.net"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", id="gallery_header").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="gallery_thumb")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def mainbabes_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for mainbabes.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="heading").find("h2", class_="title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="thumbs_box").find_all("div", class_="thumb_box")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def metarthunter_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for hetarthunter.com"""
    # Parses the html of the site
    soup = soupify(driver)
    image_list = soup.find("ul", class_="list-justified2").find_all("a")
    images = [image.get("href") for image in image_list]
    num_files = len(images)
    dir_name = image_list[0].find("img").get("alt")
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_files, dir_name)

def morazzia_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for morazzia.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="block-post album-item").find_all("a")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def nakedgirls_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for nakedgirls.xxx"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="content").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="content").find_all("div", class_="thumb")
    images = ["".join(["https://www.nakedgirls.xxx", img.find("a").get("href")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def nightdreambabe_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for nightdreambabe.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", id="gallery_middle").find_all("h1", recursive=False)[1].text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="gwrapper")
    images = ["".join([PROTOCOL, img.find("img").get("src")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def novoglam_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for novoglam.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", id="heading").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("ul", id="myGalleryThumbs").find_all("img")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def novohot_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for novohot.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", id="viewIMG").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="runout").find_all("img")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def novojoy_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for novojoy.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("img", class_="gallery-image")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def novoporn_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for novoporn.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="gallery").find("h1").text.split()
    for i, word in enumerate(dir_name):
        if word == "porn":
            del dir_name[i:]
            break
    dir_name = clean_dir_name(" ".join(dir_name))
    images = soup.find_all("img", class_="gallerythumbs")
    images = [img.get("src").replace("tn_", "") for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def nudity911_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for nudity911.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("tr", valign="top").find("td", align="center").find("table", width="650").find_all("td", width="33%")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def pbabes_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for pbabes.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find_all("div", class_="box_654")[1].find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", style="margin-left:35px;").find_all("a", rel="nofollow")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def pics_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for pics.vc"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="gall_header").find("h2").text.split("-")[1].strip()
    dir_name = clean_dir_name(dir_name)
    images = []
    while True:
        image_list = soup.find("div", class_="grid").find_all("div", class_="photo_el grid-item transition_bs")
        image_list = [img.find("img").get("src").replace("/s/", "/o/") for img in image_list]
        images.extend(image_list)
        if soup.find("div", id="center_control").find("div", class_="next_page clip") == None:
            break
        else:
            next_page = "".join(["https://pics.vc", soup.find("div", id="center_control").find("a").get("href")])
            driver.get(next_page)
            soup = soupify(driver)
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def pinkfineart_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for pinkfineart.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h5", class_="d-none d-sm-block text-center my-2")
    dir_name = "".join([t for t in dir_name.contents if type(t) == bs4.element.NavigableString])
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="card ithumbnail-nobody ishadow ml-2 mb-3")
    images = ["".join(["https://pinkfineart.com", img.find("a").get("href")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def pleasuregirl_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for pleasuregirl.net"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h2", class_="title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="lightgallery-wrap").find_all("div", class_="grid-item thumb")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def pmatehunter_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for pmatehunter.com"""
    # Parses the html of the site
    soup = soupify(driver)
    image_list = soup.find("ul", class_="list-justified2").find_all("a")
    images = [image.get("href") for image in image_list]
    num_files = len(images)
    dir_name = image_list[0].find("img").get("alt")
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_files, dir_name)

def rabbitsfun_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for rabbitsfun.com"""
    #Parses the html of the site
    time.sleep(1)
    soup = soupify(driver)
    dir_name = soup.find("h3", class_="watch-mobTitle").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="gallery-watch").find_all("li")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_","")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def redpornblog_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for redpornblog.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", id="pic-title").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", id="bigpic-image").find_all("img")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def rossoporn_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for rossoporn.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="content_right").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="wrapper_g")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for tag_list in images for img in tag_list.find_all("img")]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def sensualgirls_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for sensualgirls.org"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("a", class_="albumName").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", id="box_289").find_all("div", class_="gbox")
    images = ["".join(["https://sensualgirls.org", img.find("img").get("src").replace("_thumb", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def sexhd_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for sexhd.pics"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="photobig").find("h4").text.split(":")[1].strip()
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="photobig").find_all("div", class_="relativetop")[1:]
    images = ["".join(["https://sexhd.pics", img.find("a").get("href")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def sexyaporno_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for sexyaporno.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("img", title="Click To Enlarge!").get("alt").split()
    for i in range(len(dir_name)):
        if dir_name[i] == '-':
            del dir_name[i:]
            break
    dir_name = " ".join(dir_name)
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="gallery_thumb")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def sexybabesart_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for sexybabesart.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="content-title").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="thumbs").find_all("img")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def sexyegirls_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for sexy-egirls.com"""
    # Parses the html of the site
    time.sleep(1)  # Wait so images can load
    soup = soupify(driver)
    dir_name = soup.find(
        "div", class_="album-info-title").find("h1").text.split()
    split = 0
    for i, word in enumerate(dir_name):
        if "Pictures" in word or "Video" in word:
            split = i
            break
    dir_name = dir_name[:split-1]
    dir_name = clean_dir_name(" ".join(dir_name))
    image_list = soup.find_all("div", class_="album-item")
    images = [image.find("a").get("href") for image in image_list]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def sexykittenporn_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for sexykittenporn.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="blockheader").text
    dir_name = clean_dir_name(dir_name)
    tag_list = soup.find_all("div", class_="list gallery col3")
    image_list = []
    for tag in tag_list:
        image_list.extend(tag.find_all("div", class_="item"))
    num_files = len(image_list)
    image_link = ["".join(["https://www.sexykittenporn.com",
                          image.find("a").get("href")]) for image in image_list]
    images = []
    for link in image_link:
        driver.get(link)
        html = driver.page_source
        soup = BeautifulSoup(html, PARSER)
        images.append("".join([PROTOCOL, soup.find(
            "div", class_="image-wrapper").find("img").get("src")]))
    driver.quit()
    return (images, num_files, dir_name)

def sexynakeds_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for sexynakeds.com"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", class_="box").find_all("h1")[1].text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="post_tn").find_all("img")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def silkengirl_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for silkengirl.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="title").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="thumb_box")
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def simplycosplay_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for simply-cosplay.com"""
    # Parses the html of the site
    time.sleep(5)  # Wait so images can load
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="content-headline").text
    dir_name = clean_dir_name(dir_name)
    image_list = soup.find("div", class_="swiper-wrapper")
    if image_list == None:
        images = [
            soup.find("div", class_="image-wrapper").find("img").get("data-src")]
        num_files = 1
    else:
        image_list = image_list.find_all("img")
        num_files = len(image_list)
        images = []
        for image in image_list:
            image = image.get("data-src").split("_")
            image.pop(1)
            image[0] = image[0][:-5]
            images.append("".join(image))
    driver.quit()
    return (images, num_files, dir_name)

def simplyporn_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for simply-porn.com"""
    # Parses the html of the site
    # time.sleep(5) #Wait so images can load
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="mt-3 mb-3").text
    dir_name = clean_dir_name(dir_name)
    image_list = soup.find(
        "div", class_="row full-gutters").find_all("div", class_="col-6 col-lg-3")
    if len(image_list) == 0:
        images = [
            soup.find("img", class_="img-fluid ls-is-cached lazyloaded").get("src")]
        num_files = 1
    else:
        images = []
        for image in image_list:
            image = image.find("img").get("data-src").split("_")
            image[0] = image[0][:-5]
            images.append("".join(image))
        num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def theomegaproject_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for theomegaproject.org"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="omega").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="postholder").find_all("div", class_="picture", recursive=False)
    images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def tuyangyan_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for tuyangyan.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", class_="post-title entry-title").find("a").text
    dir_name = dir_name.split("[")
    num_files = dir_name[1].replace("P]", "")
    dir_name = dir_name[0]
    dir_name = clean_dir_name("".join(dir_name))
    if int(num_files) > 20:
        pages = ceil(int(num_files) / 20)
        images = []
        page_url = driver.current_url
        for i in range(pages):
            if i > 0:
                url = "".join([page_url, str(i+1), "/"])
                driver.get(url)
                soup = soupify(driver)
            image_list = soup.find(
                "div", class_="entry-content clearfix").find_all("img")
            images.extend(image_list)
    else:
        images = soup.find(
            "div", class_="entry-content clearfix").find_all("img")
    images = ["".join([PROTOCOL, img.get("src")]) for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def wantedbabes_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for wantedbabes.com"""
    # Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("div", id="main-content").find("h1").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find_all("div", class_="gallery")
    images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def xarthunter_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for xarthunter.com"""
    # Parses the html of the site
    soup = soupify(driver)
    image_list = soup.find("ul", class_="list-justified2").find_all("a")
    images = [image.get("href") for image in image_list]
    num_files = len(images)
    dir_name = image_list[0].find("img").get("alt")
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return (images, num_files, dir_name)

def xmissy_parse(driver: webdriver.Firefox) -> tuple:
    """Read the html for xmissy.nl"""
    #Parses the html of the site
    soup = soupify(driver)
    dir_name = soup.find("h1", id="pagetitle").text
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", id="gallery").find_all("div", class_="noclick-image")
    images = [img.find("img").get("data-src") if img.find("img").get("data-src") != None else img.find("img").get("src") for img in images]
    num_files = len(images)
    driver.quit()
    return (images, num_files, dir_name)

def test_parse(given_url: str) -> list:
    """Return image URL, number of images, and folder name."""
    driver = None
    try:
        options = Options()
        options.headless = True
        options.add_argument = DRIVER_HEADER
        driver = webdriver.Firefox(options=options)
        driver.get(given_url)
        return bustybloom_parse(driver)
    finally:
        driver.quit()

def print_html(soup: BeautifulSoup):
    with open("html.html", "w+") as f:
        f.write(str(soup))

def clean_dir_name(given_name: str) -> str:
    """Remove forbidden characters from name"""
    translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*'), None)
    return given_name.translate(translation_table).strip()

def soupify(driver: webdriver.Firefox) -> BeautifulSoup:
    """Return BeautifulSoup object of html from driver"""
    html = driver.page_source
    return BeautifulSoup(html, PARSER)

def trim_url(given_url: str) -> str:
    """Return the URL without the filename attached."""
    file_ext = (".jpg", ".png", ".jpeg", ".gif")
    if any(x in given_url for x in file_ext):
        given_url = "".join([str("/".join(given_url.split("/")[0:-1])), "/"])
    return given_url

def read_config(header: str, child: str) -> str:
    """Read from config.ini"""
    config = configparser.ConfigParser()
    config.read(CONFIG)
    if not path.isfile(CONFIG):
        config['DEFAULT'] = {}
        config['DEFAULT']['SavePath'] = 'Rips/'
        config['DEFAULT']['Theme'] = 'Dark'
        config['DEFAULT']['AskToReRip'] = 'True'
        config['DEFAULT']['LiveHistoryUpdate'] = 'False'
        config['DEFAULT']['HashFilenames'] = 'True'
        config['DEFAULT']['NumberOfThreads'] = 1
        with open(CONFIG, 'w') as configfile:    # save
            config.write(configfile)
    return config.get(header, child)

def write_config(header: str, child: str, change: str):
    """Write to config.ini"""
    config = configparser.ConfigParser()
    config.read(CONFIG)
    config[header][child] = change
    with open(CONFIG, 'w') as configfile:    # save
        config.write(configfile)

def url_check(given_url: str) -> bool:
    """Check the url to make sure it is from valid site"""
    sites = ("https://imhentai.xxx/", "https://hotgirl.asia/", "https://www.redpornblog.com/"
             "https://www.cup-e.club/", "https://girlsreleased.com/", "https://www.bustybloom.com/",
             "https://www.morazzia.com/", "https://www.novojoy.com/", "https://www.hqbabes.com/",
             "https://www.silkengirl.com/", "https://www.babesandgirls.com/", "https://www.babeimpact.com/",
             "https://www.100bucksbabes.com/", "https://www.sexykittenporn.com/", "https://www.babesbang.com/",
             "https://www.exgirlfriendmarket.com/", "https://www.novoporn.com/", "https://www.hottystop.com/",
             "https://www.babeuniversum.com/", "https://www.babesandbitches.net/", "https://www.chickteases.com/",
             "https://www.wantedbabes.com/", "https://cyberdrop.me/", "https://www.sexy-egirls.com/",
             "https://www.pleasuregirl.net/", "https://www.sexyaporno.com/", "https://www.theomegaproject.org/",
             "https://www.babesmachine.com/", "https://www.babesinporn.com/", "https://www.livejasminbabes.net/",
             "https://www.grabpussy.com/", "https://www.simply-cosplay.com/", "https://www.simply-porn.com/",
             "https://pmatehunter.com/", "https://www.elitebabes.com/", "https://www.xarthunter.com/",
             "https://www.joymiihub.com/", "https://www.metarthunter.com/", "https://www.femjoyhunter.com/",
             "https://www.ftvhunter.com/", "https://www.hegrehunter.com/", "https://hanime.tv/",
             "https://members.hanime.tv/", "https://www.babesaround.com/", "https://www.8boobs.com/",
             "https://www.decorativemodels.com/", "https://www.girlsofdesire.org/", "https://www.tuyangyan.com/",
             "http://www.hqsluts.com/", "https://www.foxhq.com/", "https://www.rabbitsfun.com/", 
             "https://www.erosberry.com/", "https://www.novohot.com/", "https://eahentai.com/",
             "https://www.nightdreambabe.com/","https://xmissy.nl/", "https://www.glam0ur.com/",
             "https://www.dirtyyoungbitches.com/", "https://www.rossoporn.com/", "https://www.nakedgirls.xxx/",
             "https://www.mainbabes.com/", "https://www.hotstunners.com/", "https://www.sexynakeds.com/",
             "https://www.nudity911.com/", "https://www.pbabes.com/", "https://www.sexybabesart.com/",
             "https://www.heymanhustle.com/", "https://sexhd.pics/", "http://www.gyrls.com/",
             "https://www.pinkfineart.com/", "https://www.sensualgirls.org/", "https://www.novoglam.com/",
             "https://www.cherrynudes.com/", "http://pics.vc/")
    return any(x in given_url for x in sites)

if __name__ == "__main__":
    if len(sys.argv) > 1:
        album_url = sys.argv[1]
    else:
        raise RipperError("Script requires a link as an argument")
    print(test_parse(sys.argv[1]))
