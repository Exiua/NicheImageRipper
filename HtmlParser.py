from __future__ import annotations

import json
import os
import pickle
import re
import sys
from math import ceil
from os import path
from time import sleep
from typing import Callable
from urllib.parse import urlparse

import bs4
import requests
import selenium
import tldextract
from bs4 import BeautifulSoup
from selenium import webdriver
from selenium.webdriver import Keys
from selenium.webdriver.common.by import By
from selenium.webdriver.firefox.options import Options

from RipInfo import RipInfo
from RipperExceptions import InvalidSubdomain, RipperError
from rippers import DRIVER_HEADER, requests_header, PARSER, DEBUG, PROTOCOL, SCHEME, read_config, url_check

logged_in: bool
PARSER_SWITCH: dict[str, Callable[[], RipInfo]]
TEST_PARSER: str = ""


class HtmlParser:
    def __init__(self, header: dict[str, str], site_name: str = ""):
        global logged_in
        options = Options()
        options.headless = site_name != "v2ph" or logged_in
        options.add_argument = DRIVER_HEADER
        options.set_preference("dom.disable_beforeunload", True)
        options.set_preference("browser.tabs.warnOnClose", False)
        self.driver = webdriver.Firefox(options=options)
        self.interrupted: bool = False
        self.site_name: str = site_name
        self.sleep_time: float = 0.2
        self.given_url: str = ""
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
            "manganato": self.manganato_parse
        }

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
        self.driver.get(url)
        self.site_name = self.site_check()
        site_parser: Callable[[], RipInfo] = self.parser_jump_table.get(self.site_name)
        try:
            site_info: RipInfo = site_parser()
        finally:
            self.driver.quit()
        self.write_partial_save(site_info, url)
        self.driver.quit()
        return site_info

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
            pass  # Doesn't matter if the cached data doesn't exist, will regen instead

    def site_login(self, site_name: str, given_url: str, logins: dict[str, str]):
        curr_url = self.driver.current_url
        if site_name == "sexy-egirls" and "forum." in given_url:
            self.driver.implicitly_wait(10)
            self.driver.get("https://forum.sexy-egirls.com/login/")
            self.driver.find_element(By.XPATH, "//input[@type='text']").send_keys(logins["sexy-egirls"][0])
            self.driver.find_element(By.XPATH, "//input[@type='password']").send_keys(logins["sexy-egirls"][1])
            self.driver.find_element(By.XPATH, "//button[@type='submit']").click()
        self.driver.get(curr_url)

    def __generic_html_parser_1(self):
        soup = self.soupify()
        image_list = soup.find("ul", class_="list-gallery a css").find_all("a")
        images = [image.get("href") for image in image_list]
        dir_name = image_list[0].find("img").get("alt")
        return RipInfo(images, dir_name)

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
        return RipInfo(images, dir_name)

    def __dot_party_parse(self, domain_url: str):
        cookies = self.driver.get_cookies()
        cookie_str = ''
        for c in cookies:
            cookie_str += "".join([c['name'], '=', c['value'], ';'])
        requests_header["cookie"] = cookie_str
        base_url = self.driver.current_url
        base_url = base_url.split("/")
        source_site = base_url[3]
        base_url = "/".join(base_url[:6])
        page_url = self.driver.current_url.split("?")[0]
        sleep(5)
        soup = self.soupify()
        dir_name = soup.find("h1", id="user-header__info-top").find("span", itemprop="name").text
        dir_name = f"{dir_name} - ({source_site})"
        page = 0
        image_links = []
        while True:
            page += 1
            print("".join(["Parsing page ", str(page)]))
            image_list = soup.find("div", class_="card-list__items").find_all("article")
            image_list = ["".join([base_url, "/post/", img.get("data-id")]) for img in image_list]
            image_links.extend(image_list)
            next_page = f"{page_url}?o={str(page * 25)}"
            print(next_page)
            self.driver.get(next_page)
            soup = self.soupify()
            self._print_html(soup)
            test_str = soup.find("h2", class_="site-section__subheading")
            if test_str is not None:
                break
        images = []
        mega_links = []
        gdrive_links = []
        num_posts = len(image_links)
        ATTACHMENTS = (".zip", ".rar", ".mp4", ".webm", ".psd", ".clip")
        for i, link in enumerate(image_links):
            print("".join(["Parsing post ", str(i + 1), " of ", str(num_posts)]))
            self.driver.get(link)
            soup = self.soupify()
            links = soup.find_all("a")
            links = [link.get("href") for link in links]
            possible_links_p = soup.find_all("p")
            possible_links = [tag.text for tag in possible_links_p]
            possible_links_div = soup.find_all("div")
            possible_links.extend([tag.text for tag in possible_links_div])
            m_links = [link + "\n" for link in links if "mega.nz" in link]
            gd_links = [link + "\n" for link in links if "drive.google.com" in link]
            for text in possible_links:
                for domain_list in (("drive.google.com", gd_links), ("mega.nz", m_links)):
                    if domain_list[0] not in text:
                        continue
                    parts = text.split()
                    for part in parts:
                        if domain_list[0] in part:
                            domain_list[1].append(part + "\n")
            attachments = [domain_url + link if domain_url not in link else link for link in links
                           if any(ext in link for ext in ATTACHMENTS)]
            images.extend(attachments)
            mega_links.extend(list(dict.fromkeys(m_links)))
            gdrive_links.extend(list(dict.fromkeys(gd_links)))
            image_list = soup.find("div", class_="post__files")
            if image_list is not None:
                image_list = image_list.find_all("a", class_="fileThumb image-link")
                image_list = ["".join([domain_url.replace("//", "//data1."), img.get("href")]) for img in image_list]
                images.extend(image_list)
        with open("megaLinks.txt", "a", encoding="utf-16") as f:
            f.writelines(mega_links)
        with open("gdriveLinks.txt", "a", encoding="utf-16") as f:
            f.writelines(gdrive_links)
        return RipInfo(images, dir_name)

    def agirlpic_parse(self) -> RipInfo:
        """Read the html for agirlpic.com"""
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
                self.driver.get("".join([base_url, str(i + 1), "/"]))
                soup = self.soupify()
        return RipInfo(images, dir_name)

    def arca_parse(self) -> RipInfo:
        """Read the html for arca.live"""
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
        return RipInfo(images, dir_name)

    def artstation_parse(self) -> RipInfo:
        """Read the html for artstation.com"""
        # Parses the html of the site
        self.lazy_load()
        soup = self.soupify()
        dir_name = soup.find("h1", class_="artist-name").text
        posts = soup.find("div", class_="gallery").find_all("a", class_="project-image")
        posts = ["https://www.artstation.com" + p.get("href") for p in posts]
        num_posts = len(posts)
        images = []
        for i, post in enumerate(posts):
            print(f'Parsing post {str(i + 1)} of {num_posts}')
            self.driver.get(post)
            try:
                self.driver.find_element(By.XPATH, '//div[@class="matureContent-container"]//a').click()
            except selenium.common.exceptions.NoSuchElementException:
                pass
            soup = self.soupify()
            image_lists = soup.find("div", {"class": "project-assets-list"}).find_all("picture")
            for image in image_lists:
                link = image.find("source").get("srcset")
                images.append(link)
        self.driver.quit()
        return RipInfo(images, dir_name)

    def babecentrum_parse(self) -> RipInfo:
        """Read the html for babecentrum.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="pageHeading").find_all("cufontext")
        dir_name = [w.text for w in dir_name]
        dir_name = " ".join(dir_name).strip()
        images = soup.find("table").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def babeimpact_parse(self) -> RipInfo:
        """Read the html for babeimpact.com"""
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
            self.driver.get(image)
            soup = self.soupify()
            images.append("".join([PROTOCOL, soup.find(
                "div", class_="image-wrapper").find("img").get("src")]))
        return RipInfo(images, dir_name)

    def babeuniversum_parse(self) -> RipInfo:
        """Read the html for babeuniversum.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title").find("h1").text
        images = soup.find("div", class_="three-column").find_all("div", class_="thumbnail")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def babesandbitches_parse(self) -> RipInfo:
        """Read the html for babesandbitches.net"""
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
        return RipInfo(images, dir_name)

    def babesandgirls_parse(self) -> RipInfo:
        """Read the html for babesandgirls.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="title").text
        images = soup.find("div", class_="block-post album-item").find_all("a", class_="item-post")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def babesaround_parse(self) -> RipInfo:
        """Read the html for babesaround.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("section", class_="outer-section").find(
            "h2").text  # soup.find("div", class_="ctitle2").find("h1").text
        images = soup.find_all("div", class_="lightgallery thumbs quadruple fivefold")
        images = [tag for img in images for tag in img.find_all("a", recursive=False)]
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def babesbang_parse(self) -> RipInfo:
        """Read the html for babesbang.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="main-title").text
        images = soup.find_all("div", class_="gal-block")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
        return RipInfo(images, dir_name)

    def babesinporn_parse(self) -> RipInfo:
        """Read the html for babesinporn.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="blockheader pink center lowercase").text
        images = soup.find_all("div", class_="list gallery")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
        return RipInfo(images, dir_name)

    def babesmachine_parse(self) -> RipInfo:
        """Read the html for babesmachine.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="gallery").find("h2").find("a").text
        images = soup.find("div", id="gallery").find("table").find_all("tr")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def bestprettygirl_parse(self) -> RipInfo:
        """Read the html for bestprettygirl.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find_all("img", class_="aligncenter size-full")
        images = [img.get("src") for img in images]
        return RipInfo(images, dir_name)

    def buondua_parse(self) -> RipInfo:
        """Read the html for buondua.com"""
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
                self.driver.get(next_page)
                self.lazy_load(True)
                soup = self.soupify()
        return RipInfo(images, dir_name)

    def bustybloom_parse(self) -> RipInfo:
        """Read the html for bustybloom.com"""
        return self.__generic_html_parser_2()

    def cherrynudes_parse(self) -> RipInfo:
        """Read the html for cherrynudes.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("title").text.split("-")[0].strip()
        images = soup.find("ul", class_="photos").find_all("a")
        content_url = self.driver.current_url.replace("www", "cdn")
        images = ["".join([content_url, img.get("href")]) for img in images]
        return RipInfo(images, dir_name)

    def chickteases_parse(self) -> RipInfo:
        """Read the html for chickteases.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", id="galleryModelName").text
        images = soup.find_all("div", class_="minithumbs")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def cool18_parse(self) -> RipInfo:
        """Read the html for cool18.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("td", class_="show_content").find("b").text
        images = soup.find("td", class_="show_content").find("pre").find_all("img")
        images = [img.get("src") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name)

    def coomer_parse(self) -> RipInfo:
        """Read the html for coomer.party"""
        # Parses the html of the site
        return self.__dot_party_parse("https://coomer.party")

    def cupe_parse(self) -> RipInfo:
        """Read the html for cup-e.club"""
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
        return RipInfo(images, dir_name)

    def cutegirlporn_parse(self) -> RipInfo:
        """Read the html for cutegirlporn.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="gal-title").text
        images = soup.find("ul", class_="gal-thumbs").find_all("li")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("/t", "/")]) for img in images]
        return RipInfo(images, dir_name)

    def cyberdrop_parse(self) -> RipInfo:
        """Read the html for cyberdrop.me"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", id="title").text
        image_list = soup.find_all("div", class_="image-container column")
        images = [image.find("a", class_="image").get("href")
                  for image in image_list]
        return RipInfo(images, dir_name)

    def decorativemodels_parse(self) -> RipInfo:
        """Read the html for decorativemodels.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="center").text
        images = soup.find("div", class_="list gallery").find_all("div", class_="item")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def deviantart_parse(self) -> RipInfo:
        """Read the html for deviantart.com"""
        # Parses the html of the site
        curr = self.driver.current_url
        dir_name = curr.split("/")[3]
        images = [curr]
        self.driver.quit()
        return RipInfo(images, dir_name)

    def dirtyyoungbitches_parse(self) -> RipInfo:
        """Read the html for dirtyyoungbitches.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title-holder").find("h1").text
        images = soup.find("div", class_="container cont-light").find("div", class_="images").find_all("a",
                                                                                                       class_="thumb")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def eahentai_parse(self) -> RipInfo:
        """Read the html for eahentai.com"""
        # Parses the html of the site
        sleep(1)
        # Load lazy loaded images
        self.lazy_load()
        soup = self.soupify()
        dir_name = soup.find("h2").text
        images = soup.find("div", class_="gallery").find_all("a")
        images = [img.find("img").get("src").replace("/thumbnail", "").replace("t.", ".") for img in images]
        return RipInfo(images, dir_name)

    def ehentai_parse(self) -> RipInfo:
        """Read the html for e-hentai.org"""
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
                    self.driver.get(next_page)
                except selenium.common.exceptions.TimeoutException:
                    print("Timed out. Sleeping for 10 seconds before retrying...")
                    sleep(10)
                    self.driver.get(next_page)
                soup = self.soupify()
        images = []
        links = str(len(image_links))
        for i, link in enumerate(image_links):
            print("".join(["Parsing image ", str(i + 1), "/", links]))
            sleep(5)
            self.driver.get(link)
            soup = self.soupify()
            img = soup.find("img", id="img").get("src")
            images.append(img)
        self.driver.quit()
        return RipInfo(images, dir_name)

    def eightboobs_parse(self) -> RipInfo:
        """Read the html for 8boobs.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="content").find_all("div", class_="title")[1].text
        images = soup.find("div", class_="gallery clear").find_all("a", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def eightmuses_parse(self) -> RipInfo:
        """Read the html for 8muses.com"""
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
        return RipInfo(images, dir_name)

    def eightkcosplay_parse(self) -> RipInfo:
        """Read the html for 8kcosplay.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find("div", class_="entry-content").find_all("img", class_="j-lazy")
        images = [img.get("src").replace(".th", "") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name)

    def elitebabes_parse(self) -> RipInfo:
        """Read the html for elitebabes.com"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def erosberry_parse(self) -> RipInfo:
        """Read the html for erosberry.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="title").text
        images = soup.find("div", class_="block-post three-post flex").find_all("a", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def everia_parse(self) -> RipInfo:
        """Read the html for everia.club"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find_all("div", class_="separator")
        images = [img.find("img").get("src") for img in images]
        return RipInfo(images, dir_name)

    def exgirlfriendmarket_parse(self) -> RipInfo:
        """Read the html for exgirlfriendmarket.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title-area").find("h1").text
        images = soup.find("div", class_="gallery").find_all("a", class_="thumb exo")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def f5girls_parse(self) -> RipInfo:
        """Read the html for f5girls.com"""
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
                self.driver.get(next_page)
                soup = self.soupify()
        return RipInfo(images, dir_name)

    # Started working on support for fantia.com
    def __fantia_parse(self) -> RipInfo:
        """Read the html for fantia.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="fanclub-name").find("a").text
        curr_url = self.driver.current_url
        self.driver.get("https://fantia.jp/sessions/signin")
        input("cont")
        self.driver.get(curr_url)
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
                self.driver.get(next_page)
                soup = self.soupify()
        images = []
        for post in post_list:
            self.driver.get(post)
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
            self._print_html(soup)
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
        return RipInfo(images, dir_name)

    def femjoyhunter_parse(self) -> RipInfo:
        """Read the html for femjoyhunter.com"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def foxhq_parse(self) -> RipInfo:
        """Read the html for foxhq.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1").text
        if dir_name is None:
            dir_name = soup.find("h2").text
        url = self.driver.current_url
        images = ["".join([url, td.find("a").get("href")]) for td in soup.find_all("td", align="center")[:-2]]
        return RipInfo(images, dir_name)

    def ftvhunter_parse(self) -> RipInfo:
        """Read the html for ftvhunter.com"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def girlsofdesire_parse(self) -> RipInfo:
        """Read the html for girlsofdesire.org"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("a", class_="albumName").text
        images = soup.find("div", id="gal_10").find_all("td", class_="vtop")
        images = ["".join(["https://girlsofdesire.org", img.find("img").get("src").replace("_thumb", "")]) for img in
                  images]
        return RipInfo(images, dir_name)

    def girlsreleased_parse(self) -> RipInfo:
        """Read the html for girlsreleased.com"""
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
        return RipInfo(images, dir_name)

    def glam0ur_parse(self) -> RipInfo:
        """Read the html for glam0ur.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="picnav").find("h1").text
        images = soup.find("div", class_="center").find_all("a", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        for i, img in enumerate(images):
            if "/banners/" in img:
                images.pop(i)
        return RipInfo(images, dir_name)

    def gofile_parse(self) -> RipInfo:
        """Read the html for gofile.io"""
        # Parses the html of the site
        sleep(5)
        current_url = self.driver.current_url
        soup = self.soupify()
        dir_name = soup.find("span", id="rowFolder-folderName").text
        images = soup.find("div", id="rowFolder-tableContent").find_all("div", recursive=False)
        images = [img.find("a", target="_blank").get("href") for img in images]
        images.insert(0, current_url)
        return RipInfo(images, dir_name)

    def grabpussy_parse(self) -> RipInfo:
        """Read the html for grabpussy.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find_all("div", class_="c-title")[1].find("h1").text
        images = soup.find("div", class_="gal own-gallery-images").find_all("a", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def gyrls_parse(self) -> RipInfo:
        """Read the html for gyrls.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="single_title").text
        images = soup.find("div", id="gallery-1").find_all("a")
        images = [img.get("href") for img in images]
        return RipInfo(images, dir_name)

    def hanime_parse(self) -> RipInfo:
        """Read the html for hanime.tv"""
        # Parses the html of the site
        sleep(1)  # Wait so images can load
        soup = self.soupify()
        dir_name = "Hanime Images"
        image_list = soup.find("div", class_="cuc_container images__content flex row wrap justify-center relative") \
            .find_all("a", recursive=False)
        images = [image.get("href") for image in image_list]
        return RipInfo(images, dir_name)

    def hegrehunter_parse(self) -> RipInfo:
        """Read the html for hegrehunter.com"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    # Cannot bypass captcha, so it doesn't work
    def __hentaicosplays_parse(self) -> RipInfo:
        """Read the html for hentai-cosplays.com"""
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
                self.driver.get(next_page)
                soup = self.soupify()
        return RipInfo(images, dir_name)

    def hentairox_parse(self) -> RipInfo:
        """Read the html for hentairox.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="col-md-7 col-sm-7 col-lg-8 right_details").find("h1").text
        images = soup.find("div", id="append_thumbs").find("img", class_="lazy preloader").get("data-src")
        num_files = int(soup.find("li", class_="pages").text.split()[0])
        return RipInfo([images], dir_name, True, num_files)

    def heymanhustle_parse(self) -> RipInfo:
        """Read the html for heymanhustle.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find("div", class_="galleria-thumbnails").find_all("img")
        images = [img.get("src").replace("/cache", "").split("-nggid")[0] for img in images]
        return RipInfo(images, dir_name)

    def hotgirl_parse(self) -> RipInfo:
        """Read the html for hotgirl.asia"""
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
                self.driver.get(page_url)
                soup = BeautifulSoup(self.driver.page_source, PARSER)
                images_list = soup.find_all("img", itemprop="image")
                del images_list[0]  # First image is just the thumbnail
                images_html.extend(images_list)
        images = [image.get("src") for image in images_html]
        return RipInfo(images, dir_name)

    def hotpornpics_parse(self) -> RipInfo:
        """Read the html for hotpornpics.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="hotpornpics_h1player").text
        images = soup.find("div", class_="hotpornpics_gallerybox").find_all("img")
        images = [img.get("src").replace("-square", "") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name)

    def hotstunners_parse(self) -> RipInfo:
        """Read the html for hotstunners.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title_content").find("h2").text
        images = soup.find("div", class_="gallery_janna2").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def hottystop_parse(self) -> RipInfo:
        """Read the html for hottystop.com"""
        # Parses the html of the site
        soup = self.soupify()
        url = self.driver.current_url
        try:
            dir_name = soup.find("div", class_="Box_Large_Content").find("h1").text
        except AttributeError:
            dir_name = soup.find("div", class_="Box_Large_Content").find("u").text
        image_list = soup.find("table").find_all("a")
        images = ["".join([url, image.get("href")]) for image in image_list]
        return RipInfo(images, dir_name)

    def hqbabes_parse(self) -> RipInfo:
        """Read the html for hqbabes.com"""
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
        return RipInfo(images, dir_name)

    def hqsluts_parse(self) -> RipInfo:
        """Read the html for hqsluts.com"""
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
        return RipInfo(images, dir_name)

    def hundredbucksbabes_parse(self) -> RipInfo:
        """Read the html for 100bucksbabes.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="main-col-2").find("h2", class_="heading").text
        images = soup.find("div", class_="main-thumbs").find_all("img")
        images = ["".join([PROTOCOL, img.get("data-url")]) for img in images]
        return RipInfo(images, dir_name)

    def imgbox_parse(self) -> RipInfo:
        """Read the html for imgbox.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="gallery-view").find("h1").text
        dir_name = dir_name.split(" - ")[0]
        images = soup.find("div", id="gallery-view-content").find_all("img")
        images = [img.get("src").replace("thumbs2", "images2").replace("_b", "_o") for img in images]
        return RipInfo(images, dir_name)

    def imgur_parse(self) -> RipInfo:
        """Read the html for imgur.com"""
        # Parses the html of the site
        client_id = read_config('KEYS', 'Imgur')
        if client_id == '':
            print("Client Id not properly set")
            print("Follow to generate Client Id: https://apidocs.imgur.com/#intro")
            print("Then add Client Id to imgur in config.ini under KEYS")
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
        return RipInfo(images, dir_name)

    def imhentai_parse(self) -> RipInfo:
        """Read the html for imhentai.xxx"""
        # Parses the html of the site
        soup = self.soupify()

        # Gets the image URL to be turned into the general image URL
        images = soup.find("img", class_="lazy preloader").get("data-src")

        # Gets the number of pages (images) in the album
        num_pages = soup.find("li", class_="pages")
        num_pages = int(num_pages.string.split()[1])
        dir_name = soup.find("h1").string

        # Removes illegal characters from folder name
        return RipInfo([images], dir_name, True, num_pages)

    def inven_parse(self) -> RipInfo:
        """Read the html for inven.co.kr"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="articleTitle").find("h1").text
        images = soup.find("div", id="BBSImageHolderTop").find_all("img")
        images = [img.get("src") for img in images]
        return RipInfo(images, dir_name)

    def jkforum_parse(self) -> RipInfo:
        """Read the html for jkforum.net"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="title-cont").find("h1").text
        images = soup.find("td", class_="t_f").find_all("img")
        images = [img.get("src") for img in images]
        return RipInfo(images, dir_name)

    def join2babes_parse(self) -> RipInfo:
        """Read the html for join2babes.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find_all("div", class_="gallery_title_div")[1].find("h1").text
        images = soup.find("div", class_="gthumbs").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def joymiihub_parse(self) -> RipInfo:
        """Read the html for joymiihub.com"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def jpg_parse(self) -> RipInfo:
        """Read the html for jpg.church"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="text-overflow-ellipsis").find("a").text
        images = soup.find("div", class_="pad-content-listing").find_all("img")
        images = [img.get("src").replace(".md", "") for img in images]
        return RipInfo(images, dir_name)

    def kemono_parse(self) -> RipInfo:
        """Read the html for kemono.party"""
        # Parses the html of the site
        return self.__dot_party_parse("https://kemono.party")

    def leakedbb_parse(self) -> RipInfo:
        """Read the html for leakedbb.com"""
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
            self.driver.get(link)
            soup = self.soupify()
            img = soup.find("a", id="download").get("href").split("?")[0]
            images.append(img)
        self.driver.quit()
        return RipInfo(images, dir_name)

    def livejasminbabes_parse(self) -> RipInfo:
        """Read the html for livejasminbabes.net"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="gallery_header").find("h1").text
        images = soup.find_all("div", class_="gallery_thumb")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def lovefap_parse(self) -> RipInfo:
        """Read the html for lovefap.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="albums-content-header").find("span").text
        images = soup.find("div", class_="files-wrapper noselect").find_all("a")
        images = [img.get("href") for img in images]
        vid = []
        for i, l in enumerate(images):
            if "/video/" in l:
                vid.append(i)
                self.driver.get("https://lovefap.com" + l)
                soup = self.soupify()
                images.append(soup.find("video", id="main-video").find("source").get("src"))
        for i in reversed(vid):
            images.pop(i)
        return RipInfo(images, dir_name)

    def luscious_parse(self) -> RipInfo:
        """Read the html for luscious.net"""
        # Parses the html of the site
        if "members." in self.driver.current_url:
            self.driver.get(self.driver.current_url.replace("members.", "www."))
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
        return RipInfo(images, dir_name)

    def mainbabes_parse(self) -> RipInfo:
        """Read the html for mainbabes.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="heading").find("h2", class_="title").text
        images = soup.find("div", class_="thumbs_box").find_all("div", class_="thumb_box")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def manganato_parse(self) -> RipInfo:
        """Read the html for manganato.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="story-info-right").find("h1").text
        next_chapter = soup.find("ul", class_="row-content-chapter").find_all("li", recursive=False)[-1].find("a")
        images = []
        counter = 1
        while next_chapter:
            print(f"Parsing Chapter {counter}")
            counter += 1
            self.driver.get(next_chapter.get("href"))
            soup = self.soupify()
            self._print_html(soup)
            chapter_images = soup.find("div", class_="container-chapter-reader").find_all("img")
            images.extend([img.get("src") for img in chapter_images])
            next_chapter = soup.find("a", class_="navi-change-chapter-btn-next a-h")
        return RipInfo(images, dir_name)

    global TEST_PARSER
    TEST_PARSER = "manganato_parse"

    def maturewoman_parse(self) -> RipInfo:
        """Read the html for maturewoman.xyz"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").text
        images = soup.find("div", class_="entry-content cf").find_all("img")
        images = [img.get("src") for img in images]
        self.driver.quit()
        return RipInfo(images, dir_name)

    def metarthunter_parse(self) -> RipInfo:
        """Read the html for hetarthunter.com"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def morazzia_parse(self) -> RipInfo:
        """Read the html for morazzia.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="title").text
        images = soup.find("div", class_="block-post album-item").find_all("a")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def myhentaigallery_parse(self) -> RipInfo:
        """Read the html for myhentaigallery.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="comic-description").find("h1").text
        images = soup.find("ul", class_="comics-grid clear").find_all("li")
        images = [img.find("img").get("src").replace("/thumbnail/", "/original/") for img in images]
        return RipInfo(images, dir_name)

    def nakedgirls_parse(self) -> RipInfo:
        """Read the html for nakedgirls.xxx"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="content").find("h1").text
        images = soup.find("div", class_="content").find_all("div", class_="thumb")
        images = ["".join(["https://www.nakedgirls.xxx", img.find("a").get("href")]) for img in images]
        return RipInfo(images, dir_name)

    def nightdreambabe_parse(self) -> RipInfo:
        """Read the html for nightdreambabe.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("section", class_="outer-section").find("h2", class_="section-title title").text
        images = soup.find("div", class_="lightgallery thumbs quadruple fivefold").find_all("a", class_="gallery-card")
        images = ["".join([PROTOCOL, img.find("img").get("src")]) for img in images]
        return RipInfo(images, dir_name)

    def nonsummerjack_parse(self) -> RipInfo:
        """Read the html for nonsummerjack.com"""
        # Parses the html of the site
        self.lazy_load(True, increment=1250, backscroll=1)
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
                self.lazy_load(True, increment=1250, backscroll=1)
                soup = self.soupify()
            image_list = soup.find("div", class_="foogallery foogallery-container "
                                                 "foogallery-justified foogallery-lightbox-foobox fg-justified "
                                                 "fg-light fg-shadow-outline fg-loading-default fg-loaded-fade-in "
                                                 "fg-caption-always fg-hover-fade fg-hover-zoom2 fg-ready "
                                                 "fbx-instance").find_all("a")
            image_list = [img.get("href") for img in image_list]
            images.extend(image_list)
        return RipInfo(images, dir_name)

    def novoglam_parse(self) -> RipInfo:
        """Read the html for novoglam.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="heading").find("h1").text
        images = soup.find("ul", id="myGalleryThumbs").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def novohot_parse(self) -> RipInfo:
        """Read the html for novohot.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="viewIMG").find("h1").text
        images = soup.find("div", class_="runout").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def novojoy_parse(self) -> RipInfo:
        """Read the html for novojoy.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1").text
        images = soup.find_all("img", class_="gallery-image")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def novoporn_parse(self) -> RipInfo:
        """Read the html for novoporn.com"""
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
        return RipInfo(images, dir_name)

    def nudebird_parse(self) -> RipInfo:
        """Read the html for nudebird.biz"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="title single-title entry-title").text
        images = soup.find_all("a", class_="fancybox-thumb")
        images = [img.get("href") for img in images]
        return RipInfo(images, dir_name)

    def nudity911_parse(self) -> RipInfo:
        """Read the html for nudity911.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1").text
        images = soup.find("tr", valign="top").find("td", align="center").find("table", width="650") \
            .find_all("td", width="33%")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def pbabes_parse(self) -> RipInfo:
        """Read the html for pbabes.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find_all("div", class_="box_654")[1].find("h1").text
        images = soup.find("div", style="margin-left:35px;").find_all("a", rel="nofollow")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    # Seems like all galleries have been deleted
    def _pics_parse(self) -> RipInfo:
        """Read the html for pics.vc"""
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
                self.driver.get(next_page)
                soup = self.soupify()
        return RipInfo(images, dir_name)

    def pinkfineart_parse(self) -> RipInfo:
        """Read the html for pinkfineart.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h5", class_="d-none d-sm-block text-center my-2")
        dir_name = "".join([t for t in dir_name.contents if type(t) == bs4.element.NavigableString])
        images = soup.find_all("div", class_="card ithumbnail-nobody ishadow ml-2 mb-3")
        images = ["".join(["https://pinkfineart.com", img.find("a").get("href")]) for img in images]
        return RipInfo(images, dir_name)

    def pleasuregirl_parse(self) -> RipInfo:
        """Read the html for pleasuregirl.net"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h2", class_="title").text
        images = soup.find("div", class_="lightgallery-wrap").find_all("div", class_="grid-item thumb")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def pmatehunter_parse(self) -> RipInfo:
        """Read the html for pmatehunter.com"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def porn3dx_parse(self) -> RipInfo:
        """Read the html for porn3dx.com"""
        # Parses the html of the site
        username = read_config("LOGINS", "Porn3dxU")
        password = read_config("LOGINS", "Porn3dxP")
        curr_url = self.driver.current_url
        logged_in_to_site = False
        if username and password:
            while True:
                try:
                    self.driver.find_element(By.XPATH, '//button[@class="btn btn-primary sign_in"]').click()
                    break
                except selenium.common.exceptions.NoSuchElementException:
                    sleep(1)
            self.driver.find_element(By.ID, "email").send_keys(username)
            self.driver.find_element(By.ID, "password").send_keys(password)
            self.driver.find_element(By.XPATH, '//button[@class="btn btn-action"]').click()
            while self.driver.current_url == curr_url:
                sleep(1)
            logged_in_to_site = True
            self.driver.get(curr_url)
        self.lazy_load(True)
        soup = self.soupify()
        dir_name = soup.find("div", class_="title-wrapper").find("h1").text
        posts = soup.find("div", class_="post-list post-list-popular-monthly position-relative") \
            .find_all("a", recursive=False)
        posts = [p.get("href") for p in posts]
        num_posts = len(posts)
        images = []
        for i, post in enumerate(posts):
            print("".join(["Parsing post ", str(i + 1), " of ", str(num_posts)]))
            self.driver.get(post)
            self.lazy_load(True)
            soup = self.soupify()
            media = soup.find("div", class_="content-wrapper")
            image_list = media.find_all("img")
            image_list = [img.get("data-full-url") for img in image_list]
            images.extend(image_list)
            if logged_in_to_site:
                links = soup.find_all("div", class_="tab download")
                links = [link.find_all("li")[-1].find("a").get("href") for link in links]
                images.extend(links)
            else:
                videos = media.find_all("div", class_="video-block")
                videos = [v.find("iframe").get("src") for v in videos]
                if videos:
                    for vid in videos:
                        self.driver.get(vid)
                        sleep(1)
                        soup = self.soupify()
                        self._print_html(soup)
                        v = soup.find("div", class_="plyr__video-wrapper").find("source").get("src")
                        images.append(v)
        self.driver.quit()
        return RipInfo(images, dir_name)

    # TODO: Site may be down permanently
    def putmega_parse(self) -> RipInfo:
        """Read the html for putmega.com"""
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
                self.driver.get(next_page)
                soup = self.soupify()
        self.driver.quit()
        return RipInfo(images, dir_name)

    def rabbitsfun_parse(self) -> RipInfo:
        """Read the html for rabbitsfun.com"""
        # Parses the html of the site
        sleep(1)
        soup = self.soupify()
        dir_name = soup.find("h3", class_="watch-mobTitle").text
        images = soup.find("div", class_="gallery-watch").find_all("li")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def redgifs_parse(self) -> RipInfo:
        """Read the html for redgifs.com"""
        # Parses the html of the site
        sleep(3)
        self.lazy_load(True, 1250)
        soup = self.soupify()
        dir_name = soup.find("div", class_="username").text
        images = []
        while True:
            wrapper = soup.find("div", class_="gif-tiles-wrapper")
            gif_list = wrapper.find_all("video")
            gif_list = [gif.find("source").get("src").replace("-mobile", "") for gif in gif_list]
            image_list = wrapper.find_all("img")
            image_list = [img.get("src").replace("-small", "-large") for img in image_list]
            images.extend(gif_list)
            images.extend(image_list)
            try:
                self.driver.find_element(By.XPATH, "//div[@class='pager']/div[@data-page='next']").click()
                self.lazy_load(True, 1250)
                soup = self.soupify()
            except selenium.common.exceptions.NoSuchElementException:
                break
        return RipInfo(images, dir_name)

    def redpornblog_parse(self) -> RipInfo:
        """Read the html for redpornblog.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="pic-title").find("h1").text
        images = soup.find("div", id="bigpic-image").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def rossoporn_parse(self) -> RipInfo:
        """Read the html for rossoporn.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="content_right").find("h1").text
        images = soup.find_all("div", class_="wrapper_g")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for tag_list in images for img in
                  tag_list.find_all("img")]
        return RipInfo(images, dir_name)

    def sankakucomplex_parse(self) -> RipInfo:
        """Read the html for sankakucomplex.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="entry-title").find("a").text
        images = soup.find_all("a", class_="swipebox")
        images = [img.get("href") if PROTOCOL in img.get("href") else "".join([PROTOCOL, img.get("href")]) for img in
                  images[1:]]
        return RipInfo(images, dir_name)

    def sensualgirls_parse(self) -> RipInfo:
        """Read the html for sensualgirls.org"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("a", class_="albumName").text
        images = soup.find("div", id="box_289").find_all("div", class_="gbox")
        images = ["".join(["https://sensualgirls.org", img.find("img").get("src").replace("_thumb", "")]) for img in
                  images]
        return RipInfo(images, dir_name)

    def sexhd_parse(self) -> RipInfo:
        """Read the html for sexhd.pics"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="photobig").find("h4").text.split(":")[1].strip()
        images = soup.find("div", class_="photobig").find_all("div", class_="relativetop")[1:]
        images = ["".join(["https://sexhd.pics", img.find("a").get("href")]) for img in images]
        return RipInfo(images, dir_name)

    def sexyaporno_parse(self) -> RipInfo:
        """Read the html for sexyaporno.com"""
        return self.__generic_html_parser_2()

    def sexybabesart_parse(self) -> RipInfo:
        """Read the html for sexybabesart.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="content-title").find("h1").text
        images = soup.find("div", class_="thumbs").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    # TODO: Convert to thotsbay parser since this site moved
    def __sexyegirls_parse(self) -> RipInfo:
        """Read the html for sexy-egirls.com"""
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
                    self.driver.get(next_page)
                    soup = self.soupify()
            for link in images:
                if any(p in link for p in parsable_links):
                    site_name = urlparse(link).netloc
                    parser: Callable[[], RipInfo] = self.parser_jump_table.get(site_name)
                    image_list = self.secondary_parse(link, parser)
                    images.extend(image_list)
                    images.remove(link)
        else:
            raise InvalidSubdomain
        return RipInfo(images, dir_name)

    def sexykittenporn_parse(self) -> RipInfo:
        """Read the html for sexykittenporn.com"""
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
            self.driver.get(link)
            soup = self.soupify()
            images.append("".join([PROTOCOL, soup.find(
                "div", class_="image-wrapper").find("img").get("src")]))
        return RipInfo(images, dir_name)

    def sexynakeds_parse(self) -> RipInfo:
        """Read the html for sexynakeds.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", class_="box").find_all("h1")[1].text
        images = soup.find("div", class_="post_tn").find_all("img")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def silkengirl_parse(self) -> RipInfo:
        """Read the html for silkengirl.com and silkengirl.net"""
        # Parses the html of the site
        soup = self.soupify()
        if ".com" in self.driver.current_url:
            dir_name = soup.find("h1", class_="title").text
        else:
            dir_name = soup.find("div", class_="content_main").find("h2").text
        images = soup.find_all("div", class_="thumb_box")
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def simplycosplay_parse(self) -> RipInfo:
        """Read the html for simply-cosplay.com"""
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
        return RipInfo(images, dir_name)

    def simplyporn_parse(self) -> RipInfo:
        """Read the html for simply-porn.com"""
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
        return RipInfo(images, dir_name)

    def sxchinesegirlz_parse(self) -> RipInfo:
        """Read the html for sxchinesegirlz.one"""
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
                self.driver.get("".join([curr_url, str(i + 1), "/"]))
                soup = self.soupify()
            image_list = soup.find("div", class_="thecontent").find_all("figure", class_="wp-block-image size-large",
                                                                        recursive=False)
            for img in image_list:
                img_url = img.find("img").get("src")
                if regp.search(img_url):  # Searches the img url for #x# and removes that to get full-scale image url
                    url_parts = img_url.split("-")
                    ext = "." + url_parts[-1].split(".")[-1]
                    img_url = "-".join(url_parts[:-1]) + ext
                images.append(img_url)
        return RipInfo(images, dir_name)

    def theomegaproject_parse(self) -> RipInfo:
        """Read the html for theomegaproject.org"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="omega").text
        images = soup.find("div", class_="postholder").find_all("div", class_="picture", recursive=False)
        images = ["".join([PROTOCOL, img.find("img").get("src").replace("tn_", "")]) for img in images]
        return RipInfo(images, dir_name)

    def thotsbay_parse(self) -> RipInfo:
        """Read the html for thotsbay.com"""
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
        for i, l in enumerate(images):
            if "/video/" in l:
                vid.append(i)
                self.driver.get(l)
                soup = self.soupify()
                images.append(soup.find("video", id="main-video").find("source").get("src"))
        for i in reversed(vid):
            images.pop(i)
        self.driver.quit()
        return RipInfo(images, dir_name)

    def tikhoe_parse(self) -> RipInfo:
        """Read the html for tikhoe.com"""
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
        return RipInfo(images, dir_name)

    # TODO: Complete this
    def _titsintops_parse(self) -> RipInfo:
        """Read the html for titsintops.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", class_="p-title-value").text
        images = soup.find()
        self.driver.quit()
        return RipInfo(images, dir_name)

    # TODO: Cert Date Invalid; Build Test Later
    def tuyangyan_parse(self) -> RipInfo:
        """Read the html for tuyangyan.com"""
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
                    self.driver.get(url)
                    soup = self.soupify()
                image_list = soup.find(
                    "div", class_="entry-content clearfix").find_all("img")
                images.extend(image_list)
        else:
            images = soup.find(
                "div", class_="entry-content clearfix").find_all("img")
        images = ["".join([PROTOCOL, img.get("src")]) for img in images]
        return RipInfo(images, dir_name)

    def wantedbabes_parse(self) -> RipInfo:
        """Read the html for wantedbabes.com"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("div", id="main-content").find("h1").text
        images = soup.find_all("div", class_="gallery")
        images = ["".join([PROTOCOL, img.get("src").replace("tn_", "")]) for im in images for img in im.find_all("img")]
        return RipInfo(images, dir_name)

    # TODO: Work on saving self.driver across sites to avoid relogging in
    def v2ph_parse(self) -> RipInfo:
        """Read the html for v2ph.com"""
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
        base_url = self.driver.current_url
        base_url = base_url.split("?")[0]
        images = []
        parse_complete = False
        for i in range(num_pages):
            if i != 0:
                next_page = "".join([base_url, "?page=", str(i + 1)])
                self.driver.get(next_page)
                if not logged_in:
                    curr_page = self.driver.current_url
                    self.driver.get("https://www.v2ph.com/login?hl=en")
                    while self.driver.current_url == "https://www.v2ph.com/login?hl=en":
                        sleep(0.1)
                    pickle.dump(self.driver.get_cookies(), open("cookies.pkl", "wb"))
                    self.driver.get(curr_page)
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
        return RipInfo(images, dir_name)

    def xarthunter_parse(self) -> RipInfo:
        """Read the html for xarthunter.com"""
        # Parses the html of the site
        return self.__generic_html_parser_1()

    def xmissy_parse(self) -> RipInfo:
        """Read the html for xmissy.nl"""
        # Parses the html of the site
        soup = self.soupify()
        dir_name = soup.find("h1", id="pagetitle").text
        images = soup.find("div", id="gallery").find_all("div", class_="noclick-image")
        images = [
            img.find("img").get("data-src") if img.find("img").get("data-src") is not None else img.find("img").get(
                "src")
            for img in images]
        return RipInfo(images, dir_name)

    def _test_parse(self, given_url: str) -> RipInfo:
        """Test the parser to see if it properly returns image URL(s), number of images, and folder name."""
        self.driver = None
        # ripper = ImageRipper()
        # ripper.rip(given_url)
        # return
        try:
            options = Options()
            options.headless = not DEBUG
            options.add_argument(DRIVER_HEADER)
            self.driver = webdriver.Firefox(options=options)
            self.driver.get(given_url.replace("members.", "www."))
            # rip = ImageRipper(given_url)
            # rip.site_login(self.driver)
            return eval(f"self.{TEST_PARSER}()")
        finally:
            self.driver.quit()

    def secondary_parse(self, link: str, parser: Callable[[webdriver.Firefox], RipInfo]) -> list[str] | str:
        """Parses the html for links for supported sites used in other sites"""
        curr = self.driver.current_url
        self.driver.get(link)
        images = parser(self.driver).urls
        self.driver.get(curr)
        return images

    @staticmethod
    def _print_html(soup: BeautifulSoup):
        with open("html.html", "w", encoding="utf-8") as f:
            f.write(str(soup))

    @staticmethod
    def _print_debug_info(title: str, *data, fd="output.txt", clear=False):
        with open(fd, "w" if clear else "a") as f:
            f.write(f"[{title}]\n")
            for d in data:
                f.write(f"\t{str(d).strip()}\n")

    # TODO: Merge the if/else
    def lazy_load(self, scroll_by: bool = False, increment: int = 2500, scroll_pause_time: float = 0.5,
                  backscroll: int = 0):
        """Load lazy loaded images by scrolling the page"""
        last_height = self.driver.execute_script("return window.pageYOffset")
        if scroll_by:
            while True:
                self.driver.execute_script(
                    "".join(["window.scrollBy({top: ", str(increment), ", left: 0, behavior: 'smooth'});"]))
                sleep(scroll_pause_time)
                new_height = self.driver.execute_script("return window.pageYOffset")
                if new_height == last_height:
                    if backscroll > 0:
                        for _ in range(backscroll):
                            self.driver.execute_script(
                                "".join(
                                    ["window.scrollBy({top: ", str(-increment), ", left: 0, behavior: 'smooth'});"]))
                            sleep(scroll_pause_time)
                        sleep(scroll_pause_time)
                    break
                last_height = new_height
        else:
            while True:
                self.driver.execute_script("window.scrollTo(0, document.body.scrollHeight);")
                sleep(scroll_pause_time)
                new_height = self.driver.execute_script("return document.body.scrollHeight")
                if new_height == last_height:
                    break
                last_height = new_height
        self.driver.implicitly_wait(10)

    @staticmethod
    def mark_as_failed(url: str):
        with open("failed.txt", "a") as f:
            f.write("".join([url, "\n"]))

    def soupify(self) -> BeautifulSoup:
        """Return BeautifulSoup object of html from driver"""
        html = self.driver.page_source
        return BeautifulSoup(html, PARSER)


if __name__ == "__main__":
    requests_header: dict[str, str] = {
        'User-Agent':
            'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36',
        'referer':
            'https://imhentai.xxx/',
        'cookie':
            ''
    }
    parser = HtmlParser(requests_header)
    print(parser._test_parse(sys.argv[1]))
