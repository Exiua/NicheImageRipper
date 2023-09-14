import sys

import requests
from bs4 import BeautifulSoup
from selenium import webdriver
from selenium.webdriver.firefox.options import Options

from ImageRipper import ImageRipper

requests_header = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36',
    'referer': 'https://kemono.party/',
    'cookie': '__ddgid=8OYPBCcijNqNLFPG; __ddg2=jJBBC0uFUQodvYkW; __ddg1=7H91n5MBCH1UanO5mMhw'
    }


def download_file(rip_url: str):
    with open("test.gif", "wb") as handle:
        response = requests.get(rip_url, headers=requests_header, stream=True)
        if not response.ok:
            print(response)
        for block in response.iter_content(chunk_size=50000):
            if not block:
                break
            handle.write(block)


def get_latests_repo_version():
    response = requests.get("https://api.github.com/repos/Exiua/NicheImageRipper/releases/latest")
    version = response.json()['tag_name']
    print('v2.7.9' < version)


def alwiki_parse(driver: webdriver.Firefox) -> tuple[list[str], int, str]:
    """Read the html for"""
    # Parses the html of the site
    soup = BeautifulSoup(driver.page_source, "lxml")
    dir_name = "Azur Lane Loading Screens"
    gallery_tags = soup.find_all("div", class_="shipgirl-art-gallery")
    images = []
    for tag in gallery_tags:
        images.extend(tag.find_all("img"))
    images = ["/".join(img.get("src").split("/")[:-1]).replace("/thumb/", "/") for img in images]
    num_files = len(images)
    driver.quit()
    return images, num_files, dir_name


def _test_parse(given_url: str) -> tuple[list[str], int, str]:
    """Test the parser to see if it properly returns image URL(s), number of images, and folder name."""
    driver = None
    try:
        options = Options()
        options.headless = True
        options.add_argument = (
            "user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36")
        driver = webdriver.Firefox(options=options)
        driver.get(given_url)
        return alwiki_parse(driver)
    finally:
        driver.quit()


if __name__ == "__main__":
    # __download_file(sys.argv[1])
    # get_latests_repo_version()
    # print(_test_parse(sys.argv[1]))
    ripper = ImageRipper()
    ripper.custom_rip(sys.argv[1], alwiki_parse)
