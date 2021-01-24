"""This module downloads images from given URL""" # pylint: disable=invalid-name
import hashlib
import os
from os import path
import sys
import configparser
import re
from pathlib import Path
from urllib.parse import urlparse
import PIL
from PIL import Image
import requests
from bs4 import BeautifulSoup
from selenium import webdriver
from selenium.webdriver.firefox.options import Options

# pylint: disable=line-too-long
class RipperError(Exception):
    """General Ripper Exceptions"""

class ImageRipper():
    """Image Ripper Class"""
    def __init__(self, given_url):
        self.folder_info = []
        self.given_url = given_url
        self.save_path = read_config('DEFAULT', 'SavePath')
        self.site_name = self.site_check()

    def image_getter(self):
        """Download images from URL."""
        self.folder_info = self.html_parse() #Gets image url, number of images, and name of album
        full_path = self.save_path + self.folder_info[2] #Save location of this album
        Path(full_path).mkdir(parents=True, exist_ok=True) #Checks if the dir path of this album exists
        if self.site_name in ("imhentai", "hentaicafe"):
            trimmed_url = trim_url(self.folder_info[0]) #Gets the general url of all images in this album
            for index in range(1, int(self.folder_info[1]) + 1): #Downloads all images from the general url
                if self.site_name == "hentaicafe" and index < 10:
                    file_num = "0" + str(index)
                else:
                    file_num = str(index)
                try:
                    #Most images will be .jpg
                    download_from_url(trimmed_url, file_num, full_path, ".jpg")
                except PIL.UnidentifiedImageError:
                    try:
                        os.remove(full_path + "/pic1.jpg")
                        #Check if .gif
                        download_from_url(trimmed_url, file_num, full_path, ".gif")
                    except PIL.UnidentifiedImageError:
                        try:
                            os.remove(full_path + "/pic1.gif")
                            #Check if .png
                            download_from_url(trimmed_url, file_num, full_path, ".png")
                        except PIL.UnidentifiedImageError:
                            try:
                                os.remove(full_path + "/pic1.png")
                                #If all fails, download thumbnail
                                download_from_url(trimmed_url, file_num + "t", full_path, ".jpg")
                            except PIL.UnidentifiedImageError:
                                pass
                except OSError:
                    pass
        elif self.site_name in ("hotgirl", "cup-e", "girlsreleased"):
            for index in range(int(self.folder_info[1])):
                try:
                    download_from_list(self.folder_info[0][index], full_path)
                except PIL.UnidentifiedImageError:
                    pass
        print("Download Complete")

    def html_parse(self):
        """Return image URL, number of images, and folder name."""
        options = Options()
        options.headless = True
        options.add_argument = ("user-agent=Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z‡ Safari/537.36") # pylint: disable=line-too-long
        driver = webdriver.Firefox(options=options)
        driver.get(self.given_url)
        html = driver.page_source
        soup = BeautifulSoup(html, "html.parser")
        if self.site_name == "imhentai":
            site_info = imhentai_parse(soup, driver)
            driver.quit()
            return site_info
        if self.site_name == "hotgirl":
            site_info = hotgirl_parse(soup, driver, self.given_url)
            driver.quit()
            return site_info
        if self.site_name == "hentaicafe":
            site_info = hentaicafe_parse(soup, driver, self.given_url)
            driver.quit()
            return site_info
        if self.site_name == "cup-e":
            site_info = cupe_parse(soup, driver)
            driver.quit()
            return site_info
        if self.site_name == "girlsreleased":
            site_info = girlsreleased_parse(soup, driver)
            driver.quit()
            return site_info
        raise RipperError("Not a supported site")

    def site_check(self):
        """Check which site the url is from"""
        if url_check(self.given_url):
            if "imhentai.com" in self.given_url:
                return "imhentai"
            if "hotgirl.asia" in self.given_url:
                return "hotgirl"
            if "hentai.cafe" in self.given_url:
                return "hentaicafe"
            if "cup-e.club" in self.given_url:
                return "cup-e"
            if "girlsreleased" in self.given_url:
                return "girlsreleased"
        raise RipperError("Not a support site")

def imhentai_parse(soup, driver):
    """Read the html for imhentai.com"""
    #Gets the image URL to be turned into the general image URL
    images = soup.find("img", class_="lazy preloader").get("data-src")
    #Gets the number of pages (images) in the album
    num_pages = soup.find("li", class_="pages")
    num_pages = num_pages.string.split()[1]
    #print(num_pages)
    dir_name = soup.find("h1").string
    #Removes illegal characters from folder name
    translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*'), None)
    dir_name = dir_name.translate(translation_table)
    #print(images)
    #print(dir_name)
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
    translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*'), None)
    dir_name = dir_name.translate(translation_table)
    images_html = soup.find_all('img', itemprop="image")
    del images_html[0]
    if int(num_pages) > 1:
        for index in range(2, int(num_pages) + 1):
            page_url = url + str(index) + '/'
            driver.get(page_url)
            soup = BeautifulSoup(driver.page_source, "html.parser")
            images_list = soup.find_all("img", itemprop="image")
            del images_list[0]
            images_html.extend(images_list)
    images = []
    for index in range(len(images_html)): # pylint: disable=consider-using-enumerate
        images.append(images_html[index].get("src"))
    num_files = len(images)
    driver.quit()
    return [images, num_files, dir_name]

def hentaicafe_parse(soup, driver, url):
    """Read the html for hentai.cafe"""
    if "hc.fyi" in url:
        dir_name = soup.find("h3").text
        translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*'), None)
        dir_name = dir_name.translate(translation_table)
        images = soup.find("a", class_="x-btn x-btn-flat x-btn-rounded x-btn-large").get("href")
        driver.get(images)
        html = driver.page_source
        soup = BeautifulSoup(html, "html.parser")
    else:
        start = url.find('/read/') + 6
        end = url.find('/en/', start)
        dir_name = url[start:end].replace("-", " ")
    images = soup.find("img", class_="open").get("src")
    num_files = soup.find("div", class_="text").string.split()[0]
    return [images, num_files, dir_name]

def cupe_parse(soup, driver):
    """Read the html for hentai.cafe"""
    image_list = soup.find_all("img", ["alignnone", "size-full"])
    del image_list[0]
    images = []
    for image in image_list:
        images.append(image.get("src"))
    if len(images) == 0:
        image_list = soup.find_all("a", class_="ngg-fancybox")
    for image in image_list:
        images.append(image.get("data-src"))
    if len(images) == 0:
        soup.find_all("img", class_="attachment-full size-full wp-post-image")
    for image in image_list:
        images.append(image.get("src"))
    images = [x for x in images if x is not None]
    num_files = len(images)
    album_title = soup.find("h1", class_="entry-title").text
    album_info = soup.find_all("p")[2].text
    album_info = album_info.split()
    shoot_theme = ""
    model_index = 0
    theme_found = False
    for index in range(len(album_info)):
        if theme_found:
            if not album_info[index] == "Model":
                shoot_theme += " " + album_info[index]
            else:
                model_index = index + 2
                break
        elif album_info[index] == "Concept":
            index += 2
            theme_found = True
    model_name = ""
    for index in range(model_index, len(album_info)):
        if not album_info[index] == "Photographer":
            if index != model_index:
                model_name += " "
            model_name += album_info[index]
        else:
            break
    dir_name = "(Cup E) " + album_title + " -" + shoot_theme + " [" + model_name + "]"
    translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*'), None)
    dir_name = dir_name.translate(translation_table)
    driver.quit()
    return [images, num_files, dir_name]

def girlsreleased_parse(soup, driver):
    """Read the html for girlsreleased.com"""
    set_name = soup.find("a", id="set_name").text
    model_name = soup.find_all("a", class_="button link")[1]
    model_name = model_name.find("span", recursive=False).text
    dir_name = "" + set_name + " [" + model_name + "]"
    translation_table = dict.fromkeys(map(ord, '<>:"/\\|?*'), None)
    dir_name = dir_name.translate(translation_table)
    images_source = soup.find_all("a", target="imageView")
    images_url = []
    images = []
    for image in images_source:
        images_url.append(image.get("href"))
    for url in images_url:
        headers = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.141 Safari/537.36'}
        response = requests.get(url, stream=True, headers=headers)
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

def test_parse(given_url):
    """Return image URL, number of images, and folder name."""
    options = Options()
    options.headless = True
    options.add_argument = ("user-agent=Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z‡ Safari/537.36") # pylint: disable=line-too-long
    driver = webdriver.Firefox(options=options)
    driver.get(given_url)
    html = driver.page_source
    soup = BeautifulSoup(html, "html.parser")
    return cupe_parse(soup, driver)

def download_from_url(url_name, file_name, full_path, ext):
    """"Download specific image from imhentai"""
    #Completes the specific image URL from the general URL
    rip_url = url_name + str(file_name) + ext
    print(rip_url)
    with open(full_path + "/pic1" + ext, "wb") as handle:
        response = requests.get(rip_url, stream=True)
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
    md5hash = hashlib.md5(Image.open(full_path + "/pic1" + ext).tobytes())
    hash5 = md5hash.hexdigest()
    if os.path.exists(full_path + "/" + hash5 + ext): #If duplicate exists, remove the duplicate
        os.remove(full_path + "/pic1" + ext)
    else:
        #Otherwise, rename the image with the md5 hash
        os.rename(full_path + "/pic1" + ext, full_path + "/" + hash5 + ext)

def download_from_list(given_url, full_path):
    """Download images from hotgirl.asia"""
    rip_url = given_url
    rip_url = rip_url.strip('\n')
    print(rip_url)
    file_name = os.path.basename(urlparse(rip_url).path)
    with open(full_path + '/' + file_name, "wb") as handle:
        response = requests.get(rip_url, stream=True)
        if not response.ok:
            print(response)
        for block in response.iter_content(1024):
            if not block:
                break
            handle.write(block)

def trim_url(given_url):
    """Return the URL without the filename attached."""
    if ".jpg" in given_url:
        given_url = str("/".join(given_url.split("/")[0:-1])) + "/"
    elif ".jpeg" in given_url:
        given_url = str("/".join(given_url.split("/")[0:-1])) + "/"
    elif ".png" in given_url:
        given_url = str("/".join(given_url.split("/")[0:-1])) + "/"
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
    sites = ["imhentai.com", "hotgirl.asia", "hentai.cafe", "cup-e.club", "girlsreleased.com"]
    return any(x in given_url for x in sites)

def sorted_nicely(l):
    """Sort the given iterable in the way that humans expect."""
    convert = lambda text: int(text) if text.isdigit() else text
    alphanum_key = lambda key: [convert(c) for c in re.split('([0-9]+)', key)]
    return sorted(l, key = alphanum_key)

if __name__ == "__main__":
    if len(sys.argv) > 1:
        album_url = sys.argv[1]
    else:
        raise Exception("Script requires a link as an argument")
    #image_ripper = ImageRipper(sys.argv[1])
    #image_ripper.image_getter()
    print(test_parse(sys.argv[1]))
