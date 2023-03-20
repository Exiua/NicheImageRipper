import base64
import collections
import json
import os
import re
import struct
import subprocess
import sys
import urllib.request
from urllib.parse import urlparse
from time import sleep

import requests
from PIL import Image
from bs4 import BeautifulSoup
import selenium
from selenium import webdriver
from selenium.webdriver import Keys
from selenium.webdriver.common.by import By
from selenium.webdriver.common.action_chains import ActionChains
from selenium.webdriver.firefox.options import Options
from selenium.webdriver.remote.webelement import WebElement

from Config import Config
from HtmlParser import DRIVER_HEADER


def string_join_test():
    """Test"""
    album_title = "Test Album"
    shoot_theme = " Test Shoot"
    model_name = "Test Model"
    model_name = "".join(["[", model_name, "]"])
    shoot_theme = shoot_theme.strip()
    print(model_name)
    dir_name = "(Cup E) " + album_title + " -" + shoot_theme + " " + model_name
    dir_name2 = " ".join(
        ["(Cup E)", album_title, "-", shoot_theme, model_name])
    return [dir_name, dir_name2]


def dequeue_test():
    """Test"""
    d = collections.deque('abcdefg')
    d.append('Test')
    print(d)
    for item in d:
        print(item)
    d.popleft()
    print(d)
    print(type(d))
    with open('test.json', 'w+') as f:
        if isinstance(d, collections.deque):
            d = list(d)
        json.dump(d, f)


def get_git_version():
    version = subprocess.check_output(['git', 'describe', '--tags'])
    version = version.decode("utf-8").strip('\n')
    end = version.find('-', 0)
    version = version[0:end]
    return version


def num_compare(num1, num2):
    return num1 > num2


def safe2(request):
    target = request.args.get('target', '')
    host = urlparse(target).hostname
    # Note the '.' preceding example.com
    if host and host.endswith(".imhentai.com"):
        return target


def return_test():
    try:
        return 5
    finally:
        print("hello")


def unbound_error():
    if 4 == 5:
        # noinspection PyUnusedLocal
        tester = 1
    # return tester


def strings_to_list(return_list=True):
    url_list = []
    user_in = input("Enter URL: ")
    regex = re.compile(
        r'^(?:http|ftp)s?://'  # http:// or https://
        # domain...
        r'(?:(?:[A-Z\d](?:[A-Z\d-]{0,61}[A-Z\d])?\.)+(?:[A-Z]{2,6}\.?|[A-Z\d-]{2,}\.?)|'
        r'localhost|'  # localhost...
        r'\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})'  # ...or ip
        r'(?::\d+)?'  # optional port
        r'(?:/?|[/?]\S+)$', re.IGNORECASE)
    while re.match(regex, user_in) is not None:
        url_list.append(user_in)
        user_in = input("Enter URL: ")
    print(len(url_list))
    if return_list:
        url_list = json.dumps(url_list)
        return url_list
    else:
        return " ".join(url_list)


def domain_extracter(url: str) -> str:
    domain = urlparse(url).netloc
    domain = domain.split(".")[-2]
    return domain


def string_to_int_list(given):
    int_list = []
    given = given.split()
    for i in given:
        int_list.append(int(i))
    return int_list


def long_num_trim(num):
    return str(num)[::19]


def split_test():
    s = "https://google.comhttps://google.comhttps://google.com"
    s = s.split("https://")
    print(s)
    return type(s)


def site_extractor(url: str) -> str:
    domain = urlparse(url).netloc
    return "".join(["https://", domain, "/"])


def user_inject_test() -> str:
    test = input("Enter answer: ")
    return test


requests_header = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) '
                  'Chrome/88.0.4324.190 Safari/537.36',
    'referer': 'https://imhentai.xxx/'}


def img_download(url: str):
    headers = {
        "User-Agent":
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 "
            "Safari/537.36 "
    }
    img_url = url
    ext = img_url.split(".")[-1]
    session = requests.Session()
    session.headers.update(headers)
    with open("".join(["test", ext]), "wb") as handle:
        bad_cert = False
        try:
            response = session.get(
                img_url, headers=requests_header, stream=True)
        except requests.exceptions.SSLError:
            response = session.get(
                img_url, headers=requests_header, stream=True, verify=False)
            bad_cert = True
        if not response.ok and not bad_cert:
            print(response)
        for block in response.iter_content(chunk_size=1024):
            if not block:
                break
            handle.write(block)
        # handle.write(response.content)


def text_formatter(text: str) -> str:
    text = text.replace("\n", "").replace("www.", "").replace("https:", "").replace("/", "").replace("\"", "").replace(
        "http:", "")
    text_list = text.split(",")
    text_list = [t.strip() for t in text_list]
    return ", ".join(text_list)


def text_sorter(text: str) -> str:
    text_list = text.split(", ")
    text_list.sort()
    text = ", ".join(text_list)
    return text


def text_finder(text: str) -> str:
    return ", ".join(re.findall(r'\[(.*?)]', text))


def request_test():
    requests.get("https://gyrls.com")


def basename_test():
    return \
        "https://forum.sexy-egirls.com/attachments/ashleytervortimage2-jpg.1435121/?hash" \
        "=03da4d5b8fed42098301bf3ecc4fc323".split(
            "/")[-2].split(".")[0].replace("-", ".")


def imgur_request():
    client_id = Config.config.keys["Imgur"]
    response = requests.get("https://api.imgur.com/3/image/gNJPnhT",
                            headers={'Authorization': f'Client-ID {client_id}'})
    json_data = response.json()
    print(json_data["data"]["link"])


def luscious_endpoint():
    endpoint = "https://members.luscious.net/graphqli/?"
    album_id = "324609"
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


def hitomi_endpoint():
    book_id = 2006299
    url = f'http://api.initiate.host/v1/hitomi.la/{book_id}/pages'
    response = requests.get(url, headers={'Data-type': 'application/json'})
    print(response)


def detect_corrupted_file(dirpath: str):
    files = [os.path.abspath(x) for x in os.listdir(dirpath)]
    bad_files = 0
    for f in files:
        try:
            img = Image.open(f)
            img.verify()
        except (IOError, SyntaxError):
            bad_files += 1
    print(bad_files)


def test_img_download(url: str):
    # requests_header = { 'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML,
    # like Gecko) Chrome/88.0.4324.190 Safari/537.36' } with requests.get(url, headers=requests_header, stream=True)
    # as r: with open("test.jpg", "wb") as f: shutil.copyfileobj(r.raw, f)
    import subprocess

    rcode = subprocess.run(["cyberdrop-dl", url, "-o", "Test"])
    print(rcode.returncode)


def ehnetai_requests():
    import time

    # params = {
    #     "referrer": "e-hentai.org",
    #     "content-type": "application/json",
    #     "cookies": {
    #         "nw": "1",
    #         "tip": "1"
    #     }
    # }
    # time.sleep(5)
    # response = requests.get("https://e-hentai.org/g/1745799/e409cf406e/", params=params)
    # print(response.text)
    # soup = BeautifulSoup(response.content, "lxml")
    # soup.find("div", id="gdt")

    # -----------------------
    time.sleep(5)
    payload = {
        'method': 'gdata',
        'gidlist': [
            [1745799, 'e409cf406e']
        ],
        'namespace': 1
    }
    params = {
        'content-type': 'application/json;charset=UTF-8'
    }
    response = requests.post(
        'https://api.e-hentai.org/api.php', json=payload, params=params)
    print(response.json())


def artstation_requests():
    response = requests.get("https://www.artstation.com/evanlee82")
    print(response.text)
    json_data = json.loads(response.text)
    print(json_data)


def m3u8_to_mp4_test():
    import m3u8_To_MP4

    m3u8_To_MP4.multithread_download(
        'https://iframe.mediadelivery.net/8baab001-3937-4f0a-8c8d-b247a31bf3b9/playlist.drm?contextId=d27e16cf-7877'
        '-48a2-93d6-da4d28ec3493&secret=13c5398c-052d-486a-ae27-f769392df5a3',
        mp4_file_name="test.mp4")


def reassign_files():
    from os import listdir
    from os.path import isfile, join

    path = 'D:\\Documents\\Programming\\Python\\NicheImageRipper\\Rips\\Bewyx'
    files = [f for f in listdir(path) if isfile(join(path, f)) and ".mp4" in f]
    empty_files = []
    rename_files = []
    for f in files:
        if "m3u8_To_Mp4" in f:
            rename_files.append(f)
        elif not os.path.getsize(join(path, f)):
            empty_files.append(f)
    for f in empty_files:
        file = join(path, f)
        os.remove(file)
    for i, f in enumerate(rename_files):
        old_name = join(path, f)
        new_name = join(path, empty_files[i])
        os.rename(old_name, new_name)


def gen_file():
    for letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ":
        with open(f"D:\\Documents\\Programming\\Python\\NicheImageRipper\\Parsers\\{letter}Parsers.py", "w") as f:
            f.write(
                "from __future__ import annotations\n\nimport pickle\nimport re\nimport sys\nfrom time import "
                "sleep\nfrom math import ceil\nfrom typing import Callable\nfrom urllib.parse import "
                "urlparse\n\nimport bs4\nimport requests\nimport selenium\nimport tldextract\nfrom bs4 import "
                "BeautifulSoup\nfrom selenium import webdriver\nfrom selenium.webdriver.common.by import By\nfrom "
                "selenium.webdriver.common.keys import Keys\n\nfrom rippers import requests_header, PARSER, PROTOCOL, "
                "SCHEME\nfrom Util.RipperHelpers import *\nfrom Util.RipperExceptions import *")
            f.write("\n\nDEBUG: bool = False\nTEST_PARSER = None\n\n")
            f.write("if __name__ == \"__main__\":\n")
            f.write("\tprint(test_parser(TEST_PARSER, sys.argv[1], DEBUG))")


def get_nth_elements():
    origin = [x for x in range(64)]
    print(origin)
    print(origin[3::4])


def deviantart_requests():
    response = requests.get("https://www.deviantart.com/sade75311/gallery/all")
    print(response)


# def psd_to_png():
#     from psd_tools import PSDImage

#     image = PSDImage.open("test.psd")
#     image.composite(force=True).save("test.png")


def get_webdriver() -> webdriver.Firefox:
    options = Options()
    options.headless = True
    options.add_argument(DRIVER_HEADER)
    driver = webdriver.Firefox(options=options)
    return driver


def test_gdrive_link_grabbing():
    driver: webdriver.Firefox = get_webdriver()
    driver.get("https://kemono.party/fanbox/user/39651/post/3632389")
    soup = BeautifulSoup(driver.page_source, 'lxml')
    possible_links_div = soup.find_all("div")
    possible_links_div = [tag.text for tag in possible_links_div]
    gd_links = []
    for text in possible_links_div:
        # print(text)
        if "drive.google.com" in text:
            parts = text.split()
            print(parts)
            for part in parts:
                print(part)
                if "drive.google.com" in part:
                    gd_links.append(part + "\n")
    print(gd_links)


def remove_dup_links(file: str, set_filter: bool = True):
    with open(file, "r", encoding="unicode_escape") as f:
        data = f.readlines()
    data = [d.replace("\x00", "").replace("\n", "") for d in data]
    data = [d for d in data if d != ""]
    for i, d in enumerate(data):
        if d.count("http") == 0:
            data[i] = ""
        elif d.count("http") > 1:
            links = d.split("http")[1:]
            data[i] = "http" + links[0]
            for j in range(1, len(links)):
                data.append("http" + links[j])
        else:
            link = d.split("http")
            data[i] = "http" + link[1]
    result = []
    if set_filter:
        seen = set()
        for item in data:
            if item not in seen:
                seen.add(item)
                result.append(item + "\n")
    else:
        print(data)
        for i in range(len(data)):
            item1 = data[i]
            if item1 == "":
                continue
            for j in range(i + 1, len(data)):
                item2 = data[j]
                if item2 == "":
                    continue
                print(f"i - {i}: {item1}\nj - {j}: {item2}")
                if len(item1) <= len(item2):
                    if item1 == item2[:len(item1)]:
                        if "#" in item1:
                            data[j] = ""
                        else:
                            data[i] = ""
                            break
                else:
                    if item2 == item1[:len(item2)]:
                        if "#" in item2:
                            data[i] = ""
                            break
                        else:
                            data[j] = ""
        result = [d + "\n" for d in data if d != ""]
        print(result)
    with open(rreplace(file, ".", "Out."), "w", encoding="unicode_escape") as f:
        f.writelines(result)


def parse_pixiv_links():
    with open("in.txt", "r") as f:
        data = f.readlines()
    data = [re.match(r"https://www\.pixiv\.net/en/users/(\d+)", d).group(1) for d in data]
    artists = " ".join(data)
    print(f"./PixivUtil2.exe -s 1 {artists} --is")


def enumerate_test():
    numbers = [1, 2, 3, 4, 5]
    for i, num in enumerate(numbers):
        numbers.append(num + i)
        print(i)
        if i == 20:
            break
    i = 0
    for num in numbers:
        numbers.append(num + i)
        print(i)
        i += 1
        if i == 20:
            break


def nonlocal_test():
    s = "w.exe"
    s = rreplace(s, ".", "_test.")
    print(s)


def rreplace(string: str, old: str, new: str, occurrence: int = 1) -> str:
    li = string.rsplit(old, occurrence)
    return new.join(li)


def progress_bar():
    from time import sleep

    for i in range(101):
        sleep(0.1)
        print(f"[{i:3}%]", end='\r')
    print()


def selenium_testing():
    options = Options()
    options.headless = True
    options.add_argument = DRIVER_HEADER
    options.set_preference("dom.disable_beforeunload", True)
    options.set_preference("browser.tabs.warnOnClose", False)
    driver = webdriver.Firefox(options=options)
    driver.get("https://google.com")

    input("Press any key to continue...")
    print(driver.binary.process.kill())
    # driver.quit()


def sc_merge():
    with open("in.json", "r") as f:
        data = json.load(f)
    first = json.loads(data["first"]["view_history"])
    second = json.loads(data["second"]["view_history"])
    print(len(first["v"]))
    print(len(second["v"]))
    combo = []
    combo.extend(first["v"])
    combo.extend(second["v"])
    combo = list(set(combo))
    print(len(combo))
    with open("out.json", "w") as f:
        json.dump(combo, f)


def mega_test():
    file_key = "-e9q8FxVGyeY5wHuiZOOeg/file/EAohUKxD"  # "kAoS2QqT"
    file_key = base64_to_a32(file_key)
    print(file_key)


def base64_to_a32(s):
    return str_to_a32(base64_url_decode(s))


def base64_url_decode(data):
    data += '=='[(2 - len(data) * 3) % 4:]
    for search, replace in (('-', '+'), ('_', '/'), (',', '')):
        data = data.replace(search, replace)
    return base64.b64decode(data)


def str_to_a32(b):
    if isinstance(b, str):
        b = makebyte(b)
    if len(b) % 4:
        # pad to multiple of 4
        b += b'\0' * (4 - len(b) % 4)
    return struct.unpack('>%dI' % (len(b) / 4), b)


def makebyte(x):
    import codecs
    return codecs.latin_1_encode(x)[0]


def parse_url(url: str):
    url = url.replace(' ', '')
    file_id = re.findall(r'\W\w\w\w\w\w\w\w\w\W', url)[0][1:-1]
    id_index = re.search(file_id, url).end()
    key = url[id_index + 1:]
    return f'{file_id}!{key}'


def danbooru_parse():
    from pybooru import Danbooru
    from time import sleep

    client = Danbooru('danbooru')
    links = []
    i = 0
    while True:
        posts = client.post_list(tags="deaver -navel", page=i)
        i += 1
        if len(posts) == 0:
            break
        links.extend([post["file_url"] for post in posts if "file_url" in post])
        sleep(0.1)
    print(links, len(links))


def rule34_parse():
    response = requests.get("https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&pid=0&tags=todding")
    data = response.json()
    images = []
    pid = 1
    while len(data) != 0:
        urls = [post["file_url"] for post in data]
        images.extend(urls)
        response = requests.get(
            f"https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&pid={pid}&tags=todding")
        pid += 1
        data = response.json()
    print(len(images))
    with open("test.json", "w") as f:
        json.dump(images, f, indent=4)
    # print(json)


class CredentialsTest:
    def __init__(self):
        username = ""
        password = ""


def object_serialization_test():
    cred = CredentialsTest()
    with open("test.json", "w") as f:
        json.dump(vars(cred), f, indent=4)


def object_deserialization_test():
    with open("test.json", "r") as f:
        cred: CredentialsTest = json.load(f)
    print(cred.username, cred.password)


def iframe_test():
    import urllib

    # response = urllib.request.urlopen("https://imgur.com/a/2crjBWC/embed?pub=true&ref=https%3A%2F%2Ftitsintops.com%2FphpBB2%2Findex.php%3Fthreads%2Fasian-yuma0ppai-busty-o-cup-japanese-chick.13525043%2F&w=540")
    response = requests.get(
        "https://imgur.com/a/2crjBWC/embed?pub=true&ref=https%3A%2F%2Ftitsintops.com%2FphpBB2%2Findex.php%3Fthreads%2Fasian-yuma0ppai-busty-o-cup-japanese-chick.13525043%2F&w=540")
    print(response.content)
    with open("test.html", "wb") as f:
        f.write(response.content)
    soup = BeautifulSoup(response.content, "lxml")
    print(soup.find("a", id="image-link").get("href"))


def cookie_test():
    import pickle

    cookies = pickle.load(open("cookies.pkl", "rb"))
    for cookie in cookies:
        print(cookie)


def session_test():
    r = requests.get(
        "https://titsintops.com/phpBB2/index.php?threads/asian-yuma0ppai-busty-o-cup-japanese-chick.13525043/")
    for c in r.cookies:
        print(f"{c.name}: {c.value}")


def create_driver(headless: bool) -> webdriver.Firefox:
    options = Options()
    options.headless = headless
    options.add_argument(DRIVER_HEADER)
    driver = webdriver.Firefox(options=options)
    return driver


def try_find_element(driver: webdriver.Firefox, by: str, value: str) -> WebElement | None:
    try:
        return driver.find_element(by, value)
    except selenium.common.exceptions.NoSuchElementException:
        return None


def tnt_login_helper(driver: webdriver.Firefox):
    download_url = driver.current_url
    logins = Config.config.logins
    driver.get("https://titsintops.com/phpBB2/index.php?login/login")
    # driver.find_element(By.XPATH, '//a[@class="p-navgroup-link p-navgroup-link--textual p-navgroup-link--logIn"]').click()
    login_input = try_find_element(driver, By.XPATH, '//input[@name="login"]')
    while not login_input:
        sleep(0.1)
        login_input = try_find_element(driver, By.XPATH, '//input[@name="login"]')
    login_input.send_keys(logins["TitsInTops"]["Username"])
    password_input = driver.find_element(By.XPATH, '//input[@name="password"]')
    password_input.send_keys(logins["TitsInTops"]["Password"])
    button = driver.find_element(By.XPATH, '//button[@class="button--primary button button--icon button--icon--login"]')
    button.click()
    while try_find_element(driver, By.XPATH,
                           '//button[@class="button--primary button button--icon button--icon--login"]'):
        sleep(0.1)
    driver.get(download_url)


def download_file(response: requests.Response, file_path: str):
    with open(file_path, "wb") as f:
        for block in response.iter_content(chunk_size=50000):
            if block:
                f.write(block)


def tnt_login_test():
    requests_header: dict[str, str] = {
        'User-Agent':
            'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36',
        'referer':
            'https://titsintops.com/phpBB2/index.php?login/login',
        'cookie':
            'tnt0=1;',
        # 'xf_csrf=l_TJPttw0YbmvBpf; yuo1={"objName":"uXqakQEuIfNAb","request_id":0,"zones":[{"idzone":"4717830","here":{}},{"idzone":"4717830","here":{}},{"idzone":"4717830"}]}'
        'origin':
            'titsintops.com'
    }
    driver = create_driver(True)
    driver.get("https://titsintops.com/phpBB2/index.php?threads/asian-yuma0ppai-busty-o-cup-japanese-chick.13525043/")
    tnt_login_helper(driver)
    cookies = driver.get_cookies()
    with open("test.json", "w") as f:
        json.dump(cookies, f, indent=4)
    driver.quit()
    s = requests.Session()
    for c in cookies:
        print(c)
        if c["name"] == "xf_user":
            requests_header["cookie"] += f'{c["name"]}={c["value"]};'
        s.cookies.set(c["name"], c["value"], domain=c["domain"])
    r = s.get("https://titsintops.com/phpBB2/index.php?attachments/c49492b2-944b-42cb-8c51-d0d7843182ab-jpg.2424014/",
              headers=requests_header)
    print(r)
    print(s.cookies.items())
    print(r.cookies.items())
    download_file(r, "test.jpg")
    with open("test.html", "wb") as f:
        f.write(r.content)


def gelbooru_parse():
    response = requests.get("https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&pid=0&tags=stelarhoshi")
    data: dict = response.json()
    images = []
    pid = 1
    posts = data["post"]
    while len(posts) != 0:
        urls = [post["file_url"] for post in posts]
        images.extend(urls)
        response = requests.get(
            f"https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&pid={pid}&tags=stelarhoshi")
        pid += 1
        data = response.json()
        posts = data.get("post", [])
    print(len(images))
    with open("test.json", "w") as f:
        json.dump(images, f, indent=4)
    # print(json)


def m3u8_to_mp4():
    import subprocess

    video_path = "test.mp4"
    video_url = "https://videos2-h.sendvid.com/hls/fa/54/cfhnmfob.mp4/master.m3u8?validfrom=1673236358&validto=1673243558&rate=200k&ip=24.15.25.190&hdl=-1&hash=JE18dpCujZO71mpCcxMK8em24nI%3D"

    cmd = ["ffmpeg", "-protocol_whitelist", "file,http,https,tcp,tls,crypto", "-i", video_url, "-c", "copy", video_path]
    subprocess.run(cmd)


def write_response(r: requests.Response, filepath: str):
    with open(filepath, "wb") as f:
        f.write(r.content)


def send_video_parse():
    r = requests.get("https://sendvid.com/cfhnmfob")
    soup = BeautifulSoup(r.content, "lxml")
    print(soup.find("source", id="video_source").get("src"))


def dead_link_test():
    r = requests.get("https://drive.google.com/file/d/1pxG_u7GfDbHzNGVa3c3OaqeWTc8EDdTj/view?usp=sharing")
    print(r)
    r = requests.get("https://mega.nz/folder/jPwnQCQD#P4SvKxHQsJzmWZXALSQPgg")
    print(r)
    write_response(r, "test.html")


class EventHandler:
    def __init__(self):
        self.events = []

    def add_listener(self, function):
        self.events.append(function)

    def invoke(self):
        for event in self.events:
            event()


class Foo:
    def __init__(self):
        pass

    def print(self):
        print("foo")


class Bar:
    def __init__(self):
        pass

    def baz(self):
        print("bar")


def event_test():
    event = EventHandler()
    foo = Foo()
    bar = Bar()
    event.add_listener(foo.print)
    event.add_listener(bar.baz)
    event.invoke()


class bcolors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    UNDERLINE = '\033[4m'


def color_print_test():
    print(f"{bcolors.HEADER}purple{bcolors.ENDC}")
    print(f"{bcolors.OKBLUE}blue{bcolors.ENDC}")
    print(f"{bcolors.OKCYAN}cyan{bcolors.ENDC}")
    print(f"{bcolors.OKGREEN}green{bcolors.ENDC}")
    print(f"{bcolors.WARNING}yellow{bcolors.ENDC}")
    print(f"{bcolors.FAIL}red{bcolors.ENDC}")


if __name__ == "__main__":
    color_print_test()
