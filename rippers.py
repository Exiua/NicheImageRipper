"""This module downloads images from given URL"""
import hashlib
import os
from os import SEEK_END, path
import sys
import string
import configparser
import time
import functools
import subprocess
from pathlib import Path
from urllib.parse import urlparse
import PIL
from PIL import Image
import requests
from bs4 import BeautifulSoup
from selenium import webdriver
from selenium.webdriver.firefox.options import Options

class RipperError(Exception):
    """General Ripper Exceptions"""

class ImageRipper():
    """Image Ripper Class"""
    def __init__(self, given_url):
        self.folder_info = []
        self.given_url = given_url
        self.save_path = read_config('DEFAULT', 'SavePath')
        self.site_name = self.site_check()
        flag = 0x08000000  # No-Window flag
        webdriver.common.service.subprocess.Popen = functools.partial(
        subprocess.Popen, creationflags=flag)

    def image_getter(self):
        """Download images from URL."""
        self.folder_info = self.html_parse() #Gets image url, number of images, and name of album
        full_path = "".join([self.save_path, self.folder_info[2]]) #Save location of this album
        Path(full_path).mkdir(parents=True, exist_ok=True) #Checks if the dir path of this album exists
        headers = {
            "User-Agent":
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36"
        }
        session = requests.Session()
        session.headers.update(headers)
        #Can get the image through numerically acending url for these sites
        if self.site_name in ("imhentai", "hentaicafe", "bustybloom", "morazzia", "novojoy", "silkengirl", "babesandgirls", "100bucksbabes",
                            "babesbang", "exgirlfriendmarket", "novoporn", "babeuniversum"):
            trimmed_url = trim_url(self.folder_info[0]) #Gets the general url of all images in this album
            for index in range(1, int(self.folder_info[1]) + 1): #Downloads all images from the general url
                num = index
                if not self.site_name in ("imhentai", "hentaicafe"): #All other sites start from 00
                    num -= 1
                if self.site_name != "imhentai" and num < 10: #All other sites use 00 styling for single digit urls
                    file_num = "".join(["0", str(num)]) #Appends a 0 to numbers less than 10
                else:
                    file_num = str(num)
                try:
                    #Most images will be .jpg
                    download_from_url(session, trimmed_url, file_num, full_path, self.folder_info[1], ".jpg")
                except PIL.UnidentifiedImageError:
                    try:
                        os.remove("".join([full_path, "/pic1.jpg"]))
                        #Check if .gif
                        download_from_url(session, trimmed_url, file_num, full_path, self.folder_info[1], ".gif")
                    except PIL.UnidentifiedImageError:
                        try:
                            os.remove("".join([full_path, "/pic1.gif"]))
                            #Check if .png
                            download_from_url(session, trimmed_url, file_num, full_path, self.folder_info[1], ".png")
                        except PIL.UnidentifiedImageError:
                            try:
                                os.remove("".join([full_path, "/pic1.png"]))
                                #If all fails, download thumbnail
                                download_from_url(session, trimmed_url, file_num + "t", full_path, self.folder_info[1], ".jpg")
                            except PIL.UnidentifiedImageError:
                                os.remove("".join([full_path, "/pic1.jpg"]))
                                pass #No image exists, probably
                except OSError:
                    pass
        #Easier to put all image url in a list and then download for these sites
        elif self.site_name in ("hotgirl", "cup-e", "girlsreleased", "hqbabes", "babeimpact", "sexykittenporn", "hottystop"):
            for index in range(int(self.folder_info[1])):
                try:
                    download_from_list(session, self.folder_info[0][index], full_path, index, self.folder_info[1])
                except PIL.UnidentifiedImageError:
                    pass #No image exists, probably
        print("Download Complete")

    def html_parse(self):
        """Return image URL, number of images, and folder name."""
        options = Options()
        options.headless = True
        options.add_argument = ("user-agent=Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z‡ Safari/537.36")
        driver = webdriver.Firefox(options=options)
        driver.get(self.given_url)
        html = driver.page_source
        soup = BeautifulSoup(html, "html.parser")
        try:
            if self.site_name == "imhentai":
                site_info = imhentai_parse(soup, driver)
            elif self.site_name == "hotgirl":
                site_info = hotgirl_parse(soup, driver, self.given_url)
            elif self.site_name == "hentaicafe":
                site_info = hentaicafe_parse(soup, driver, self.given_url)
            elif self.site_name == "cup-e":
                site_info = cupe_parse(soup, driver)
            elif self.site_name == "girlsreleased":
                site_info = girlsreleased_parse(soup, driver)
            elif self.site_name == "bustybloom":
                site_info = bustybloom_parse(soup, driver)
            elif self.site_name == "morazzia":
                site_info = morazzia_parse(soup, driver)
            elif self.site_name == "novojoy":
                site_info = novojoy_parse(soup, driver)
            elif self.site_name == "hqbabes":
                site_info = hqbabes_parse(soup, driver)
            elif self.site_name == "silkengirl":
                site_info = silkengirl_parse(soup, driver)
            elif self.site_name == "babesandgirls":
                site_info = babesandgirls_parse(soup, driver)
            elif self.site_name == "babeimpact":
                site_info = babeimpact_parse(soup, driver)
            elif self.site_name == "100bucksbabes":
                site_info = hundredbucksbabes_parse(soup, driver)
            elif self.site_name == "sexykittenporn":
                site_info = sexykittenporn_parse(soup, driver)
            elif self.site_name == "babesbang":
                site_info = babesbang_parse(soup, driver)
            elif self.site_name == "exgirlfriendmarket":
                site_info = exgirlfriendmarket_parse(soup, driver)
            elif self.site_name == "novoporn":
                site_info = novoporn_parse(soup, driver)
            elif self.site_name == "hottystop":
                site_info = hottystop_parse(soup, driver, self.given_url)
            elif self.site_name == "babeuniversum":
                site_info = babeuniversum_parse(soup, driver)
        except UnboundLocalError:
            raise RipperError("Not a supported site")
        finally:
            driver.quit()
            return site_info # pyright: reportUnboundVariable=false

    def site_check(self):
        """Check which site the url is from"""
        if url_check(self.given_url):
            if "https://imhentai.com/" in self.given_url:
                return "imhentai"
            if "https://hotgirl.asia/" in self.given_url:
                return "hotgirl"
            if "https://hentai.cafe/" in self.given_url:
                return "hentaicafe"
            if "https://www.cup-e.club/" in self.given_url:
                return "cup-e"
            if "https://girlsreleased.com/" in self.given_url:
                return "girlsreleased"
            if "https://www.bustybloom.com/" in self.given_url:
                return "bustybloom"
            if "https://www.morazzia.com/" in self.given_url:
                return "morazzia"
            if "https://www.novojoy.com/" in self.given_url:
                return "novojoy"
            if "https://www.hqbabes.com/" in self.given_url:
                return "hqbabes"
            if "https://www.silkengirl.com/" in self.given_url:
                return "silkengirl"
            if "https://www.babesandgirls.com/" in self.given_url:
                return "babesandgirls"
            if "https://www.babeimpact.com/" in self.given_url:
                return "babeimpact"
            if "https://www.100bucksbabes.com/" in self.given_url:
                return "100bucksbabes"
            if "https://www.sexykittenporn.com/" in self.given_url:
                return "sexykittenporn"
            if "https://www.babesbang.com/" in self.given_url:
                return "babesbang"
            if "https://www.exgirlfriendmarket.com/" in self.given_url:
                return "exgirlfriendmarket"
            if "https://www.novoporn.com/" in self.given_url:
                return "novoporn"
            if "https://www.hottystop.com/" in self.given_url:
                return "hottystop"
            if "https://www.babeuniversum.com/" in self.given_url:
                return "babeuniversum"
        raise RipperError("Not a support site")

def imhentai_parse(soup, driver):
    """Read the html for imhentai.com"""
    #Gets the image URL to be turned into the general image URL
    images = soup.find("img", class_="lazy preloader").get("data-src")
    #Gets the number of pages (images) in the album
    num_pages = soup.find("li", class_="pages")
    num_pages = num_pages.string.split()[1]
    dir_name = soup.find("h1").string
    #Removes illegal characters from folder name
    dir_name = clean_dir_name(dir_name)
    driver.quit()
    return [images, num_pages, dir_name]

def hotgirl_parse(soup, driver, url):
    """Read the html for hotgirl.asia"""
    #Gets the number of pages
    num_pages = soup.findAll("a", class_="page larger")
    if len(num_pages) > 0:
        num_pages = num_pages.pop().text
    else:
        num_pages = 1
    #Gets the folder name
    dir_name = soup.find("h3").text
    #Removes illegal characters from folder name
    dir_name = clean_dir_name(dir_name)
    images_html = soup.find_all('img', itemprop="image")
    del images_html[0]
    if int(num_pages) > 1:
        for index in range(2, int(num_pages) + 1):
            page_url = "".join([url, str(index), '/'])
            driver.get(page_url)
            soup = BeautifulSoup(driver.page_source, "html.parser")
            images_list = soup.find_all("img", itemprop="image")
            del images_list[0]
            images_html.extend(images_list)
    images = [image.get("src") for image in images_html]
    num_files = len(images)
    driver.quit()
    return [images, num_files, dir_name]

def hentaicafe_parse(soup, driver, url):
    """Read the html for hentai.cafe"""
    if "hc.fyi" in url:
        dir_name = soup.find("h3").text
        dir_name = clean_dir_name(dir_name)
        images = soup.find("a", class_="x-btn x-btn-flat x-btn-rounded x-btn-large").get("href")
        driver.get(images)
        html = driver.page_source
        soup = BeautifulSoup(html, "html.parser")
    else:
        start = url.find('/read/') + 6
        end = url.find('/en/', start)
        dir_name = url[start:end].replace("-", " ")   
        dir_name = string.capwords(dir_name)
        dir_name = clean_dir_name(dir_name)
    images = soup.find("img", class_="open").get("src")
    num_files = soup.find("div", class_="text").string.split()[0]
    return [images, num_files, dir_name]

def cupe_parse(soup, driver):
    """Read the html for hentai.cafe"""
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
    album_info = soup.find_all("p")[2].text
    album_info = album_info.split()
    shoot_theme = []
    model_index = 0
    theme_found = False
    if "Concept" in album_info:
        for index in range(len(album_info)):
            if theme_found:
                if not album_info[index] == "Model":
                    shoot_theme.append(album_info[index])
                else:
                    model_index = index + 2
                    shoot_theme = " ".join(shoot_theme).replace(":", "").strip()
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
    return [images, num_files, dir_name]

def girlsreleased_parse(soup, driver):
    """Read the html for girlsreleased.com"""
    set_name = soup.find("a", id="set_name").text
    model_name = soup.find_all("a", class_="button link")[1]
    model_name = model_name.find("span", recursive=False).text
    model_name = "".join(["[", model_name, "]"])
    dir_name = " ".join([set_name, model_name])
    dir_name = clean_dir_name(dir_name)
    images_source = soup.find_all("a", target="imageView")
    images_url = [image.get("href") for image in images_source]
    images = []
    headers = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.141 Safari/537.36'}
    session = requests.Session()
    for url in images_url:
        response = session.get(url, stream=True, headers=headers)
        html = response.text
        soup = BeautifulSoup(html, "html.parser")
        try:
            image = soup.find("img", class_="pic img img-responsive").get("src")
            images.append(image)
        except AttributeError:
            pass #Image may have been deleted from ImageTwist servers
    num_files = len(images)
    driver.quit()
    return [images, num_files, dir_name]

def bustybloom_parse(soup, driver):
    """Read the html for bustybloom.com"""
    num_files = len(soup.find_all("div", class_="gallery_thumb"))
    dir_name = soup.find("img", title="Click To Enlarge!").get("alt").split()
    for i in range(len(dir_name)):
        if dir_name[i] == '-':
            del dir_name[i::]
            break
    dir_name = " ".join(dir_name)
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="gallery_thumb").find("a").get("href")
    driver.get("".join(["https://www.bustybloom.com", images]))
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    images = soup.find("div", class_="picture_thumb").find("img").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def morazzia_parse(soup, driver):
    """Read the html for morazzia.com"""
    dir_name = soup.find("h1", class_="title").text
    dir_name = clean_dir_name(dir_name)
    num_files = soup.find("div", class_="block-post album-item").find_all("a")
    num_files = len(num_files)
    images = soup.find("div", class_="block-post album-item").find("a").get("href")
    driver.get("".join(["https://www.morazzia.com", images]))
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    try:
        images = soup.find("p", align="center").find("img").get("src")
    except AttributeError:
        images = soup.find("a", class_="main-post item-post w-100").find("img").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def novojoy_parse(soup, driver):
    """Read the html for novojoy.com"""
    dir_name = soup.find("h1").text
    dir_name = clean_dir_name(dir_name)
    num_files = soup.find_all("a", class_="gallery-thumb")
    num_files = len(num_files)
    images = soup.find("a", class_="gallery-thumb").get("href")
    images = "".join(["https://novojoy.com", images])
    driver.get(images)
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    images = soup.find("div", class_="bigpic").find("img").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def hqbabes_parse(soup, driver):
    """Read the html for hqbabes.com"""
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
            images.append("".join(["https:", image_url]))
    num_files = len(images)
    driver.quit()
    return [images, num_files, dir_name]

def silkengirl_parse(soup, driver):
    """Read the html for silkengirl.com"""
    dir_name = soup.find("h1", class_="title").text
    dir_name = clean_dir_name(dir_name)
    num_files = soup.find_all("div", class_="thumb_box")
    num_files = len(num_files)
    images = soup.find("div", class_="thumb_box").find("a").get("href")
    images = "".join(["https://silkengirl.com", images])
    driver.get(images)
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    images = soup.find("div", class_="wrap").find("img").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def babesandgirls_parse(soup, driver):
    """Read the html for babesandgirls.com"""
    dir_name = soup.find("h1", class_="title").text
    dir_name = clean_dir_name(dir_name)
    num_files = soup.find("div", class_="block-post album-item").find_all("a", class_="item-post")
    num_files = len(num_files)
    images = soup.find("div", class_="block-post album-item").find("a", class_="item-post").get("href")
    images = "".join(["https://www.babesandgirls.com", images])
    driver.get(images)
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    images = soup.find("div", class_="main-post item-post w-100").find("img").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def babeimpact_parse(soup, driver):
    """Read the html for babeimpact.com"""
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
        soup = BeautifulSoup(html, "html.parser")
        images.append("".join(["https:", soup.find("div", class_="image-wrapper").find("img").get("src")]))
    driver.quit()
    return [images, num_files, dir_name]

def hundredbucksbabes_parse(soup, driver):
    """Read the html for 100bucksbabes.com"""
    dir_name = soup.find("div", class_="fl").find("h1").text
    dir_name = clean_dir_name(dir_name)
    num_files = soup.find("div", class_="galleryblock").find_all("a")
    num_files = len(num_files)
    images = soup.find("div", class_="galleryblock").find("a").get("href")
    images = "".join(["https://www.100bucksbabes.com", images])
    driver.get(images)
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    images = soup.find("div", class_="imageblock").find("img").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def sexykittenporn_parse(soup, driver):
    """Read the html for sexykittenporn.com"""
    dir_name = soup.find("h1", class_="blockheader").text
    dir_name = clean_dir_name(dir_name)
    tag_list = soup.find_all("div", class_="list gallery col3")
    image_list = []
    for tag in tag_list:
        image_list.extend(tag.find_all("div", class_="item"))
    num_files = len(image_list)
    image_link = ["".join(["https://www.sexykittenporn.com", image.find("a").get("href")]) for image in image_list]
    images = []
    for link in image_link:
        driver.get(link)
        html = driver.page_source
        soup = BeautifulSoup(html, "html.parser")
        images.append("".join(["https:", soup.find("div", class_="image-wrapper").find("img").get("src")]))
    driver.quit()
    return [images, num_files, dir_name]

def babesbang_parse(soup, driver):
    """Read the html for babesbang.com"""
    dir_name = soup.find("div", class_="main-title").text
    dir_name = clean_dir_name(dir_name)
    tag_list = soup.find_all("div", class_="gal-block")
    image_list = []
    for tag in tag_list:
        image_list.extend(tag.find_all("a"))
    num_files = len(image_list)
    images = "".join(["https://www.babesbang.com", image_list[0].get("href")])
    driver.get(images)
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    images = soup.find("img", style="max-width:620px").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def exgirlfriendmarket_parse(soup, driver):
    """Read the html for exgirlfriendmarket.com"""
    num_files = len(soup.find_all("div", class_="gallery_thumb"))
    dir_name = soup.find("img", title="Click To Enlarge!").get("alt").split()
    for i in range(len(dir_name)):
        if dir_name[i] == '-':
            del dir_name[i::]
            break
    dir_name = " ".join(dir_name)
    dir_name = clean_dir_name(dir_name)
    images = soup.find("div", class_="gallery_thumb").find("a").get("href")
    driver.get("".join(["https://www.exgirlfriendmarket.com", images]))
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    images = soup.find("div", class_="gallery_thumb").find("img").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def novoporn_parse(soup, driver):
    """Read the html for novoporn.com"""
    dir_name = soup.find("div", class_="gallery").find("h1").text.split()
    for i, word in enumerate(dir_name):
        if word == "porn":
            del dir_name[i::]
            break
    dir_name = clean_dir_name(" ".join(dir_name))
    num_files = len(soup.find_all("img", class_="gallerythumbs"))
    images = soup.find("div", class_="gallery").find("a", rel="nofollow").get("href")
    images = "".join(["https://novoporn.com", images])
    driver.get(images)
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    images = soup.find("div", id="picture-holder").find("img").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def hottystop_parse(soup, driver, url):
    """Read the html for hottystop.com"""
    try:
        dir_name = soup.find("div", class_="Box_Large_Content").find("h1").text
    except AttributeError:
        dir_name = soup.find("div", class_="Box_Large_Content").find("u").text
    dir_name = clean_dir_name(dir_name)
    image_list = soup.find("table").find_all("a")
    images = ["".join([url, image.get("href")]) for image in image_list]
    num_files = len(images)
    driver.quit()
    return [images, num_files, dir_name]

def babeuniversum_parse(soup, driver):
    """Read the html for babeuniversum.com"""
    dir_name = soup.find("div", class_="title").find("h1").text
    dir_name = clean_dir_name(dir_name)
    num_files = soup.find("div", class_="three-column").find_all("div", class_="thumbnail")
    images = num_files[0].find("a").get("href")
    images = "".join(["https://www.babeuniversum.com", images])
    num_files = len(num_files)
    driver.get(images)
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    images = soup.find_all("div", class_="one-column")[1].find("img").get("src")
    images = "".join(["https:", images])
    driver.quit()
    return [images, num_files, dir_name]

def test_parse(given_url):
    """Return image URL, number of images, and folder name."""
    driver = None
    try:
        options = Options()
        options.headless = True
        options.add_argument = ("user-agent=Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z‡ Safari/537.36")
        driver = webdriver.Firefox(options=options)
        driver.get(given_url)
        html = driver.page_source
        soup = BeautifulSoup(html, "html.parser")
        return babeuniversum_parse(soup, driver)
    finally:
        driver.quit()

def download_from_url(session, url_name, file_name, full_path, num_files, ext):
    """"Download image from image url"""
    #Completes the specific image URL from the general URL
    rip_url = "".join([url_name, str(file_name), ext])
    num_progress = "".join(["(", file_name, "/", str(num_files), ")"])
    print(" ".join([rip_url, "   ", num_progress]))
    image_url = "".join([full_path, "/pic1", ext])
    with open(image_url, "wb") as handle:
        response = session.get(rip_url, stream=True)
        if not response.ok:
            print(response)
        if ext == ".jpg":
            for block in response.iter_content(1024):
                if not block:
                    break
                handle.write(block)
        elif ext == ".gif":
            handle.write(response.content)
    #md5 hash is used as image name to avoid duplicate names
    md5hash = hashlib.md5(Image.open(image_url).tobytes())
    hash5 = md5hash.hexdigest()
    image_hash_name = "".join([full_path, "/", hash5, ext])
    if os.path.exists(image_hash_name): #If duplicate exists, remove the duplicate
        os.remove(image_url)
    else:
        #Otherwise, rename the image with the md5 hash
        os.rename(image_url, image_hash_name)
    time.sleep(0.05)

def download_from_list(session, given_url, full_path, current_file_num, num_files):
    """Download images from hotgirl.asia"""
    rip_url = given_url.strip('\n')
    num_progress = "".join(["(", str(current_file_num + 1), "/", str(num_files), ")"])
    print(" ".join([rip_url, "   ", num_progress]))
    file_name = os.path.basename(urlparse(rip_url).path)
    with open("".join([full_path, '/', file_name]), "wb") as handle:
        try: 
            response = session.get(rip_url, stream=True)
            if not response.ok:
                print(response)
            for block in response.iter_content(1024):
                if not block:
                    break
                handle.write(block)
        except requests.exceptions.ConnectionError:
            options = Options()
            options.headless = True
            options.add_argument = ("user-agent=Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z‡ Safari/537.36")
            driver = webdriver.Firefox(options=options)
            response = driver.get(rip_url)
            if not response.ok:
                print(response)
            for block in response.iter_content(1024):
                if not block:
                    break
                handle.write(block)
    time.sleep(0.05)

def clean_dir_name(given_name):
    """Remove illeage characters from name"""
    translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*'), None)
    return given_name.translate(translation_table).strip()

def trim_url(given_url):
    """Return the URL without the filename attached."""
    file_ext = [".jpg", ".png", ".jpeg", ".gif"]
    if any(x in given_url for x in file_ext):
        given_url = "".join([str("/".join(given_url.split("/")[0:-1])), "/"])
    return given_url

def read_config(header, child):
    """Read from config.ini"""
    config = configparser.ConfigParser()
    config.read('config.ini')
    if not path.isfile('config.ini'):
        config['DEFAULT'] = {}
        config['DEFAULT']['SavePath'] = 'Rips/'
        config['DEFAULT']['Theme'] = 'Dark'
        config['DEFAULT']['AskToReRip'] = 'True'
        config['DEFAULT']['NumberOfThreads'] = 1
        with open('config.ini', 'w') as configfile:    # save
            config.write(configfile)
    return config.get(header, child)

def write_config(header, child, change):
    """Write to config.ini"""
    config = configparser.ConfigParser()
    config.read('config.ini')
    config[header][child] = change
    with open('config.ini', 'w') as configfile:    # save
        config.write(configfile)

def url_check(given_url):
    """Check the url to make sure it is from valid site"""
    sites = ["https://imhentai.com/", "https://hotgirl.asia/", "https://hentai.cafe/", 
            "https://www.cup-e.club/", "https://girlsreleased.com/", "https://www.bustybloom.com/", 
            "https://www.morazzia.com/", "https://www.novojoy.com/", "https://www.hqbabes.com/",
            "https://www.silkengirl.com/", "https://www.babesandgirls.com/", "https://www.babeimpact.com/",
            "https://www.100bucksbabes.com/", "https://www.sexykittenporn.com/", "https://www.babesbang.com/",
            "https://www.exgirlfriendmarket.com/", "https://www.novoporn.com/", "https://www.hottystop.com/",
            "https://www.babeuniversum.com/"]
    return any(x in given_url for x in sites)

if __name__ == "__main__":
    if len(sys.argv) > 1:
        album_url = sys.argv[1]
    else:
        raise Exception("Script requires a link as an argument")
    #image_ripper = ImageRipper(sys.argv[1])
    #image_ripper.image_getter()
    print(test_parse(sys.argv[1]))
    #print(trim_url(sys.argv[1], True))
