from __future__ import annotations

import argparse
import json
import os
import pickle
import random
import re
import signal
import subprocess
import time
from math import ceil
from os import path
from subprocess import Popen
from time import sleep
from typing import Callable
from urllib.parse import urlparse, unquote

import bs4
import cloudscraper
import requests
import selenium
import tldextract
import urllib3.exceptions
from bs4 import BeautifulSoup
from pathlib import Path
from pybooru import Danbooru
from selenium import webdriver
from selenium.webdriver import Keys
from selenium.webdriver.common.action_chains import ActionChains
from selenium.webdriver.common.by import By
from selenium.webdriver.firefox.options import Options
from selenium.webdriver.remote.webelement import WebElement

from Config import Config
from Enums import FilenameScheme
from ImageLink import ImageLink
from RipInfo import RipInfo
from RipperExceptions import InvalidSubdomain, RipperError
from Util import SCHEME, url_check

PROTOCOL: str = "https:"
PARSER: str = "lxml"  # The XML parsing engine to be used by BeautifulSoup
DRIVER_HEADER: str = (
    "user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0")
# Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html)
# Chrome/W.X.Y.Z‡ Safari/537.36")
EXTERNAL_SITES: tuple = ("drive.google.com", "mega.nz", "mediafire.com", "sendvid.com")

requests_header: dict[str, str] = {
    'User-Agent':
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 '
        'Safari/537.36',
    'referer':
        'https://imhentai.xxx/',
    'cookie':
        ''
}
logged_in: bool


class HtmlParser:
    def __init__(self, header: dict[str, str], site_name: str = "",
                 filename_scheme: FilenameScheme = FilenameScheme.ORIGINAL):
        global logged_in
        options = self.__initialize_options(site_name)
        self.driver: webdriver.Firefox = webdriver.Firefox(options=options)
        self.driver_pid: Popen = self.driver.service.process.pid
        self.interrupted: bool = False
        self.site_name: str = site_name
        self.sleep_time: float = 0.2
        self.jitter: float = 0.5
        self.given_url: str = ""
        self.filename_scheme: FilenameScheme = filename_scheme
        self.requests_header: dict[str, str] = header
        self.parser_jump_table: dict[str, Callable[[], RipInfo]] = {
            "imhentai": self.imhentai_parse,
            "hotgirl": self.hotgirl_parse,
            "cup-e": self.cupe_parse,
            "girlsreleased": self.girlsreleased_parse,
            "bustybloom": self.bustybloom_parse,
            "morazzia": self.morazzia_parse,
            "novojoy": self.novojoy_parse,
            "hqbabes": self.hqbabes_parse,
            "silkengirl": self.silkengirl_parse,
            "babesandgirls": self.babesandgirls_parse,
            "babeimpact": self.babeimpact_parse,
            "100bucksbabes": self.hundredbucksbabes_parse,
            "sexykittenporn": self.sexykittenporn_parse,
            "babesbang": self.babesbang_parse,
            "exgirlfriendmarket": self.exgirlfriendmarket_parse,
            "novoporn": self.novoporn_parse,
            "hottystop": self.hottystop_parse,
            "babeuniversum": self.babeuniversum_parse,
            "babesandbitches": self.babesandbitches_parse,
            "chickteases": self.chickteases_parse,
            "wantedbabes": self.wantedbabes_parse,
            "cyberdrop": self.cyberdrop_parse,
            "pleasuregirl": self.pleasuregirl_parse,
            "sexyaporno": self.sexyaporno_parse,
            "theomegaproject": self.theomegaproject_parse,
            "babesmachine": self.babesmachine_parse,
            "babesinporn": self.babesinporn_parse,
            "livejasminbabes": self.livejasminbabes_parse,
            "grabpussy": self.grabpussy_parse,
            "simply-cosplay": self.simplycosplay_parse,
            "simply-porn": self.simplyporn_parse,
            "pmatehunter": self.pmatehunter_parse,
            "elitebabes": self.elitebabes_parse,
            "xarthunter": self.xarthunter_parse,
            "joymiihub": self.joymiihub_parse,
            "metarthunter": self.metarthunter_parse,
            "femjoyhunter": self.femjoyhunter_parse,
            "ftvhunter": self.ftvhunter_parse,
            "hegrehunter": self.hegrehunter_parse,
            "hanime": self.hanime_parse,
            "babesaround": self.babesaround_parse,
            "8boobs": self.eightboobs_parse,
            "decorativemodels": self.decorativemodels_parse,
            "girlsofdesire": self.girlsofdesire_parse,
            "tuyangyan": self.tuyangyan_parse,
            "hqsluts": self.hqsluts_parse,
            "foxhq": self.foxhq_parse,
            "rabbitsfun": self.rabbitsfun_parse,
            "erosberry": self.erosberry_parse,
            "novohot": self.novohot_parse,
            "eahentai": self.eahentai_parse,
            "nightdreambabe": self.nightdreambabe_parse,
            "xmissy": self.xmissy_parse,
            "glam0ur": self.glam0ur_parse,
            "dirtyyoungbitches": self.dirtyyoungbitches_parse,
            "rossoporn": self.rossoporn_parse,
            "nakedgirls": self.nakedgirls_parse,
            "mainbabes": self.mainbabes_parse,
            "hotstunners": self.hotstunners_parse,
            "sexynakeds": self.sexynakeds_parse,
            "nudity911": self.nudity911_parse,
            "pbabes": self.pbabes_parse,
            "sexybabesart": self.sexybabesart_parse,
            "heymanhustle": self.heymanhustle_parse,
            "sexhd": self.sexhd_parse,
            "gyrls": self.gyrls_parse,
            "pinkfineart": self.pinkfineart_parse,
            "sensualgirls": self.sensualgirls_parse,
            "novoglam": self.novoglam_parse,
            "cherrynudes": self.cherrynudes_parse,
            "redpornblog": self.redpornblog_parse,
            "join2babes": self.join2babes_parse,
            "babecentrum": self.babecentrum_parse,
            "cutegirlporn": self.cutegirlporn_parse,
            "everia": self.everia_parse,
            "imgbox": self.imgbox_parse,
            "nonsummerjack": self.nonsummerjack_parse,
            "myhentaigallery": self.myhentaigallery_parse,
            "buondua": self.buondua_parse,
            "f5girls": self.f5girls_parse,
            "hentairox": self.hentairox_parse,
            "gofile": self.gofile_parse,
            "redgifs": self.redgifs_parse,
            "kemono": self.kemono_parse,
            "sankakucomplex": self.sankakucomplex_parse,
            "luscious": self.luscious_parse,
            "sxchinesegirlz": self.sxchinesegirlz_parse,
            "agirlpic": self.agirlpic_parse,
            "v2ph": self.v2ph_parse,
            "nudebird": self.nudebird_parse,
            "bestprettygirl": self.bestprettygirl_parse,
            "coomer": self.coomer_parse,
            "imgur": self.imgur_parse,
            "8kcosplay": self.eightkcosplay_parse,
            "inven": self.inven_parse,
            "arca": self.arca_parse,
            "cool18": self.cool18_parse,
            "maturewoman": self.maturewoman_parse,
            "putmega": self.putmega_parse,
            "thotsbay": self.thotsbay_parse,
            "tikhoe": self.tikhoe_parse,
            "lovefap": self.lovefap_parse,
            "8muses": self.eightmuses_parse,
            "jkforum": self.jkforum_parse,
            "leakedbb": self.leakedbb_parse,
            "e-hentai": self.ehentai_parse,
            "jpg": self.jpg_parse,
            "artstation": self.artstation_parse,
            "porn3dx": self.porn3dx_parse,
            "deviantart": self.deviantart_parse,
            "readmanganato": self.manganato_parse,
            "manganato": self.manganato_parse,
            "sfmcompile": self.sfmcompile_parse,
            "tsumino": self.tsumino_parse,
            "danbooru": self.danbooru_parse,
            "flickr": self.flickr_parse,
            "rule34": self.rule34_parse,
            "titsintops": self.titsintops_parse,
            "gelbooru": self.gelbooru_parse,
            "999hentai": self.nine99hentai_parse,
            "newgrounds": self.newgrounds_parse,
            "fapello": self.fapello_parse,
            "nijie": self.nijie_parse,
            "faponic": self.faponic_parse,
            "erothots": self.erothots_parse,
            "bitchesgirls": self.bitchesgirls_parse,
            "thothub": self.thothub_parse,
            "influencersgonewild": self.influencersgonewild_parse,
            "erome": self.erome_parse,
            "ggoorr": self.ggoorr_parse,
            "google": self.google_parse,
            "dropbox": self.dropbox_parse
        }

    def __enter__(self) -> HtmlParser:
        options = self.__initialize_options()
        self.driver = webdriver.Firefox(options=options)
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.driver.quit()

    @property
    def current_url(self):
        return self.driver.current_url

    @current_url.setter
    def current_url(self, value):
        self.driver.get(value)

    @staticmethod
    def __initialize_options(site_name: str = "") -> Options:
        options = Options()
        if site_name != "v2ph" or logged_in:
            options.add_argument("-headless")
        options.add_argument(DRIVER_HEADER)
        options.set_preference("dom.disable_beforeunload", True)
        options.set_preference("browser.tabs.warnOnClose", False)
        return options

    def parse_site(self, url: str) -> RipInfo:
        if path.isfile("partial.json"):
            save_data: dict = self.read_partial_save()
            if url in save_data:
                requests_header["cookie"] = save_data["cookies"]
                requests_header["referer"] = save_data["referer"]
                self.interrupted = True
                return save_data[url]
        url = url.replace("members.", "www.")
        self.given_url = url
        self.current_url = url
        self.site_name = self.site_check()
        site_parser: Callable[[], RipInfo] = self.parser_jump_table.get(self.site_name)
        try:
            site_info: RipInfo = site_parser()
            self.write_partial_save(site_info, url)
            pickle.dump(self.driver.get_cookies(), open("cookies.pkl", "wb"))
            return site_info
        except Exception:
            print(self.current_url)
            raise
        finally:
            self.driver.quit()

    def site_check(self) -> str:
        """
            Check which site the url is from while also updating requests_header['referer'] to match the domain that
            hosts the files
        """
        if url_check(self.given_url):
            special_domains = ("inven.co.kr", "danbooru.donmai.us")
            domain = urlparse(self.given_url).netloc
            requests_header['referer'] = "".join([SCHEME, domain, "/"])
            domain_parts = domain.split(".")
            domain = domain_parts[-3] if any(special_domain in domain for special_domain in special_domains) else \
                domain_parts[-2]
            # Hosts images on a different domain
            if "https://members.hanime.tv/" in self.given_url or "https://hanime.tv/" in self.given_url:
                requests_header['referer'] = "https://cdn.discordapp.com/"
            elif "https://kemono.party/" in self.given_url:
                requests_header['referer'] = ""
            elif "https://e-hentai.org/" in self.given_url:
                self.sleep_time = 5
            return domain
        raise RipperError("Not a support site")

    @staticmethod
    def sequential_rename(old_name: str, new_name: str):
        try:
            os.rename(old_name, new_name)
        except FileExistsError:
            new_name = " (1).".join(new_name.rsplit(".", 1))
            print(f"{old_name} -> {new_name}", end='\r')
            try:
                os.rename(old_name, new_name)
            except FileExistsError:
                counter = 2
                while True:
                    new_name = re.sub(r"\(\d+\)", f"({counter})", new_name)
                    print(f"{old_name} -> {new_name}", end='\r')
                    try:
                        os.rename(old_name, new_name)
                        break
                    except FileExistsError:
                        counter += 1

    @staticmethod
    def write_partial_save(site_info: RipInfo, given_url: str):
        """Saves parsed site data to quickly retrieve in event of a failure"""
        # TODO
        data: dict[str, RipInfo | str] = {
            given_url: site_info.serialize(),
            "cookies": requests_header["cookie"],
            "referer": requests_header["referer"]
        }
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
            return {"": ""}  # Doesn't matter if the cached data doesn't exist, will regen instead

    def site_login(self) -> bool:
        if self.site_name == "newgrounds":
            return self.__newgrounds_login()
        if self.site_name == "titsintops":
            return self.__titsintops_login()
        if self.site_name == "nijie":
            return self.__nijie_login()
        if self.site_name == "porn3dx":
            return self.__porn3dx_login()

    def sleep(self, seconds: float):
        jitter = random.random() * self.jitter
        sleep(seconds + jitter)

    def __get_login_creds(self, site_name: str) -> tuple[str, str]:
        login = Config.config.logins[site_name]
        return login["Username"], login["Password"]

    def __porn3dx_login(self) -> bool:
        def try_send_key(xpath: str, key: str):
            fields = self.driver.find_elements(By.XPATH, xpath)
            for field in fields:
                try:
                    field.send_keys(key)
                    break
                except selenium.common.exceptions.ElementNotInteractableException:
                    pass

        curr_url = self.current_url
        username, password = self.__get_login_creds("Porn3dx")
        self.current_url = "https://porn3dx.com/login"
        logged_in_to_site = False
        if username and password:
            try_send_key('//form[@class="space-y-4 md:space-y-6"]//input[@type="text"]', username)
            try_send_key('//form[@class="space-y-4 md:space-y-6"]//input[@type="password"]', password)
            self.driver.find_elements(By.XPATH, '//button[@type="submit"]')[-1].click()
            while "/login" in self.driver.current_url:
                sleep(1)
            logged_in_to_site = True
            self.current_url = curr_url
        return logged_in_to_site

    def __newgrounds_login(self) -> bool:
        curr_url = self.current_url
        username, password = self.__get_login_creds("Newgrounds")
        self.current_url = "https://newgrounds.com/passport"
        self.driver.find_element(By.XPATH, '//input[@name="username"]').send_keys(username)
        self.driver.find_element(By.XPATH, '//input[@name="password"]').send_keys(password)
        self.driver.find_element(By.XPATH, '//button[@name="login"]').click()
        while self.current_url != "https://www.newgrounds.com/social":
            sleep(1)
        self.current_url = curr_url
        return True

    def __nijie_login(self) -> bool:
        orig_url = self.current_url
        username, password = self.__get_login_creds("Nijie")
        self.current_url = "https://nijie.info/login.php"
        if "age_ver.php" in self.current_url:
            self.driver.find_element(By.XPATH, '//li[@class="ok"]').click()
            while "login.php" not in self.current_url:
                sleep(0.1)
        self.driver.find_element(By.XPATH, '//input[@name="email"]').send_keys(username)
        self.driver.find_element(By.XPATH, '//input[@name="password"]').send_keys(password)
        self.driver.find_element(By.XPATH, '//input[@class="login_button"]').click()
        while "login.php" in self.current_url:
            sleep(0.1)
        self.current_url = orig_url
        return True

    def __titsintops_login(self) -> bool:
        download_url = self.current_url
        username, password = self.__get_login_creds("TitsInTops")
        self.current_url = "https://titsintops.com/phpBB2/index.php?login/login"
        # self.driver.find_element(By.XPATH, '//a[@class="p-navgroup-link p-navgroup-link--textual p-navgroup-link--logIn"]').click()
        login_input = self.try_find_element(By.XPATH, '//input[@name="login"]')
        while not login_input:
            sleep(0.1)
            login_input = self.try_find_element(By.XPATH, '//input[@name="login"]')
        login_input.send_keys(username)
        password_input = self.driver.find_element(By.XPATH, '//input[@name="password"]')
        password_input.send_keys(password)
        button = self.driver.find_element(By.XPATH,
                                          '//button[@class="button--primary button button--icon button--icon--login"]')
        button.click()
        while self.try_find_element(By.XPATH,
                                    '//button[@class="button--primary button button--icon button--icon--login"]'):
            sleep(0.1)
        self.current_url = download_url
        return True

    # region Parsers

    def __generic_html_parser_1(self):
        soup = self.soupify()
        image_list = soup.find("ul", class_="list-gallery a css").find_all("a")
        images = [image.get("href") for image in image_list]
        dir_name = image_list[0].find("img").get("alt")
        return RipInfo(images, dir_name, self.filename_scheme)

    def __generic_html_parser_2(self):
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("img", title="Click To Enlarge!").get("alt").split()
        for i in range(len(dir_name)):
            if dir_name[i] == '-':
                del dir_name[i:]
                break
        dir_name = " ".join(dir_name)
        images = soup.find_all("div", class_="gallery_thumb")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def __dot_party_parse(self, domain_url: str):
        cookies = self.driver.get_cookies()
        cookie_str = ''
        for c in cookies:
            cookie_str += "".join([c['name'], '=', c['value'], ';'])
        requests_header["cookie"] = cookie_str
        base_url = self.driver.current_url
        base_url = base_url.split("/")
        source_site = base_url[3]
        base_url = "/".join(base_url[:6]).split("?")[0]
        page_url = self.driver.current_url.split("?")[0]
        sleep(5)
        soup = self.soupify()
        dir_name = soup.find("h1", id="user-header__info-top").find("span", itemprop="name").text
        dir_name = f"{dir_name} - ({source_site})"

        # region Get All Posts

        page = 0
        image_links = []
        while True:
            page += 1
            print("".join(["Parsing page ", str(page)]))
            image_list = soup.find("div", class_="card-list__items").find_all("article")
            image_list = ["".join([base_url, "/post/", img.get("data-id")]) for img in image_list]
            image_links.extend(image_list)
            next_page = f"{page_url}?o={str(page * 50)}"
            print(next_page)
            soup = self.soupify(next_page)
            self.__print_html(soup)
            test_str = soup.find("h2", class_="site-section__subheading")
            if test_str is not None:
                break

        # endregion

        # region Parse All Posts

        images = []
        external_links: dict[str, list[str]] = self.__create_external_link_dict()
        num_posts = len(image_links)
        ATTACHMENTS = (".zip", ".rar", ".mp4", ".webm", ".psd", ".clip", ".m4v", ".7z", ".jpg", ".png", ".webp")
        for i, link in enumerate(image_links):
            print("".join(["Parsing post ", str(i + 1), " of ", str(num_posts)]))
            print(link)
            soup = self.soupify(link)
            links = soup.find_all("a")
            links = [link.get("href") for link in links]
            possible_links_p = soup.find_all("p")
            possible_links = [tag.text for tag in possible_links_p]
            possible_links_div = soup.find_all("div")
            possible_links.extend([tag.text for tag in possible_links_div])
            ext_links = self.__extract_external_urls(links)
            for site in ext_links:
                external_links[site].extend(ext_links[site])
            ext_links = self.__extract_possible_external_urls(possible_links)
            for site in ext_links:
                external_links[site].extend(ext_links[site])
            attachments = [domain_url + link if domain_url not in link and not any(protocol in link for protocol in
                                                                                   ("https", "http")) else link for link
                           in links if any(ext in link for ext in ATTACHMENTS)]
            images.extend(attachments)
            images.extend(external_links["drive.google.com"])
            image_list = soup.find("div", class_="post__files")
            if image_list is not None:
                image_list = image_list.find_all("a", class_="fileThumb image-link")
                image_list = [img.get("href") for img in image_list]
                images.extend(image_list)

        # endregion

        self.__save_external_links(external_links)
        if any("dropbox.com/" in url for url in images):
            old_links = images
            images: list[str | ImageLink] = []
            for link in old_links:
                if "dropbox.com/" not in link:
                    images.append(link)
                else:
                    rip_info = self.dropbox_parse(link)
                    if rip_info.dir_name:
                        images.extend(rip_info.urls)
        return RipInfo(images, dir_name, self.filename_scheme)

    def agirlpic_parse(self) -> RipInfo:
        """Parses the html for agirlpic.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        base_url = self.driver.current_url
        num_pages = len(soup.find("div", class_="page-links").find_all("a", recursive=False)) + 1
        images = []
        for i in range(1, num_pages + 1):
            tags = soup.find("div", class_="entry-content clear").find_all("div", class_="separator", recursive=False)
            for tag in tags:
                img_tags = tag.find_all("img")
                for img in img_tags:
                    if img:
                        images.append(img.get("src"))
            if i != num_pages:
                soup = self.soupify("".join([base_url, str(i + 1), "/"]))
        return RipInfo(images, dir_name, self.filename_scheme)

    def arca_parse(self) -> RipInfo:
        """Parses the html for arca.live and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title").text
        main_tag = soup.find("div", class_="fr-view article-content")
        images = main_tag.find_all("img")
        images = ["".join([img.get("src").split("?")[0], "?type=orig"]) for img in images]
        images = [PROTOCOL + img if PROTOCOL not in img else img for img in images]
        videos = main_tag.find_all("video")
        videos = [vid.get("src") for vid in videos]
        videos = [PROTOCOL + vid if PROTOCOL not in vid else vid for vid in videos]
        images.extend(videos)
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def artstation_parse(self) -> RipInfo:
        """
            Parses the html for artstation.com and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        soup = self.soupify()
        username = self.current_url.split("/")[3]
        dir_name = soup.find("h1", class_="artist-name").text
        cache_script = soup.find("div", class_="wrapper-main").find_all("script")[1].text

        # region Id Extraction

        start_ = cache_script.find("quick.json")
        end_ = cache_script.rfind(");")
        json_data = cache_script[start_ + 14:end_ - 1].replace("\n", "").replace(r'\"', '"')
        json_data = json.loads(json_data)
        user_id = json_data["id"]
        user_name = json_data["full_name"]

        # endregion

        # region Get Posts

        total = 1
        page_count = 1
        first_iter = True
        posts = []
        scraper = cloudscraper.create_scraper()
        while total > 0:
            print(page_count)
            url = f"https://www.artstation.com/users/{username}/projects.json?page={page_count}"
            print(url)
            response = scraper.get(url)
            response_data = response.json()
            data = response_data["data"]
            for d in data:
                posts.append(d["permalink"].split("/")[4])
            if first_iter:
                total = response_data["total_count"] - len(data)
                first_iter = False
            else:
                total -= len(data)
            page_count += 1
            sleep(0.1)

        # endregion

        # region Get Media Links

        images = []
        for post in posts:
            url = f"https://www.artstation.com/projects/{post}.json"
            try:
                response = scraper.get(url)
            except urllib3.exceptions.MaxRetryError:
                sleep(5)
                response = scraper.get(url)
            response_data = response.json()
            assets = response_data["assets"]
            urls = [asset["image_url"].replace("/large/", "/4k/") for asset in assets]
            images.extend(urls)

        # endregion

        return RipInfo(images, dir_name, self.filename_scheme)

    def babecentrum_parse(self) -> RipInfo:
        """Parses the html for babecentrum.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="pageHeading").find_all("cufontext")
        dir_name = [w.text for w in dir_name]
        dir_name = " ".join(dir_name).strip()
        images = soup.find("table").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def babeimpact_parse(self) -> RipInfo:
        """Parses the html for babeimpact.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        title = soup.find("h1", class_="blockheader pink center lowercase").text
        sponsor = soup.find("div", class_="c").find_all("a")[1].text
        sponsor = "".join(["(", sponsor.strip(), ")"])
        dir_name = " ".join([sponsor, title])
        tags = soup.find_all("div", class_="list gallery")
        tag_list = []
        for tag in tags:
            tag_list.extend(tag.find_all("div", class_="item"))
        image_list = ["".join(["https://babeimpact.com", tag.find("a").get("href")]) for tag in tag_list]
        images = []
        for image in image_list:
            soup = self.soupify(image)
            images.append("".join([PROTOCOL, soup.find(
                "div", class_="image-wrapper").find("img").get("src")]))
        return RipInfo(images, dir_name, self.filename_scheme)

    def babeuniversum_parse(self) -> RipInfo:
        """Parses the html for babeuniversum.com and extracts the relevant information necessary for downloading
        images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title").find("h1").text
        images = soup.find("div", class_="three-column").find_all("div", class_="thumbnail")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def babesandbitches_parse(self) -> RipInfo:
        """Parses the html for babesandbitches.net and extracts the relevant information necessary for downloading
        images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", id="title").text.split()
        for i, word in enumerate(dir_name):
            if word == "picture":
                del dir_name[i:]
                break
        dir_name = " ".join(dir_name)
        images = soup.find_all("a", class_="gallery-thumb")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def babesandgirls_parse(self) -> RipInfo:
        """Parses the html for babesandgirls.com and extracts the relevant information necessary for downloading
        images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="title").text
        images = soup.find("div", class_="block-post album-item").find_all("a", class_="item-post")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def babesaround_parse(self) -> RipInfo:
        """Parses the html for babesaround.com and extracts the relevant information necessary for downloading images
        from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("section", class_="outer-section").find(
            "h2").text  # soup.find("div", class_="ctitle2").find("h1").text
        images = soup.find_all("div", class_="lightgallery thumbs quadruple fivefold")
        images = [tag for img in images for tag in img.find_all("a", recursive=False)]
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def babesbang_parse(self) -> RipInfo:
        """Parses the html for babesbang.com and extracts the relevant information necessary for downloading images
        from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="main-title").text
        images = soup.find_all("div", class_="gal-block")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
        return RipInfo(images, dir_name, self.filename_scheme)

    def babesinporn_parse(self) -> RipInfo:
        """Parses the html for babesinporn.com and extracts the relevant information necessary for downloading images
        from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="blockheader pink center lowercase").text
        images = soup.find_all("div", class_="list gallery")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
        return RipInfo(images, dir_name, self.filename_scheme)

    def babesmachine_parse(self) -> RipInfo:
        """Parses the html for babesmachine.com and extracts the relevant information necessary for downloading
        images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="gallery").find("h2").find("a").text
        images = soup.find("div", id="gallery").find("table").find_all("tr")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def bestprettygirl_parse(self) -> RipInfo:
        """
            Parses the html for bestprettygirl.com and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find_all("img", class_="aligncenter size-full")
        images = [img.get("src") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def bitchesgirls_parse(self) -> RipInfo:
        """
            Parses the html for bitchesgirls.com and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="album-name").text
        images = []
        base_url = self.current_url
        if base_url[-1] != "/":
            base_url += "/"
        page = 1
        while True:
            if page != 1:
                soup = self.soupify(f"{base_url}{page}")
            posts = soup.find("div", class_="albumgrid").find_all("a", class_="post-container", recursive=False)
            links = [post.get("href") for post in posts]
            images.extend(links)
            load_btn = soup.find("a", id="loadMore")
            if load_btn:
                page += 1
            else:
                break
        return RipInfo(images, dir_name, self.filename_scheme)

    def buondua_parse(self) -> RipInfo:
        """Parses the html for buondua.com and extracts the relevant information necessary for downloading images
        from the site"""
        # Parses the html of the site
        self.lazy_load(True)
        soup = self.soupify()
        dir_name = soup.find("div", class_="article-header").find("h1").text
        dir_name = dir_name.split("(")
        if "pictures" in dir_name[-1] or "photos" in dir_name[-1]:
            dir_name = dir_name[:-1]
        dir_name = "(".join(dir_name)
        pages = len(soup.find("div", class_="pagination-list").find_all("span"))
        curr_url = self.driver.current_url.replace("?page=1", "")
        images = []
        for i in range(pages):
            image_list = soup.find("div", class_="article-fulltext").find_all("img")
            image_list = [img.get("src") for img in image_list]
            images.extend(image_list)
            if i < pages - 1:
                next_page = "".join([curr_url, "?page=", str(i + 2)])
                self.current_url = next_page
                self.lazy_load(True)
                soup = self.soupify()
        return RipInfo(images, dir_name, self.filename_scheme)

    def bustybloom_parse(self) -> RipInfo:
        """Parses the html for bustybloom.com and extracts the relevant information necessary for downloading images from the site"""
        return self.__generic_html_parser_2()

    def cherrynudes_parse(self) -> RipInfo:
        """Parses the html for cherrynudes.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("title").text.split("-")[0].strip()
        images = soup.find("ul", class_="photos").find_all("a")
        content_url = self.driver.current_url.replace("www", "cdn")
        images = ["".join([content_url, img.get("href")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def chickteases_parse(self) -> RipInfo:
        """Parses the html for chickteases.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", id="galleryModelName").text
        images = soup.find_all("div", class_="minithumbs")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def cool18_parse(self) -> RipInfo:
        """Parses the html for cool18.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("td", class_="show_content").find("b").text
        images = soup.find("td", class_="show_content").find("pre").find_all("img")
        images = [img.get("src") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def coomer_parse(self) -> RipInfo:
        """Parses the html for coomer.party and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        return self.__dot_party_parse("https://coomer.party")

    def cupe_parse(self) -> RipInfo:
        """Parses the html for cup-e.club and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        image_list = soup.find_all("img", {"class": ["alignnone", "size-full"]})
        images = [image.get("src") for image in image_list]
        if len(images) == 0:
            image_list = soup.find_all("a", class_="ngg-fancybox")
            images = [image.get("data-src") for image in image_list]
        if len(images) == 0:
            soup.find_all("img", class_="attachment-full size-full wp-post-image")
            images = [image.get("src") for image in image_list]
        images = [x for x in images if x is not None]
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
        return RipInfo(images, dir_name, self.filename_scheme)

    def cutegirlporn_parse(self) -> RipInfo:
        """Parses the html for cutegirlporn.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="gal-title").text
        images = soup.find("ul", class_="gal-thumbs").find_all("li")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("/t", "/")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def cyberdrop_parse(self) -> RipInfo:
        """Parses the html for cyberdrop.me and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", id="title").text
        image_list = soup.find_all("div", class_="image-container column")
        images = [image.find("a", class_="image").get("href")
                  for image in image_list]
        return RipInfo(images, dir_name, self.filename_scheme)

    def danbooru_parse(self) -> RipInfo:
        """Parses the html for danbooru.donmai.us and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        url = self.driver.current_url
        tags = url.split("tags=")[-1]
        tags = tags.split("&")[0]
        tags = tags.replace("+", " ")
        tags = unquote(tags)
        dir_name = "[Danbooru] " + tags
        images = []
        client = Danbooru('danbooru')
        i = 0
        while True:
            posts = client.post_list(tags=tags, page=i)
            i += 1
            if len(posts) == 0:
                break
            images.extend([post["file_url"] for post in posts if "file_url" in post])
            sleep(0.1)
        return RipInfo(images, dir_name, self.filename_scheme)

    def decorativemodels_parse(self) -> RipInfo:
        """Parses the html for decorativemodels.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="center").text
        images = soup.find("div", class_="list gallery").find_all("div", class_="item")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def deviantart_parse(self) -> RipInfo:
        """Parses the html for deviantart.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        curr = self.driver.current_url
        dir_name = curr.split("/")[3]
        images = [curr]
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def dirtyyoungbitches_parse(self) -> RipInfo:
        """
            Parses the html for dirtyyoungbitches.com and extracts the relevant information necessary for downloading
            images from the site
        """
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title-holder").find("h1").text
        images = soup.find("div", class_="container cont-light").find("div", class_="images").find_all("a",
                                                                                                       class_="thumb")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def dropbox_parse(self, dropbox_url: str = "") -> RipInfo:
        """
            Parses the html for dropbox.com and extracts the relevant information necessary for downloading images from
            the site
        """

        def get_dropbox_file(soup_: BeautifulSoup, post: str, filenames_: list[str], images_: list[str]):
            filename = post.split("/")[-1].split("?")[0]
            filenames_.append(filename)
            img = soup_.find("img", class_="_fullSizeImg_1anuf_16")
            try:
                if img:
                    images_.append(img.get("src"))
                else:
                    vid = soup_.find("video")
                    if vid:
                        images_.append(vid.find("source").get("src"))
                    else:
                        new_posts = soup_.find("ol", class_="_sl-grid-body_6yqpe_26").find_all("a")
                        new_posts = [post.get("href") for post in new_posts]
                        new_posts = self.__remove_duplicates(new_posts)
                        posts.extend(new_posts)
            except AttributeError:
                pass

        internal_use = False
        if dropbox_url:
            self.current_url = dropbox_url
            internal_use = True
        soup = self.soupify(xpath='//span[@class="dig-Breadcrumb-link-text"]')
        if not internal_use:
            try:
                dir_name = soup.find("span", class_="dig-Breadcrumb-link-text").text
            except AttributeError:
                deleted_notice = soup.find("h2", class_="dig-Title dig-Title--size-large dig-Title--color-standard")
                if deleted_notice:
                    return RipInfo([], "Deleted", self.filename_scheme)
                else:
                    print(self.current_url)
                    raise
        else:
            dir_name = ""

        images = []
        filenames = []
        posts = soup.find("ol", class_="_sl-grid-body_6yqpe_26")
        if posts:
            posts = posts.find_all("a")
            posts = [post.get("href") for post in posts]
            posts = self.__remove_duplicates(posts)
            for post in posts:
                soup = self.soupify(post, xpath='//img[@class="_fullSizeImg_1anuf_16"]')
                get_dropbox_file(soup, post, filenames, images)
        else:
            get_dropbox_file(soup, self.current_url, filenames, images)
        return RipInfo(images, dir_name, self.filename_scheme, filenames=filenames)

    def eahentai_parse(self) -> RipInfo:
        """Parses the html for eahentai.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        sleep(1)
        # Load lazy loaded images
        self.lazy_load()
        soup = self.soupify()
        dir_name = soup.find("h2").text
        images = soup.find("div", class_="gallery").find_all("a")
        images = [img.find("img").get("src").replace("/thumbnail", "").replace("t.", ".") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def ehentai_parse(self) -> RipInfo:
        """Parses the html for e-hentai.org and extracts the relevant information necessary for downloading images from the site"""
        sleep(5)  # To prevent ip ban; This is going to be a slow process
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", id="gn").text
        image_links = []
        page_count = 1
        while True:
            print("Parsing page " + str(page_count))
            image_tags = soup.find("div", id="gdt").find_all("a")
            image_tags = [link.get("href") for link in image_tags]
            image_links.extend(image_tags)
            next_page = soup.find("table", class_="ptb").find_all("a")[-1].get("href")
            if next_page == self.driver.current_url:
                break
            else:
                sleep(5)
                try:
                    page_count += 1
                    self.current_url = next_page
                except selenium.common.exceptions.TimeoutException:
                    print("Timed out. Sleeping for 10 seconds before retrying...")
                    sleep(10)
                    self.current_url = next_page
                soup = self.soupify()
        images = []
        links = str(len(image_links))
        for i, link in enumerate(image_links):
            print("".join(["Parsing image ", str(i + 1), "/", links]))
            sleep(5)
            soup = self.soupify(link)
            img = soup.find("img", id="img").get("src")
            images.append(img)
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def eightboobs_parse(self) -> RipInfo:
        """Parses the html for 8boobs.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="content").find_all("div", class_="title")[1].text
        images = soup.find("div", class_="gallery clear").find_all("a", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def eightmuses_parse(self) -> RipInfo:
        """Parses the html for 8muses.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        gallery = self.driver.find_element(By.CLASS_NAME, "gallery")
        while gallery is None:
            sleep(0.5)
            gallery = self.driver.find_element(By.CLASS_NAME, "gallery")
        self.lazy_load()
        soup = self.soupify()
        dir_name = soup.find("div", class_="top-menu-breadcrumb").find_all("a")[-1].text
        images = soup.find("div", class_="gallery").find_all("img")
        images = ["https://comics.8muses.com" + img.get("src").replace("/th/", "/fm/") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def eightkcosplay_parse(self) -> RipInfo:
        """Parses the html for 8kcosplay.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find("div", class_="entry-content").find_all("img", class_="j-lazy")
        images = [img.get("src").replace(".th", "") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def elitebabes_parse(self) -> RipInfo:
        """Parses the html for elitebabes.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def erosberry_parse(self) -> RipInfo:
        """
            Parses the html for erosberry.com and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="title").text
        images = soup.find("div", class_="block-post three-post flex").find_all("a", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    # Server takes forever and fails sometimes to serve data
    def __erohive_parse(self) -> RipInfo:
        """
            Parses the html for erohive.com and extracts the relevant information necessary for downloading images from the site
        """

        # Parses the html of the site
        def wait_for_post_load():
            while True:
                elm = self.driver.find_element(By.ID, "has_no_img")
                if elm.get_attribute("class"):
                    sleep(0.1)
                    break
                elif self.driver.find_elements(By.XPATH, '//h2[@class="warning-page"]'):
                    self.sleep(5)
                    self.driver.refresh()
                sleep(0.1)

        base_url = self.current_url.split("?")[0]
        wait_for_post_load()
        soup = self.soupify()
        dir_name = " ".join(soup.find("h1", class_="title").text.split(" ")[:-3])
        posts = []
        page = 0
        while True:
            print(f"Parsing page {page}")
            if page != 0:
                self.current_url = f"{base_url}?p={page}"
                wait_for_post_load()
                soup = self.soupify()
            page += 1
            links = soup.find_all("a", class_="image-thumb")
            if not links:
                break
            links = [link.get("href") for link in links]
            posts.extend(links)
        images = []
        total = len(posts)
        for i, post in enumerate(posts):
            print(f"Parsing post {i + 1}/{total}")
            self.current_url = post
            wait_for_post_load()
            soup = self.soupify()
            img = soup.find("div", class_="img").find("img").get("src")
            images.append(img)
        return RipInfo(images, dir_name, self.filename_scheme)

    def erome_parse(self) -> RipInfo:
        """
           Parses the html for erome.com and extracts the relevant information necessary for downloading images from the site
        """
        self.lazy_load(scroll_by=True, increment=1250)
        soup = self.soupify()
        dir_name = soup.find("h1").text
        posts = soup.find_all("div", class_="col-sm-12 page-content")[1].find_all("div", recursive=False)
        images = []
        for post in posts:
            img = post.find("img")
            if img:
                url = img.get("src")
                images.append(url)
                continue
            vid = post.find("video")
            if vid:
                url = vid.find("source").get("src")
                images.append(url)
                continue
        return RipInfo(images, dir_name, self.filename_scheme)

    def erothots_parse(self) -> RipInfo:
        """
            Parses the html for erothots.co and extracts the relevant information necessary for downloading images from the site
        """
        soup = self.soupify()
        dir_name = soup.find("div", class_="head-title").find("span").text
        links = soup.find("div", class_="album-gallery").find_all("a", recursive=False)
        images = [link.get("data-src") for link in links]
        return RipInfo(images, dir_name, self.filename_scheme)

    def everia_parse(self) -> RipInfo:
        """Parses the html for everia.club and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find_all("div", class_="separator")
        images = [img.find("img").get("src") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def exgirlfriendmarket_parse(self) -> RipInfo:
        """Parses the html for exgirlfriendmarket.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title-area").find("h1").text
        images = soup.find("div", class_="gallery").find_all("a", class_="thumb exo")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def fapello_parse(self) -> RipInfo:
        """
            Parses the html for fapello.com and extracts the relevant information necessary for downloading images from the site
        """
        self.lazy_load(scroll_by=True)
        soup = self.soupify()
        dir_name = soup.find("h2", class_="font-semibold lg:text-2xl text-lg mb-2 mt-4").text
        images = soup.find("div", id="content").find_all("img")
        images = [img.get("src").replace("_300px", "") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def faponic_parse(self) -> RipInfo:
        """
            Parses the html for faponic.com and extracts the relevant information necessary for downloading images from the site
        """
        self.lazy_load(scroll_by=True, scroll_pause_time=1)
        soup = self.soupify()
        dir_name = soup.find("div", class_="author-content").find("a").text
        posts = soup.find("div", id="content").find_all("div", class_="photo-item col-4-width")
        images: list[str] = []
        for post in posts:
            video = post.find("a", class_="play-video2")
            if video:
                images.append(f"video:{video.get('href')}")
            else:
                images.append(post.find("img").get("src"))
        for i, img in enumerate(images):
            if not img.startswith("video:"):
                continue
            soup = self.soupify(img.replace("video:", ""))
            vid = soup.find("source").get("src")
            images[i] = vid
        return RipInfo(images, dir_name, self.filename_scheme)

    def f5girls_parse(self) -> RipInfo:
        """Parses the html for f5girls.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find_all("div", class_="container")[2].find("h1").text
        images = []
        curr_url = self.driver.current_url.replace("?page=1", "")
        pages = len(soup.find("ul", class_="pagination").find_all("li")) - 1
        for i in range(pages):
            image_list = soup.find_all("img", class_="album-image lazy")
            image_list = [img.get("src") for img in image_list]
            images.extend(image_list)
            if i < pages - 1:
                next_page = "".join([curr_url, "?page=", str(i + 2)])
                soup = self.soupify(next_page)
        return RipInfo(images, dir_name, self.filename_scheme)

    # Started working on support for fantia.com
    def __fantia_parse(self) -> RipInfo:
        """Parses the html for fantia.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="fanclub-name").find("a").text
        curr_url = self.driver.current_url
        self.current_url = "https://fantia.jp/sessions/signin"
        input("cont")
        self.current_url = curr_url
        post_list = []
        while True:
            posts = soup.find("div", class_="row row-packed row-eq-height").find_all("a", class_="link-block")
            posts = ["https://fantia.jp" + post.get("href") for post in posts]
            post_list.extend(posts)
            next_page = soup.find("ul", class_="pagination").find("a", rel="next")
            if next_page is None:
                break
            else:
                next_page = "https://fantia.jp" + next_page.get("href")
                soup = self.soupify(next_page)
        images = []
        for post in post_list:
            self.current_url = post
            mimg = None
            try:
                mimg = self.driver.find_element(By.CLASS_NAME, "post-thumbnail bg-gray mt-30 mb-30 full-xs ng-scope")
            except selenium.common.exceptions.NoSuchElementException:
                pass
            while mimg is None:
                sleep(0.5)
                try:
                    mimg = self.driver.find_element(By.CLASS_NAME,
                                                    "post-thumbnail bg-gray mt-30 mb-30 full-xs ng-scope")
                except selenium.common.exceptions.NoSuchElementException:
                    pass
            soup = self.soupify()
            self.__print_html(soup)
            print(post)
            main_image = soup.find("div", class_="post-thumbnail bg-gray mt-30 mb-30 full-xs ng-scope").find("img").get(
                "src")
            images.append(main_image)
            # 4 and 6
            other_images = soup.find("div", class_="row no-gutters ng-scope").find_all("img")
            for img in other_images:
                url = img.split("/")
                img_url = "/".join([post, url[4], url[6]])
                images.append(img_url)
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def femjoyhunter_parse(self) -> RipInfo:
        """Parses the html for femjoyhunter.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def flickr_parse(self) -> RipInfo:
        """Parses the html for flickr.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        self.lazy_load(True)
        soup = self.soupify()
        dir_name = soup.find("h1").text.strip()
        images = []
        image_posts = []
        page_count = 1
        while True:
            print(f"Parsing page {page_count}")
            page_count += 1
            posts = soup.select_one("div.view.photo-list-view.photostream").select(
                "div.view.photo-list-photo-view.photostream")
            posts = [p.select_one("a.overlay").get("href") for p in posts]
            posts = [f"https://www.flickr.com{p}" for p in posts]
            image_posts.extend(posts)
            next_button = soup.find("a", rel="next")
            if next_button:
                next_url = next_button.get("href")
                soup = self.soupify(f"https://www.flickr.com{next_url}", lazy_load=True)
            else:
                break
        for i, post in enumerate(image_posts):
            print(f"Parsing post {i + 1}: {post}")
            delay = 0.1
            soup = self.soupify(post, delay=delay)
            script = soup.find("script", class_="modelExport").text
            while True:
                try:
                    params = '{"photoModel"' + script.split('{"photoModel"')[1]  # ("params: ")[1]
                    break
                except IndexError:
                    delay *= 2
                    soup = self.soupify(post, delay=delay)
                    script = soup.find("script", class_="modelExport").text
            params = self._extract_json_object(params)
            # print(params)
            params_json: dict = json.loads(params)
            # print(params_json)
            img_url = params_json["photoModel"]["descendingSizes"][0]["url"]
            images.append(f"{PROTOCOL}{img_url}")
        return RipInfo(images, dir_name, self.filename_scheme)

    def foxhq_parse(self) -> RipInfo:
        """Parses the html for foxhq.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1").text
        if dir_name is None:
            dir_name = soup.find("h2").text
        url = self.driver.current_url
        images = ["".join([url, td.find("a").get("href")]) for td in soup.find_all("td", align="center")[:-2]]
        return RipInfo(images, dir_name, self.filename_scheme)

    def ftvhunter_parse(self) -> RipInfo:
        """
            Parses the html for ftvhunter.com and extracts the relevant information necessary for downloading images
            from the site
        """
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def gelbooru_parse(self) -> RipInfo:
        """
            Parses the html for gelbooru.com and extracts the relevant information necessary for downloading images
            from the site
        """
        # Parses the html of the site
        tags = re.search(r"(tags=[^&]+)", self.current_url).group(1)
        tags = unquote(tags)
        dir_name = "[Gelbooru] " + tags.replace("+", " ").replace("tags=", "")
        response = requests.get(f"https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&pid=0&{tags}")
        data: dict = response.json()
        images = []
        pid = 1
        posts = data["post"]
        while len(posts) != 0:
            urls = [post["file_url"] for post in posts]
            images.extend(urls)
            response = requests.get(
                f"https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&pid={pid}&{tags}")
            pid += 1
            data = response.json()
            posts = data.get("post", [])
        return RipInfo(images, dir_name, self.filename_scheme)

    def ggoorr_parse(self) -> RipInfo:
        """
            Parses the html for ggoorr.net and extracts the relevant information necessary for downloading images
            from the site
        """
        SCHEMA = "https://cdn.ggoorr.net"
        soup = self.soupify()
        dir_name = soup.find("h1", class_="np_18px").find("a").text
        posts = soup.find("div", id="article_1").find("div").find_all(["img", "video"])
        images = []
        for post in posts:
            link = post.get("src")
            if link is None:
                link = post.find("source").get("src")
            if "https://" not in link:
                link = f"{SCHEMA}{link}"
            images.append(link)
        return RipInfo(images, dir_name, self.filename_scheme)

    def girlsofdesire_parse(self) -> RipInfo:
        """Parses the html for girlsofdesire.org and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("a", class_="albumName").text
        images = soup.find("div", id="gal_10").find_all("td", class_="vtop")
        images = ["".join(["https://girlsofdesire.org", img.find("img").get("src").replace("_thumb", "")]) for img in
                  images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def girlsreleased_parse(self) -> RipInfo:
        """Parses the html for girlsreleased.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        sleep(5)
        soup = self.soupify()
        set_name = soup.find("a", id="set_name").text
        model_name = soup.find_all("a", class_="button link")[1]
        model_name = model_name.find("span", recursive=False).text
        model_name = "".join(["[", model_name, "]"])
        dir_name = " ".join([set_name, model_name])
        images = soup.find("ul", class_="setthumbs").find_all("img")
        images = [img.get("src").replace("/t/", "/i/") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def glam0ur_parse(self) -> RipInfo:
        """Parses the html for glam0ur.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="picnav").find("h1").text
        images = soup.find("div", class_="center").find_all("a", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        for i, img in enumerate(images):
            if "/banners/" in img:
                images.pop(i)
        return RipInfo(images, dir_name, self.filename_scheme)

    def gofile_parse(self) -> RipInfo:
        """
            Parses the html for gofile.io and extracts the relevant information necessary for downloading images
            from the site
        """
        # Parses the html of the site
        sleep(5)
        soup = self.soupify()
        dir_name = soup.find("span", id="rowFolder-folderName").text
        images = soup.find("div", id="rowFolder-tableContent").find_all("div", recursive=False)
        images = [img.find("a", target="_blank").get("href") for img in images]
        images.insert(0, self.current_url)
        return RipInfo(images, dir_name, self.filename_scheme)

    def google_parse(self, gdrive_url: str = "") -> RipInfo:
        """
            Query the google drive API to get file information to download
        """
        if not gdrive_url:
            gdrive_url = self.current_url
        # Actual querying happens within the RipInfo object
        return RipInfo(gdrive_url, "", self.filename_scheme)

    def grabpussy_parse(self) -> RipInfo:
        """Parses the html for grabpussy.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find_all("div", class_="c-title")[1].find("h1").text
        images = soup.find("div", class_="gal own-gallery-images").find_all("a", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def gyrls_parse(self) -> RipInfo:
        """Parses the html for gyrls.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="single_title").text
        images = soup.find("div", id="gallery-1").find_all("a")
        images = [img.get("href") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def hanime_parse(self) -> RipInfo:
        """Parses the html for hanime.tv and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        sleep(1)  # Wait so images can load
        soup = self.soupify()
        dir_name = "Hanime Images"
        image_list = soup.find("div", class_="cuc_container images__content flex row wrap justify-center relative") \
            .find_all("a", recursive=False)
        images = [image.get("href") for image in image_list]
        return RipInfo(images, dir_name, self.filename_scheme)

    def hegrehunter_parse(self) -> RipInfo:
        """Parses the html for hegrehunter.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    # Cannot bypass captcha, so it doesn't work
    def __hentaicosplays_parse(self) -> RipInfo:
        """Parses the html for hentai-cosplays.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="main_contents").find("h2").text
        images = []
        while True:
            image_list = [img.find("img").get("src") for img in images]
            images.extend(image_list)
            next_page = soup.find("div", id="paginator").find_all("span")[-2].find("a")
            if next_page is None:
                break
            else:
                next_page = "".join(["https://hentai-cosplays.com", next_page.get("href")])
                soup = self.soupify(next_page)
        return RipInfo(images, dir_name, self.filename_scheme)

    def hentairox_parse(self) -> RipInfo:
        """Parses the html for hentairox.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="col-md-7 col-sm-7 col-lg-8 right_details").find("h1").text
        images = soup.find("div", id="append_thumbs").find("img", class_="lazy preloader").get("data-src")
        num_files = int(soup.find("li", class_="pages").text.split()[0])
        return RipInfo([images], dir_name, generate=True, num_urls=num_files)

    def heymanhustle_parse(self) -> RipInfo:
        """Parses the html for heymanhustle.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find("div", class_="galleria-thumbnails").find_all("img")
        images = [img.get("src").replace("/cache", "").split("-nggid")[0] for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def hotgirl_parse(self) -> RipInfo:
        """Parses the html for hotgirl.asia and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        url = self.driver.current_url
        # Gets the number of pages
        num_pages = soup.findAll("a", class_="page larger")
        if len(num_pages) > 0:
            num_pages = num_pages.pop().text
        else:
            num_pages = 1
        # Gets the folder name
        dir_name = soup.find("h3").text
        # Removes illegal characters from folder name
        images_html = soup.find_all('img', itemprop="image")
        del images_html[0]
        if int(num_pages) > 1:
            for index in range(2, int(num_pages) + 1):
                page_url = "".join([url, str(index), '/'])
                soup = self.soupify(page_url)
                images_list = soup.find_all("img", itemprop="image")
                del images_list[0]  # First image is just the thumbnail
                images_html.extend(images_list)
        images = [image.get("src") for image in images_html]
        return RipInfo(images, dir_name, self.filename_scheme)

    def hotpornpics_parse(self) -> RipInfo:
        """Parses the html for hotpornpics.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="hotpornpics_h1player").text
        images = soup.find("div", class_="hotpornpics_gallerybox").find_all("img")
        images = [img.get("src").replace("-square", "") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def hotstunners_parse(self) -> RipInfo:
        """Parses the html for hotstunners.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title_content").find("h2").text
        images = soup.find("div", class_="gallery_janna2").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def hottystop_parse(self) -> RipInfo:
        """Parses the html for hottystop.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        url = self.driver.current_url
        try:
            dir_name = soup.find("div", class_="Box_Large_Content").find("h1").text
        except AttributeError:
            dir_name = soup.find("div", class_="Box_Large_Content").find("u").text
        image_list = soup.find("table").find_all("a")
        images = ["".join([url, image.get("href")]) for image in image_list]
        return RipInfo(images, dir_name, self.filename_scheme)

    def hqbabes_parse(self) -> RipInfo:
        """Parses the html for hqbabes.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        model = soup.find("p", class_="desc").find("a").text
        model = "".join(["[", model, "]"])
        try:
            shoot = soup.find("p", class_="desc").find("span").text
        except AttributeError:
            shoot = "-"
        producer = soup.find("p", class_="details").find_all("a")[1].text
        producer = "".join(["(", producer, ")"])
        dir_name = " ".join([producer, shoot, model])
        ext = [".png", ".jpg", ".jpeg"]
        images = []
        image_list = soup.find_all("li", class_="item i p")
        for image in image_list:
            image_url = image.find("a").get("href")
            if any(x in image_url for x in ext):
                images.append("".join([PROTOCOL, image_url]))
        return RipInfo(images, dir_name, self.filename_scheme)

    def hqsluts_parse(self) -> RipInfo:
        """Parses the html for hqsluts.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        model = soup.find("p", class_="desc").find("a").text
        model = "".join(["[", model, "]"])
        try:
            shoot = soup.find("p", class_="desc").find("span").text
        except AttributeError:
            shoot = "-"
        producer = soup.find("p", class_="details").find_all("b")[2].find("a").text
        producer = "".join(["(", producer, ")"])
        dir_name = " ".join([producer, shoot, model])
        images = [image.find("a").get("href") for image in soup.find_all("li", class_="item i p")]
        return RipInfo(images, dir_name, self.filename_scheme)

    def hundredbucksbabes_parse(self) -> RipInfo:
        """Parses the html for 100bucksbabes.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="main-col-2").find("h2", class_="heading").text
        images = soup.find("div", class_="main-thumbs").find_all("img")
        images = ["".join([PROTOCOL, img.get("data-url")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def imgbox_parse(self) -> RipInfo:
        """Parses the html for imgbox.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="gallery-view").find("h1").text
        dir_name = dir_name.split(" - ")[0]
        images = soup.find("div", id="gallery-view-content").find_all("img")
        images = [img.get("src").replace("thumbs2", "images2").replace("_b", "_o") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def imgur_parse(self) -> RipInfo:
        """
        Parse the html for imgur.com and extracts the relevant information necessary for downloading images from the
        site
        """
        # Parses the html of the site
        client_id = Config.config.keys["Imgur"]
        if client_id == '':
            print("Client Id not properly set")
            print("Follow to generate Client Id: https://apidocs.imgur.com/#intro")
            print("Then add Client Id to Imgur in config.json under Keys")
            raise RuntimeError("Client Id Not Set")
        else:
            requests_header['Authorization'] = 'Client-ID ' + client_id
            album_hash = self.driver.current_url.split("/")[4]
            response = requests.get("https://api.imgur.com/3/album/" + album_hash, headers=requests_header)
            if response.status_code == 403:
                print("Client Id is incorrect")
                raise RuntimeError("Client Id Incorrect")
            else:
                json_data = response.json()['data']
                dir_name = json_data.get('title')
                images = [img.get("link") for img in json_data.get("images")]
        return RipInfo(images, dir_name, self.filename_scheme)

    def imhentai_parse(self) -> RipInfo:
        """
            Parses the html for imhentai.xxx and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        if "/gallery/" not in self.current_url:
            gal_code = self.current_url.split("/")[4]
            self.current_url = f"https://imhentai.xxx/gallery/{gal_code}/"
        soup = self.soupify()

        # Gets the image URL to be turned into the general image URL
        images = soup.find("img", class_="lazy preloader").get("data-src")

        # Gets the number of pages (images) in the album
        num_pages = soup.find("li", class_="pages")
        num_pages = int(num_pages.string.split()[1])
        dir_name = soup.find("h1").string

        # Removes illegal characters from folder name
        return RipInfo([images], dir_name, generate=True, num_urls=num_pages)

    def influencersgonewild_parse(self) -> RipInfo:
        """
            Parses the html for influencersgonewild.com and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        self.lazy_load(True, increment=625, scroll_pause_time=1)
        soup = self.soupify()
        dir_name = soup.find("h1", class_="g1-mega g1-mega-1st entry-title").text
        posts: bs4.ResultSet[any] = soup.find("div",
                                              class_="g1-content-narrow g1-typography-xl entry-content").find_all(
            ["img", "video"])
        images = []
        for p in posts:
            if p.name == "img":
                url = p.get("src")
                images.append(url)
            elif p.name == "video":
                url = p.find("source").get("src")
                images.append(url)
        return RipInfo(images, dir_name, self.filename_scheme)

    def inven_parse(self) -> RipInfo:
        """Parses the html for inven.co.kr and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="articleTitle").find("h1").text
        images = soup.find("div", id="BBSImageHolderTop").find_all("img")
        images = [img.get("src") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def jkforum_parse(self) -> RipInfo:
        """Parses the html for jkforum.net and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title-cont").find("h1").text
        images = soup.find("td", class_="t_f").find_all("img")
        images = [img.get("src") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def join2babes_parse(self) -> RipInfo:
        """Parses the html for join2babes.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find_all("div", class_="gallery_title_div")[1].find("h1").text
        images = soup.find("div", class_="gthumbs").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def joymiihub_parse(self) -> RipInfo:
        """Parses the html for joymiihub.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def jpg_parse(self) -> RipInfo:
        """Parses the html for jpg.church and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="text-overflow-ellipsis").find("a").text
        images = soup.find("div", class_="pad-content-listing").find_all("img")
        images = [img.get("src").replace(".md", "") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def kemono_parse(self) -> RipInfo:
        """
            Parses the html for kemono.party and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        return self.__dot_party_parse("https://kemono.party")

    # unable to load closed shadow DOM
    def __koushoku_parse(self) -> RipInfo:
        """Parses the html for koushoku.org and extracts the relevant information necessary for downloading images
        from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h2").text
        num_images = int(soup.find_all("tr")[1].find_all("td")[1].text.split()[0])
        base_url = self.driver.current_url
        images = []
        for i in range(1, num_images + 1):
            self.current_url = f"{base_url}/{str(i)}"
            # input(".")
            shadow_host = self.driver.find_element(By.XPATH, '//div[@class="main"]/a')
            shadow_host.click()
            action = ActionChains(self.driver)
            action.send_keys(Keys.TAB)
            action.click()
            input("1")
            soup = BeautifulSoup(shadow_host.get_attribute("inner_html"), PARSER)
            self.__print_html(soup)
            img = soup.find("img").get("src")
            images.append(img)
        return RipInfo(images, dir_name, self.filename_scheme)

    def leakedbb_parse(self) -> RipInfo:
        """Parses the html for leakedbb.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="flow-text left").find("strong").text
        image_links = soup.find("div", class_="post_body scaleimages").find_all("img", recursive=False)
        image_links = [img.get("src") for img in image_links]
        images = []
        for link in image_links:
            if "postimg.cc" not in link:
                images.append(link)
                continue
            soup = self.soupify(link)
            img = soup.find("a", id="download").get("href").split("?")[0]
            images.append(img)
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def livejasminbabes_parse(self) -> RipInfo:
        """Parses the html for livejasminbabes.net and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="gallery_header").find("h1").text
        images = soup.find_all("div", class_="gallery_thumb")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def lovefap_parse(self) -> RipInfo:
        """Parses the html for lovefap.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="albums-content-header").find("span").text
        images = soup.find("div", class_="files-wrapper noselect").find_all("a")
        images = [img.get("href") for img in images]
        vid = []
        for i, link in enumerate(images):
            if "/video/" in link:
                vid.append(i)
                soup = self.soupify(f"https://lovefap.com{link}")
                images.append(soup.find("video", id="main-video").find("source").get("src"))
        for i in reversed(vid):
            images.pop(i)
        return RipInfo(images, dir_name, self.filename_scheme)

    def luscious_parse(self) -> RipInfo:
        """Parses the html for luscious.net and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        if "members." in self.driver.current_url:
            self.current_url = self.driver.current_url.replace("members.", "www.")
        soup = self.soupify()
        dir_name = soup.find("h1", class_="o-h1 album-heading").text
        endpoint = "https://members.luscious.net/graphqli/?"
        album_id = self.driver.current_url.split("/")[4].split("_")[-1]
        variables = {
            "input": {
                "page": 1,
                "display": "date_newest",
                "filters": [{"name": "album_id", "value": album_id}]
            }
        }
        query = """query PictureQuery($input: PictureListInput!) {
                    picture {
                        list(input: $input) {
                            info {
                                total_items
                                has_next_page
                            }
                            items {
                                id
                                title
                                url_to_original
                                tags{
                                    id
                                    text
                                }
                            }
                        }
                    }
                }"""
        next_page = True
        images = []
        while next_page:
            response = requests.post(endpoint, headers=requests_header, json={
                "operationName": "PictureQuery",
                "query": query,
                "variables": variables
            })
            json_data = response.json()['data']['picture']['list']
            next_page = json_data['info']['has_next_page']
            variables["input"]["page"] += 1
            items = json_data['items']
            images.extend([i['url_to_original'] for i in items])
        for i, img in enumerate(images):
            if "https:" not in img:
                images[i] = "https://" + img.replace("//", "")
        return RipInfo(images, dir_name, self.filename_scheme)

    def mainbabes_parse(self) -> RipInfo:
        """Parses the html for mainbabes.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="heading").find("h2", class_="title").text
        images = soup.find("div", class_="thumbs_box").find_all("div", class_="thumb_box")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def manganato_parse(self) -> RipInfo:
        """Parses the html for manganato.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="story-info-right").find("h1").text
        next_chapter = soup.find("ul", class_="row-content-chapter").find_all("li", recursive=False)[-1].find("a")
        images = []
        counter = 1
        while next_chapter:
            print(f"Parsing Chapter {counter}")
            counter += 1
            soup = self.soupify(next_chapter.get("href"))
            chapter_images = soup.find("div", class_="container-chapter-reader").find_all("img")
            images.extend([img.get("src") for img in chapter_images])
            next_chapter = soup.find("a", class_="navi-change-chapter-btn-next a-h")
        return RipInfo(images, dir_name, self.filename_scheme)

    def maturewoman_parse(self) -> RipInfo:
        """Parses the html for maturewoman.xyz and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find("div", class_="entry-content cf").find_all("img")
        images = [img.get("src") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def metarthunter_parse(self) -> RipInfo:
        """Parses the html for hetarthunter.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def mitaku_parse(self) -> RipInfo:
        """Parses the html for mitaku.net and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="page-title").text
        nav_bar = soup.find("div", class_="wp-pagenavi").find("span")
        page_count = int(nav_bar.text.split(" ")[-1])
        base_url = self.current_url
        posts = []
        for i in range(1, page_count + 1):
            soup = self.soupify(f"{base_url}page/{str(i)}/")
            sub_posts = soup.find("div", class_="article-container").find_all("article", recursive=False)
            sub_posts = [post.find("a").get("href") for post in posts]
            posts.extend(sub_posts)
        images = []
        return RipInfo(images, dir_name)

    def morazzia_parse(self) -> RipInfo:
        """Parses the html for morazzia.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="title").text
        images = soup.find("div", class_="block-post album-item").find_all("a")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def myhentaigallery_parse(self) -> RipInfo:
        """Parses the html for myhentaigallery.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="comic-description").find("h1").text
        images = soup.find("ul", class_="comics-grid clear").find_all("li")
        images = [img.find("img").get("src").replace("/thumbnail/", "/original/") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def nakedgirls_parse(self) -> RipInfo:
        """Parses the html for nakedgirls.xxx and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="content").find("h1").text
        images = soup.find("div", class_="content").find_all("div", class_="thumb")
        images = ["".join(["https://www.nakedgirls.xxx", img.find("a").get("href")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def newgrounds_parse(self) -> RipInfo:
        """Parses the html for newgrounds.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        if self.current_url.endswith("/art/"):
            url = self.current_url.split("/")[:3]
            url = "/".join(url)
            self.current_url = f"{url}/art/"
        self.site_login()
        self.lazy_load(scroll_by=True)
        soup = self.soupify()
        dir_name = soup.find("a", class_="user-link").text.strip()
        images = []
        post_years = soup.find("div", class_="userpage-browse-content").find("div").find_all("div", recursive=False)
        for post_year in post_years:
            posts = post_year.find_all("div", class_="span-1 align-center")
            for post in posts:
                image_title = post.find("a").get("href").split("/")[-1]
                thumbnail_url = post.find("img").get("src")
                image_url = thumbnail_url.replace("/thumbnails/", "/images/") \
                    .replace("_full", f"_{dir_name}_{image_title}").replace(".webp", ".png")
                images.append(image_url)
        return RipInfo(images, dir_name, self.filename_scheme)

    def nightdreambabe_parse(self) -> RipInfo:
        """Parses the html for nightdreambabe.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("section", class_="outer-section").find("h2", class_="section-title title").text
        images = soup.find("div", class_="lightgallery thumbs quadruple fivefold").find_all("a", class_="gallery-card")
        images = ["".join([PROTOCOL, img.find("img").get("src")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def nijie_parse(self) -> RipInfo:
        """
            Parses the html for nijie.info and extracts the relevant information necessary for downloading images from the site
        """
        self.site_name = "nijie"
        self.site_login()
        soup = self.soupify()
        if not "members_illust" in self.current_url:
            member_id = re.findall(r"id=(\d+)", self.current_url)[0]
            soup = self.soupify(f"https://nijie.info/members_illust.php?id={member_id}")
        dir_name = soup.find("a", class_="name").text
        posts = []
        count = 1
        while True:
            print(f"Parsing posts page {count}")
            count += 1
            post_tags = soup.find("div", class_="mem-index clearboth").find_all("p", class_="nijiedao")
            post_links = [f"https://nijie.info{link.find('a').get('href')}" for link in post_tags]
            posts.extend(post_links)
            next_page_btn = soup.find("div", class_="right")
            if not next_page_btn:
                break
            next_page_btn = next_page_btn.find("p", class_="page_button")
            if next_page_btn:
                next_page = next_page_btn.find("a").get("href")
                soup = self.soupify(f"https://nijie.info{next_page}")
            else:
                break
        print("Collected all posts...")
        images: list[str] = []
        total = len(posts)
        for i, post in enumerate(posts):
            print(f"Parsing post {i + 1}/{total}")
            soup = self.soupify(post)
            try:
                imgs = soup.find("div", id="gallery_open").find_all("img", class_="mozamoza ngtag")
            except AttributeError:
                sleep(5)
                soup = self.soupify(post)
                imgs = soup.find("div", id="gallery_open").find_all("img", class_="mozamoza ngtag")
            image_links = [f"{PROTOCOL}{img.get('src')}" for img in imgs]
            images.extend(image_links)
        for i, image in enumerate(images):
            img_split = image.split("/")
            if img_split[4] != "nijie":
                img_split.pop(4)
            images[i] = "/".join(img_split)
        return RipInfo(images, dir_name, self.filename_scheme)

    def nine99hentai_parse(self) -> RipInfo:
        """Parses the html for 999hentai.to and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        pause_time = 1
        scroll_distance = 2500 // 4
        while True:
            self.lazy_load(True, increment=scroll_distance, scroll_pause_time=pause_time)
            soup = self.soupify()
            dir_name = soup.find("h1", class_="container main-night").text
            images = soup.find("div", class_="d-flex image-board mb container image-board-chapter").find_all("img",
                                                                                                             recursive=False)
            images = [img.get("src").replace("s.", ".") for img in images]
            fail_count = sum("data:image/gif" in img for img in images)
            if fail_count != 0:
                print(f"{str(fail_count)} Improper Urls Found, Retrying...")
                pause_time *= 2
                scroll_distance //= 2
            else:
                break
        return RipInfo(images, dir_name, self.filename_scheme)

    def nonsummerjack_parse(self) -> RipInfo:
        """Parses the html for nonsummerjack.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        self.lazy_load(True, increment=1250, scroll_back=1)
        ul = self.driver.find_element(By.CLASS_NAME, "fg-dots")
        while not ul:
            ul = self.driver.find_element(By.CLASS_NAME, "fg-dots")
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        pages = len(soup.find("ul", class_="fg-dots").find_all("li", recursive=False))
        images = []
        for i in range(1, pages + 1):
            if i != 1:
                self.driver.find_element(By.XPATH, f"//ul[@class='fg-dots']/li[{i}]").click()
                self.lazy_load(True, increment=1250, scroll_back=1)
                soup = self.soupify()
            image_list = soup.find("div", class_="foogallery foogallery-container "
                                                 "foogallery-justified foogallery-lightbox-foobox fg-justified "
                                                 "fg-light fg-shadow-outline fg-loading-default fg-loaded-fade-in "
                                                 "fg-caption-always fg-hover-fade fg-hover-zoom2 fg-ready "
                                                 "fbx-instance").find_all("a")
            image_list = [img.get("href") for img in image_list]
            images.extend(image_list)
        return RipInfo(images, dir_name, self.filename_scheme)

    def novoglam_parse(self) -> RipInfo:
        """Parses the html for novoglam.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="heading").find("h1").text
        images = soup.find("ul", id="myGalleryThumbs").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def novohot_parse(self) -> RipInfo:
        """Parses the html for novohot.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="viewIMG").find("h1").text
        images = soup.find("div", class_="runout").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def novojoy_parse(self) -> RipInfo:
        """Parses the html for novojoy.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1").text
        images = soup.find_all("img", class_="gallery-image")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def novoporn_parse(self) -> RipInfo:
        """Parses the html for novoporn.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("section", class_="outer-section").find(
            "h2").text.split()  # find("div", class_="gallery").find("h1").text.split()
        for i, word in enumerate(dir_name):
            if word == "porn":
                del dir_name[i:]
                break
        dir_name = " ".join(dir_name)
        images = soup.find_all("div", class_="thumb grid-item")
        images = [img.find("img").get("src").replace("tn_", "") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def nudebird_parse(self) -> RipInfo:
        """Parses the html for nudebird.biz and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="title single-title entry-title").text
        images = soup.find_all("a", class_="fancybox-thumb")
        images = [img.get("href") for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def nudity911_parse(self) -> RipInfo:
        """Parses the html for nudity911.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1").text
        images = soup.find("tr", valign="top").find("td", align="center").find("table", width="650") \
            .find_all("td", width="33%")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def pbabes_parse(self) -> RipInfo:
        """Parses the html for pbabes.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find_all("div", class_="box_654")[1].find("h1").text
        images = soup.find("div", style="margin-left:35px;").find_all("a", rel="nofollow")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    # Seems like all galleries have been deleted
    def _pics_parse(self) -> RipInfo:
        """Parses the html for pics.vc and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="gall_header").find("h2").text.split("-")[1].strip()
        images = []
        while True:
            image_list = soup.find("div", class_="grid").find_all("div", class_="photo_el grid-item transition_bs")
            image_list = [img.find("img").get("src").replace("/s/", "/o/") for img in image_list]
            images.extend(image_list)
            if soup.find("div", id="center_control").find("div", class_="next_page clip") is None:
                break
            else:
                next_page = "".join(["https://pics.vc", soup.find("div", id="center_control").find("a").get("href")])
                soup = self.soupify(next_page)
        return RipInfo(images, dir_name, self.filename_scheme)

    def pinkfineart_parse(self) -> RipInfo:
        """Parses the html for pinkfineart.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h5", class_="d-none d-sm-block text-center my-2")
        dir_name = "".join([t for t in dir_name.contents if type(t) == bs4.element.NavigableString])
        images = soup.find_all("div", class_="card ithumbnail-nobody ishadow ml-2 mb-3")
        images = ["".join(["https://pinkfineart.com", img.find("a").get("href")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def pleasuregirl_parse(self) -> RipInfo:
        """Parses the html for pleasuregirl.net and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h2", class_="title").text
        images = soup.find("div", class_="lightgallery-wrap").find_all("div", class_="grid-item thumb")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def pmatehunter_parse(self) -> RipInfo:
        """Parses the html for pmatehunter.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    # TODO: Fix to not need to download in the HtmlParser
    def porn3dx_parse(self) -> RipInfo:
        """
        Parses the html for porn3dx.com and extracts the relevant information necessary for downloading images from
        the site
        """
        # Parses the html of the site
        self.site_login()
        self.lazy_load(scroll_by=True, increment=1250, scroll_pause_time=1)
        soup = self.soupify()
        dir_name = soup.find("div", class_="text-white leading-none text-sm font-bold items-center self-center").text
        orig_url = self.current_url
        posts = []
        id_ = 0
        if not self.__wait_for_element('//a[@id="gallery-0"]', timeout=50):
            raise Exception("Element could not be found")

        while True:
            post = self.try_find_element(By.ID, f"gallery-{id_}")
            if not post:
                break
            posts.append(post.get_attribute("href"))
            id_ += 1

        num_posts = len(posts)
        images = []
        for i, post in enumerate(posts):
            content_found = False
            print(f"Parsing post {i + 1} of {num_posts}")
            while not content_found:
                self.current_url = post
                iframes: list[WebElement] = self.driver.find_elements(By.XPATH, '//main[@id="postView"]//iframe')
                pictures: list[WebElement] = self.driver.find_elements(By.XPATH, '//picture')
                while not iframes and not pictures:
                    sleep(5)
                    if self.current_url == orig_url:
                        ad = self.try_find_element(By.XPATH, f'//div[@class="ex-over-top ex-opened"]//div[@class="ex-over-btn"]')
                        if ad:
                            ad.click()
                    self.__clean_tabs("porn3dx")
                    iframes = self.driver.find_elements(By.XPATH, '//main[@id="postView"]//iframe')
                    pictures = self.driver.find_elements(By.XPATH, '//picture')

                if iframes:
                    for iframe in iframes:
                        iframe_url = iframe.get_attribute("src")
                        if "iframe.mediadelivery.net" in iframe_url:
                            # print(f"Found iframes: {post}")
                            content_found = True
                            self.driver.switch_to.frame(iframe)
                            source = self.driver.find_element(By.XPATH, '//video/source')
                            url = source.get_attribute("src")
                            qualities = self.driver.find_elements(By.XPATH, '//button[@data-plyr="quality"]')
                            max_quality = 0
                            for quality in qualities:
                                value = int(quality.get_attribute("value"))
                                if value > max_quality:
                                    max_quality = value
                            images.append(url + f"{{{max_quality}}}{iframe_url}")
                            self.driver.switch_to.default_content()
                if pictures:
                    for picture in pictures:
                        # print(f"Found pictures: {post}")
                        content_found = True
                        imgs = picture.find_elements(By.XPATH, "//img")
                        for img in imgs:
                            url = img.get_attribute("src")
                            if "m.porn3dx.com" in url and not any(s in url for s in ("avatar", "thumb")):
                                images.append(url)
        return RipInfo(images, dir_name, self.filename_scheme)

    # TODO: Site may be down permanently
    def putmega_parse(self) -> RipInfo:
        """Parses the html for putmega.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("a", {"data-text": "album-name"}).text
        images = []
        while True:
            image_list = soup.find("div", class_="pad-content-listing").find_all("img")
            image_list = [img.get("src").replace(".md", "") for img in image_list]
            images.extend(image_list)
            next_page = soup.find("li", class_="pagination-next")
            if next_page is None:
                break
            else:
                next_page = next_page.find("a").get("href")
                if next_page is None:
                    break
                print(next_page)
                soup = self.soupify(next_page)
        return RipInfo(images, dir_name, self.filename_scheme)

    def rabbitsfun_parse(self) -> RipInfo:
        """Parses the html for rabbitsfun.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        sleep(1)
        soup = self.soupify()
        dir_name = soup.find("h3", class_="watch-mobTitle").text
        images = soup.find("div", class_="gallery-watch").find_all("li")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def redgifs_parse(self) -> RipInfo:
        """
            Parses the html for redgifs.com and extracts the relevant information necessary for downloading images from
            the site
        """
        # Parses the html of the site
        sleep(3)
        base_request = "https://api.redgifs.com/v2/gifs?ids="
        self.lazy_load(True, 1250)
        soup = self.soupify()
        dir_name = soup.find("h1", class_="userName").text
        images = []
        posts = soup.find("div", class_="tileFeed").find_all("a", recursive=False)
        ids = []
        for post in posts:
            vid_id = post.get("href").split("/")[-1].split("#")[0]
            ids.append(vid_id)
        id_chunks = self.list_splitter(ids, 100)
        session = requests.Session()
        response = session.get("https://api.redgifs.com/v2/auth/temporary")
        response_json = response.json()
        self.requests_header["Authorization"] = f"Bearer {response_json['token']}"
        for chunk in id_chunks:
            id_param = "%2C".join(chunk)
            response = session.get(f"{base_request}{id_param}", headers=self.requests_header)
            response_json = response.json()
            gifs = response_json["gifs"]
            gifs = [gif["urls"]["hd"] for gif in gifs]
            images.extend(gifs)
        return RipInfo(images, dir_name, self.filename_scheme)

    def redpornblog_parse(self) -> RipInfo:
        """Parses the html for redpornblog.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="pic-title").find("h1").text
        images = soup.find("div", id="bigpic-image").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def rossoporn_parse(self) -> RipInfo:
        """Parses the html for rossoporn.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="content_right").find("h1").text
        images = soup.find_all("div", class_="wrapper_g")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for tag_list in images for img in
                  tag_list.find_all("img")]
        return RipInfo(images, dir_name, self.filename_scheme)

    def rule34_parse(self) -> RipInfo:
        """Read the html for rule34.xxx"""
        # Parses the html of the site
        tags = re.search(r"(tags=[^&]+)", self.current_url).group(1)
        tags = unquote(tags)
        dir_name = "[Rule34] " + tags.replace("+", " ").replace("tags=", "")
        response = requests.get(f"https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&pid=0&{tags}")
        data = response.json()
        images = []
        pid = 1
        while len(data) != 0:
            urls = [post["file_url"] for post in data]
            images.extend(urls)
            response = requests.get(
                f"https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&pid={pid}&{tags}")
            pid += 1
            data = response.json()
        return RipInfo(images, dir_name, self.filename_scheme)

    def sankakucomplex_parse(self) -> RipInfo:
        """Parses the html for sankakucomplex.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").find("a").text
        images = soup.find_all("a", class_="swipebox")
        images = [img.get("href") if PROTOCOL in img.get("href") else "".join([PROTOCOL, img.get("href")]) for img in
                  images[1:]]
        return RipInfo(images, dir_name, self.filename_scheme)

    def sensualgirls_parse(self) -> RipInfo:
        """Parses the html for sensualgirls.org and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("a", class_="albumName").text
        images = soup.find("div", id="box_289").find_all("div", class_="gbox")
        images = ["".join(["https://sensualgirls.org", img.find("img").get("src").replace("_thumb", "")]) for img in
                  images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def sexhd_parse(self) -> RipInfo:
        """Parses the html for sexhd.pics and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="photobig").find("h4").text.split(":")[1].strip()
        images = soup.find("div", class_="photobig").find_all("div", class_="relativetop")[1:]
        images = ["".join(["https://sexhd.pics", img.find("a").get("href")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def sexyaporno_parse(self) -> RipInfo:
        """Parses the html for sexyaporno.com and extracts the relevant information necessary for downloading images from the site"""
        return self.__generic_html_parser_2()

    def sexybabesart_parse(self) -> RipInfo:
        """Parses the html for sexybabesart.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="content-title").find("h1").text
        images = soup.find("div", class_="thumbs").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    # TODO: Convert to thotsbay parser since this site moved
    def __sexyegirls_parse(self) -> RipInfo:
        """Parses the html for sexy-egirls.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        sleep(1)  # Wait so images can load
        soup = self.soupify()
        url = self.driver.current_url
        subdomain = getattr(tldextract.extract(url), "subdomain")
        rippable_links = ("https://forum.sexy-egirls.com/data/video/", "/attachments/"  # , "https://gofile.io/"
                          )
        rippable_images = ("https://forum.sexy-egirls.com/attachments/", "putme.ga"  # , "https://i.imgur.com/"
                           )
        parsable_links = ("https://gofile.io/", "https://cyberdrop.me/a/")
        if subdomain == "www":
            dir_name = soup.find("div", class_="album-info-title").find("h1").text.split()
            split = 0
            for i, word in enumerate(dir_name):
                if "Pictures" in word or "Video" in word:
                    split = i
                    break
            dir_name = dir_name[:split - 1]
            dir_name = " ".join(dir_name)
            image_list = soup.find_all("div", class_="album-item")
            images = [image.find("a").get("href") for image in image_list]
        elif subdomain == "forum":
            dir_name = soup.find("h1", class_="p-title-value").find_all(text=True, recursive=False)
            dir_name = "".join(dir_name)
            images = []
            BASE_URL = "https://forum.sexy-egirls.com"
            page = 1
            while True:
                posts = soup.find("div", class_="block-body js-replyNewMessageContainer").find_all("article",
                                                                                                   recursive=False)
                posts = [p.find("div", {"class": "message-userContent lbContainer js-lbContainer"}) for p in posts]
                for p in posts:
                    links = p.find_all("a")
                    image_list = p.find_all("img")
                    videos = p.find_all("video")
                    links = [link.get("href") for link in links]
                    links = [link if SCHEME in link else "".join([BASE_URL, link]) for link in links if
                             link is not None and any(r in link for r in rippable_links)]
                    image_list = [img.get("src") for img in image_list]
                    image_list = [img if "putme.ga" not in img else img.replace(".md", "") for img in image_list if
                                  any(r in img for r in rippable_images)]
                    videos = [vid.find("source").get("src") for vid in videos]
                    videos = [vid if SCHEME in vid else "".join([BASE_URL, vid]) for vid in videos]
                    images.extend(links)
                    images.extend(image_list)
                    images.extend(videos)
                next_page = soup.find("nav", {"class": "pageNavWrapper pageNavWrapper--mixed"}) \
                    .find("a", class_="pageNav-jump pageNav-jump--next")
                print("".join(["Parsed page ", str(page)]))
                if next_page is None:
                    break
                else:
                    page += 1
                    next_page = "".join([BASE_URL, "/", next_page.get("href")])
                    soup = self.soupify(next_page)
            for link in images:
                if any(p in link for p in parsable_links):
                    site_name = urlparse(link).netloc
                    parser: Callable[[], RipInfo] = self.parser_jump_table.get(site_name)
                    image_list = self.secondary_parse(link, parser)
                    images.extend(image_list)
                    images.remove(link)
        else:
            raise InvalidSubdomain
        return RipInfo(images, dir_name, self.filename_scheme)

    def sexykittenporn_parse(self) -> RipInfo:
        """Parses the html for sexykittenporn.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="blockheader").text
        tag_list = soup.find_all("div", class_="list gallery col3")
        image_list = []
        for tag in tag_list:
            image_list.extend(tag.find_all("div", class_="item"))
        image_link = ["".join(["https://www.sexykittenporn.com",
                               image.find("a").get("href")]) for image in image_list]
        images = []
        for link in image_link:
            soup = self.soupify(link)
            images.append("".join([PROTOCOL, soup.find(
                "div", class_="image-wrapper").find("img").get("src")]))
        return RipInfo(images, dir_name, self.filename_scheme)

    def sexynakeds_parse(self) -> RipInfo:
        """Parses the html for sexynakeds.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="box").find_all("h1")[1].text
        images = soup.find("div", class_="post_tn").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def sfmcompile_parse(self) -> RipInfo:
        """Parses the html for sfmcompile.club and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        self.__print_html(soup)
        dir_name = soup.find("h1", class_="g1-alpha g1-alpha-2nd page-title archive-title").text.replace("\"", "")
        elements = []
        images = []
        while True:
            elements.extend(soup.find("ul", class_="g1-collection-items").find_all("li", class_="g1-collection-item"))
            next_page = soup.find("a", class_="g1-link g1-link-m g1-link-right next")
            if next_page:
                next_page = next_page.get("href")
                soup = self.soupify(next_page)
            else:
                break
        for element in elements:
            media = element.find("video")
            with open("test.html", "w") as f:
                f.write(str(element))
            if media:
                video_src = media.find("a").get("href")
            else:
                video_link = element.find("a", class_="g1-frame").get("href")
                soup = self.soupify(video_link)
                video_src = soup.find("video").find("source").get("src")
            images.append(video_src)
        return RipInfo(images, dir_name, self.filename_scheme)

    def silkengirl_parse(self) -> RipInfo:
        """Parses the html for silkengirl.com and silkengirl.net and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        if ".com" in self.driver.current_url:
            dir_name = soup.find("h1", class_="title").text
        else:
            dir_name = soup.find("div", class_="content_main").find("h2").text
        images = soup.find_all("div", class_="thumb_box")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def simplycosplay_parse(self) -> RipInfo:
        """Parses the html for simply-cosplay.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        sleep(5)  # Wait so images can load
        soup = self.soupify()
        dir_name = soup.find("h1", class_="content-headline").text
        image_list = soup.find("div", class_="swiper-wrapper")
        if image_list is None:
            images = [
                soup.find("div", class_="image-wrapper").find("img").get("data-src")]
        else:
            image_list = image_list.find_all("img")
            images = []
            for image in image_list:
                image = image.get("data-src").split("_")
                image.pop(1)
                image[0] = image[0][:-5]
                images.append("".join(image))
        return RipInfo(images, dir_name, self.filename_scheme)

    def simplyporn_parse(self) -> RipInfo:
        """Parses the html for simply-porn.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        # sleep(5) #Wait so images can load
        soup = self.soupify()
        dir_name = soup.find("h1", class_="mt-3 mb-3").text
        image_list = soup.find(
            "div", class_="row full-gutters").find_all("div", class_="col-6 col-lg-3")
        if len(image_list) == 0:
            images = [
                soup.find("img", class_="img-fluid ls-is-cached lazyloaded").get("src")]
        else:
            images = []
            for image in image_list:
                image = image.find("img").get("data-src").split("_")
                image[0] = image[0][:-5]
                images.append("".join(image))
        return RipInfo(images, dir_name, self.filename_scheme)

    def sxchinesegirlz_parse(self) -> RipInfo:
        """Parses the html for sxchinesegirlz.one and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        regp = re.compile(r'\d+x\d')
        soup = self.soupify()
        dir_name = soup.find("h1", class_="title single-title entry-title").text
        num_pages = soup.find("div", class_="pagination").find_all("a", class_="post-page-numbers")
        num_pages = len(num_pages)
        images = []
        curr_url = self.driver.current_url
        for i in range(num_pages):
            if i != 0:
                soup = self.soupify("".join([curr_url, str(i + 1), "/"]))
            image_list = soup.find("div", class_="thecontent").find_all("figure", class_="wp-block-image size-large",
                                                                        recursive=False)
            for img in image_list:
                img_url = img.find("img").get("src")
                if regp.search(img_url):  # Searches the img url for #x# and removes that to get full-scale image url
                    url_parts = img_url.split("-")
                    ext = "." + url_parts[-1].split(".")[-1]
                    img_url = "-".join(url_parts[:-1]) + ext
                images.append(img_url)
        return RipInfo(images, dir_name, self.filename_scheme)

    def theomegaproject_parse(self) -> RipInfo:
        """
            Parses the html for theomegaproject.org and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="omega").text
        images = soup.find("div", class_="postholder").find_all("div", class_="picture", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def thothub_parse(self) -> RipInfo:
        """
            Parses the html for thothub.lol and extracts the relevant information necessary for downloading images from the site
        """
        self.lazy_load(True, increment=625, scroll_pause_time=1)
        soup = self.soupify()
        dir_name = soup.find("div", class_="headline").find("h1").text
        if "/videos/" in self.current_url:
            vid = soup.find("video", class_="fp-engine").get("src")
            if not vid:
                vid = soup.find("div", class_="no-player").find("img").get("src")
            images = [vid]
        else:
            posts = soup.find("div", class_="images").find_all("img")
            images = []
            for img in posts:
                url = img.get("src").replace("/main/200x150/", "/sources/")
                images.append(url)
        return RipInfo(images, dir_name, self.filename_scheme)

    def thotsbay_parse(self) -> RipInfo:
        """Parses the html for thotsbay.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="album-info-title").find("h1").text
        images = []
        while not images:
            soup = self.soupify()
            images = soup.find("div", class_="album-files").find_all("a")
            images = [img.get("href") for img in images]
            sleep(1)
        vid = []
        for i, link in enumerate(images):
            if "/video/" in link:
                vid.append(i)
                soup = self.soupify(link)
                images.append(soup.find("video", id="main-video").find("source").get("src"))
        for i in reversed(vid):
            images.pop(i)
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def tikhoe_parse(self) -> RipInfo:
        """Parses the html for tikhoe.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="album-title").find("h1").text
        file_tag = soup.find("div", class_="album-files")
        images = file_tag.find_all("a")
        videos = file_tag.find_all("source")
        images = [img.get("href") for img in images]
        videos = [vid.get("src") for vid in videos]
        images.extend(videos)
        self.driver.quit()
        return RipInfo(images, dir_name, self.filename_scheme)

    def titsintops_parse(self) -> RipInfo:
        """
            Parses the html for titsintops.com and extracts the relevant information necessary for downloading images from the site
        """
        # Parses the html of the site
        # noinspection PyPep8Naming
        SITE_URL = "https://titsintops.com"
        self.site_login()
        cookies = self.driver.get_cookies()
        cookie_str = ''
        for c in cookies:
            cookie_str += "".join([c['name'], '=', c['value'], ';'])
        requests_header["cookie"] = cookie_str
        soup = self.soupify()
        dir_name = soup.find("h1", class_="p-title-value").text
        images = []
        external_links: dict[str, list[str]] = self.__create_external_link_dict()
        page_count = 1
        while True:
            print(f"Parsing page {page_count}")
            page_count += 1
            posts = soup.find("div", class_="block-body js-replyNewMessageContainer").find_all("div",
                                                                                               class_="message-content js-messageContent")
            for post in posts:
                imgs = post.find("article", class_="message-body js-selectToQuote").find_all("img")
                if imgs:
                    imgs = [im.get("src") for im in imgs if "http" in im]
                    images.extend(imgs)
                videos = post.find_all("video")
                if videos:
                    video_urls = [f"{SITE_URL}{vid.find('source').get('src')}" for vid in videos]
                    images.extend(video_urls)
                iframes = post.find_all("iframe")
                if iframes:
                    # with open("test.html", "w") as f:
                    #     f.write(str(iframes))
                    embedded_urls = [em.get("src") for em in iframes]
                    embedded_urls = self.parse_embedded_urls(embedded_urls)
                    images.extend(embedded_urls)
                attachments = post.find("ul", class_="attachmentList")
                if attachments:
                    attachments = attachments.find_all("a", class_="file-preview js-lbImage")
                    if attachments:
                        attachment_urls = [f"{SITE_URL}{attach.get('href')}" for attach in attachments]
                        images.extend(attachment_urls)
                links = post.find("article", class_="message-body js-selectToQuote").find_all("a")
                if links:
                    links = [link.get("href") for link in links]
                    # print(links)
                    filtered_links = self.__extract_external_urls(links)
                    downloadable_links = self.__extract_downloadable_links(filtered_links, external_links)
                    images.extend(downloadable_links)
            next_page = soup.find("a", class_="pageNav-jump pageNav-jump--next")
            if next_page:
                next_page = next_page.get("href")
                soup = self.soupify(f"{SITE_URL}{next_page}")
            else:
                self.__save_external_links(external_links)
                break
        return RipInfo(images, dir_name, self.filename_scheme)

    def tsumino_parse(self) -> RipInfo:
        """Parses the html for tsumino.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="book-title").text
        num_pages = int(soup.find("div", id="Pages").text.strip())
        pager_url = self.driver.current_url.replace("/entry/", "/Read/Index/") + "?page="
        images = []
        for i in range(1, num_pages + 1):
            soup = self.soupify(pager_url + str(i), 3)
            src = soup.find("img", class_="img-responsive reader-img").get("src")
            images.append(src)
        return RipInfo(images, dir_name, self.filename_scheme)

    # TODO: Cert Date Invalid; Build Test Later
    def tuyangyan_parse(self) -> RipInfo:
        """Parses the html for tuyangyan.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="post-title entry-title").find("a").text
        dir_name = dir_name.split("[")
        num_files = dir_name[1].replace("P]", "")
        dir_name = dir_name[0]
        dir_name = "".join(dir_name)
        if int(num_files) > 20:
            pages = ceil(int(num_files) / 20)
            images = []
            page_url = self.driver.current_url
            for i in range(pages):
                if i > 0:
                    url = "".join([page_url, str(i + 1), "/"])
                    soup = self.soupify(url)
                image_list = soup.find(
                    "div", class_="entry-content clearfix").find_all("img")
                images.extend(image_list)
        else:
            images = soup.find(
                "div", class_="entry-content clearfix").find_all("img")
        images = ["".join([PROTOCOL, img.get("src")]) for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    def wantedbabes_parse(self) -> RipInfo:
        """Parses the html for wantedbabes.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="main-content").find("h1").text
        images = soup.find_all("div", class_="gallery")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
        return RipInfo(images, dir_name, self.filename_scheme)

    # TODO: Work on saving self.driver across sites to avoid relogging in
    def v2ph_parse(self) -> RipInfo:
        """Parses the html for v2ph.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        global logged_in
        try:
            cookies = pickle.load(open("cookies.pkl", "rb"))
            logged_in = True
        except IOError:
            cookies = []
            logged_in = False
        for cookie in cookies:
            self.driver.add_cookie(cookie)
        LAZY_LOAD_ARGS = (True, 1250, 0.75)
        self.lazy_load(*LAZY_LOAD_ARGS)
        soup = self.soupify()
        dir_name = soup.find("h1", class_="h5 text-center mb-3").text
        num = soup.find("dl", class_="row mb-0").find_all("dd")[-1].text
        digits = ("0", "1", "2", "3", "4", "5", "6", "7", "8", "9")
        for i, d in enumerate(num):
            if d not in digits:
                num = num[:i]
                break
        num_pages = int(num)
        base_url = self.current_url
        base_url = base_url.split("?")[0]
        images = []
        parse_complete = False
        for i in range(num_pages):
            if i != 0:
                next_page = "".join([base_url, "?page=", str(i + 1)])
                self.current_url = next_page
                if not logged_in:
                    curr_page = self.current_url
                    self.current_url = "https://www.v2ph.com/login?hl=en"
                    while self.current_url == "https://www.v2ph.com/login?hl=en":
                        sleep(0.1)
                    pickle.dump(self.driver.get_cookies(), open("cookies.pkl", "wb"))
                    self.current_url = curr_page
                    logged_in = True
                self.lazy_load(*LAZY_LOAD_ARGS)
                soup = self.soupify()
            while True:
                image_list = soup.find("div", class_="photos-list text-center").find_all("div",
                                                                                         class_="album-photo my-2")
                if len(image_list) == 0:
                    parse_complete = True
                    break
                image_list = [img.find("img").get("src") for img in image_list]
                if not any([img for img in image_list if "data:image/gif;base64" in img]):
                    break
                else:
                    self.driver.find_element(By.TAG_NAME, 'body').send_keys(Keys.CONTROL + Keys.HOME)
                    self.lazy_load(*LAZY_LOAD_ARGS)
                    soup = self.soupify()
            images.extend(image_list)
            if parse_complete:
                break
        return RipInfo(images, dir_name, self.filename_scheme)

    def xarthunter_parse(self) -> RipInfo:
        """Parses the html for xarthunter.com and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def xmissy_parse(self) -> RipInfo:
        """Parses the html for xmissy.nl and extracts the relevant information necessary for downloading images from the site"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", id="pagetitle").text
        images = soup.find("div", id="gallery").find_all("div", class_="noclick-image")
        images = [
            img.find("img").get("data-src") if img.find("img").get("data-src") is not None else img.find("img").get(
                "src")
            for img in images]
        return RipInfo(images, dir_name, self.filename_scheme)

    # endregion

    def __clean_tabs(self, url_match: str):
        window_handles = self.driver.window_handles
        for handle in window_handles:
            self.driver.switch_to.window(handle)
            if url_match not in self.current_url:
                self.driver.close()
        self.driver.switch_to.window(self.driver.window_handles[0])

    def _test_parse(self, given_url: str, debug: bool, print_site: bool) -> RipInfo:
        """Test the parser to see if it properly returns image URL(s), number of images, and folder name."""
        self.driver = None
        try:
            options = Options()
            if not debug:
                options.add_argument("-headless")
            options.add_argument(DRIVER_HEADER)
            self.driver = webdriver.Firefox(options=options)
            self.current_url = given_url.replace("members.", "www.")
            site_name = self._test_site_check(given_url)
            if site_name == "999hentai":
                site_name = "nine99hentai"
            print(f"Testing: {site_name}_parse")
            data: RipInfo = eval(f"self.{site_name}_parse()")
            print(data.urls[0].referer)
            out = [d.url for d in data]
            with open("test.json", "w") as f:
                json.dump(out, f, indent=4)
            return data
        except Exception:
            with open("test.html", "w", encoding="utf-16") as f:
                f.write(self.driver.page_source)
            raise
        finally:
            if print_site:
                with open("test.html", "w") as f:
                    f.write(self.driver.page_source)
            self.driver.quit()

    def _test_site_check(self, url: str) -> str:
        domain = urlparse(url).netloc
        requests_header['referer'] = "".join([SCHEME, domain, "/"])
        domain = self._domain_name_override(domain)
        # if not domain:
        #     domain = domain.split(".")[-2]
        # domain = "inven" if "inven.co.kr" in domain else domain.split(".")[-2]
        # Hosts images on a different domain
        if "https://members.hanime.tv/" in url or "https://hanime.tv/" in url:
            requests_header['referer'] = "https://cdn.discordapp.com/"
        elif "https://kemono.party/" in url:
            requests_header['referer'] = ""
        return domain

    @staticmethod
    def _domain_name_override(url: str) -> str:
        special_domains = ("inven.co.kr", "danbooru.donmai.us")
        url_split = url.split(".")
        return url_split[-3] if any(domain in url for domain in special_domains) else url_split[-2]

    def secondary_parse(self, link: str, parser: Callable[[webdriver.Firefox], RipInfo]) -> list[ImageLink]:
        """Parses the html for links for supported sites used in other sites"""
        curr = self.current_url
        self.current_url = link
        images = parser(self.driver).urls
        self.current_url = curr
        return images

    def parse_embedded_urls(self, urls: list[str]) -> list[str]:
        parsed_urls = []
        imgur_key = Config.config.keys["Imgur"]
        for url in urls:
            if "imgur" in url:
                response = requests.get(url)
                soup = self.soupify(response=response)
                imgur_url = soup.find("a", id="image-link").get("href")
                image_hash = imgur_url.split("#")[-1]
                response = requests.get(f"https://api.imgur.com/3/image/{image_hash}",
                                        headers={"Authorization": f"Client-ID {imgur_key}"})
                response_json = response.json()
                parsed_urls.append(response_json["data"]["link"])
        return parsed_urls

    @staticmethod
    def __create_external_link_dict() -> dict[str, list[str]]:
        external_links: dict[str, list[str]] = {}
        for site in EXTERNAL_SITES:
            external_links[site] = []
        return external_links

    def __extract_external_urls(self, urls: list[str]) -> dict[str, list[str]]:
        external_links: dict[str, list[str]] = self.__create_external_link_dict()
        for site in external_links.keys():
            ext_links = [self._extract_url(url) + "\n" for url in urls if url and site in url]
            external_links[site].extend(ext_links)
        return external_links

    def __extract_possible_external_urls(self, possible_urls: list[str]) -> dict[str, list[str]]:
        external_links: dict[str, list[str]] = self.__create_external_link_dict()
        for site in external_links.keys():
            for text in possible_urls:
                if site not in text:
                    continue
                parts = text.split()
                for part in parts:
                    if site in part:
                        external_links[site].append(part + "\n")
        return external_links

    @staticmethod
    def __save_external_links(links: dict[str, list[str]]):
        for site in links:
            if not links[site]:
                continue
            with open(f"{site}_links.txt", "a", encoding="utf-16") as f:
                f.writelines(links[site])

    def __extract_downloadable_links(self, src_dict: dict[str, list[str]], dst_dict: dict[str, list[str]]) -> list[str]:
        """
            Extract links that can be downloaded while copying links that cannot be downloaded to dst_dict.
            Returns list of links that can be downloaded.
        """
        downloadable_links = []
        downloadable_sites = ("sendvid.com",)
        for site in src_dict:
            if any(s == site for s in downloadable_sites):
                downloadable_links.extend(src_dict[site])
                src_dict[site].clear()
            else:
                dst_dict[site].extend(src_dict[site])
        return self.__resolve_downloadable_links(downloadable_links)

    @staticmethod
    def __resolve_downloadable_links(links: list[str]) -> list[str]:
        resolved_links = []
        for link in links:
            if "sendvid.com" in link:
                response = requests.get(link)
                soup = BeautifulSoup(response.content, "lxml")
                source_link = soup.find("source", id="video_source").get("src")
                resolved_links.append(source_link)
            else:
                resolved_links.append(link)
        return resolved_links

    @staticmethod
    def __remove_duplicates(list_: list) -> list:
        seen = set()
        clean_list = []
        for item in list_:
            if item not in seen:
                clean_list.append(item)
                seen.add(item)
        return clean_list

    def __wait_for_element(self, xpath: str, delay: float = 0.1, timeout: float = 10) -> bool:
        if timeout != -1:
            timeout *= 1_000_000_000
        start_time = time.time_ns()
        while not self.driver.find_elements(By.XPATH, xpath):
            sleep(delay)
            curr_time = time.time_ns()
            if timeout != -1 and curr_time - start_time >= timeout:
                return False
        return True

    def __print_html(self, soup: BeautifulSoup = None):
        with open("html.html", "w", encoding="utf-8") as f:
            if soup:
                f.write(str(soup))
            else:
                f.write(self.driver.page_source)

    @staticmethod
    def _extract_url(text: str) -> str:
        protocol_index = text.find("https:")
        if protocol_index == -1:
            return ""
        url = text[protocol_index:]
        url.replace("</a>", "")
        return url

    @staticmethod
    def _print_debug_info(title: str, *data, fd="output.txt", clear=False):
        with open(fd, "w" if clear else "a") as f:
            f.write(f"[{title}]\n")
            for d in data:
                f.write(f"\t{str(d).strip()}\n")

    @staticmethod
    def _extract_json_object(json_: str) -> str:
        depth = 0
        escape = False
        string = False
        for i, char in enumerate(json_):
            if escape:
                escape = False
            else:
                if char == '\\':
                    escape = True
                if char == '{':
                    depth += 1
                elif char == '}':
                    depth -= 1
                elif char == '"':
                    string = not string
            if depth == 0:
                return json_[:i + 1]
        raise Exception("Improperly formatted json: " + json_)

    def scroll_page(self, distance: int = 1250):
        curr_height = self.driver.execute_script("return window.pageYOffset")
        scroll_script = f"window.scrollBy({{top: {int(curr_height) + distance}, left: 0, behavior: 'smooth'}});"
        self.driver.execute_script(scroll_script)

    def dump_cookies(self):
        with open("cookies.txt", "w") as f:
            for cookie in self.driver.get_cookies():
                f.write(f"{cookie}\n")

    def dump_cookies_netscape(self):
        cookies = self.driver.get_cookies()
        formatted_cookies = []
        for c in cookies:
            formatted_cookie = f"{c['domain']} FALSE {c['path']} {c['httpOnly']} {c['expiry']} {c['name']} {c['value']}\n"
            formatted_cookies.append(formatted_cookie)
        with open("cookies.txt", "w") as f:
            f.writelines(formatted_cookies)

    def lazy_load(self, scroll_by: bool = False, increment: int = 2500, scroll_pause_time: float = 0.5,
                  scroll_back: int = 0, rescroll: bool = False):
        """
            Load lazy loaded images by scrolling the page
        :param scroll_by: Whether to scroll through the page or instantly scroll to the bottom
        :param increment: Distance to scroll by each iteration
        :param scroll_pause_time: Seconds to wait between each scroll
        :param scroll_back: Distance to scroll back by after reaching the bottom of the page
        :param rescroll: Whether scrolling through the page again
        """
        last_height = self.driver.execute_script("return window.pageYOffset")
        if rescroll:
            self.driver.execute_script("window.scrollTo(0, 0);")
        if scroll_by:
            scroll_script = "".join(["window.scrollBy({top: ", str(increment), ", left: 0, behavior: 'smooth'});"])
            height_check_script = "return window.pageYOffset"
        else:
            scroll_script = "window.scrollTo(0, document.body.scrollHeight);"
            height_check_script = "return document.body.scrollHeight"
        while True:
            self.driver.execute_script(scroll_script)
            sleep(scroll_pause_time)
            new_height = self.driver.execute_script(height_check_script)
            if new_height == last_height:
                if scroll_back > 0:
                    for _ in range(scroll_back):
                        self.driver.execute_script(
                            "".join(
                                ["window.scrollBy({top: ", str(-increment), ", left: 0, behavior: 'smooth'});"]))
                        sleep(scroll_pause_time)
                    sleep(scroll_pause_time)
                break
            last_height = new_height
        self.driver.implicitly_wait(10)

    @staticmethod
    def try_click(element: WebElement, func: Callable[[], None]):
        try:
            element.click()
        except selenium.common.exceptions.ElementClickInterceptedException:
            func()

    @staticmethod
    def log_failed_url(url: str):
        with open("failed.txt", "a") as f:
            f.write("".join([url, "\n"]))

    def try_find_element(self, by: str, value: str) -> WebElement | None:
        try:
            return self.driver.find_element(by, value)
        except selenium.common.exceptions.NoSuchElementException:
            return None

    def soupify(self, url: str | None = None, delay: float = 0, lazy_load: bool = False,
                response: requests.Response = None, xpath: str = "") -> BeautifulSoup:
        """
            Return BeautifulSoup object of html from WebDriver or given Response if provided
        :param url: Url to switch to before getting the BeautifulSoup object of the html (not needed if WebDriver is
            currently at this url)
        :param delay: Seconds to wait (for JS and other events) before creating BeautifulSoup object
        :param lazy_load: Whether there are elements on the webpage that are lazily loaded
        :param response: Response object to create BeautifulSoup object from (if set, ignores other arguments)
        :param xpath: XPath to an element the WebDriver should wait until it exists before creating the BeautifulSoup object
        """
        if response:
            return BeautifulSoup(response.content, PARSER)
        if url:
            self.current_url = url
        if delay != 0:
            sleep(delay)
        if xpath:
            self.__wait_for_element(xpath)
        if lazy_load:
            self.lazy_load(True)
        html = self.driver.page_source
        return BeautifulSoup(html, PARSER)

    @staticmethod
    def list_splitter(lst: list, size: int):
        for i in range(0, len(lst), size):
            yield lst[i:i + size]


if __name__ == "__main__":
    requests_header: dict[str, str] = {
        'User-Agent':
            'Mozilla/5.0 (Windows NT 10.0; Win64; x'
            '64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36',
        'referer':
            'https://imhentai.xxx/',
        'cookie':
            ''
    }

    parser = argparse.ArgumentParser()
    parser.add_argument("url", type=str)
    parser.add_argument("-d", "--debug", action="store_true")
    parser.add_argument("-p", "--print", action="store_true")
    args = parser.parse_args()

    parser = HtmlParser(requests_header)

    start = time.process_time_ns()
    # HtmlParser._download_from_mega("https://mega.nz/folder/hAhFzTBB#-e9q8FxVGyeY5wHuiZOOeg", "./Temp")
    print(parser._test_parse(args.url, args.debug, args.print))
    end = time.process_time_ns()
