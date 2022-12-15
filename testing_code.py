import base64
import collections
import json
import os
import re
import struct
import subprocess
from urllib.parse import urlparse

import requests
from PIL import Image
from bs4 import BeautifulSoup
from selenium import webdriver
from selenium.webdriver.firefox.options import Options


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
    response = requests.get("https://api.imgur.com/3/album/BEJ5oFZ",
                            headers={'Authorization': 'Client-ID eb3193efe167c8e'})
    json_data = response.json()
    print(len(json_data["data"]["images"]))
    print([img.get("link")
           for img in response.json().get("data").get("images")])


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


def psd_to_png():
    from psd_tools import PSDImage

    image = PSDImage.open("test.psd")
    image.composite(force=True).save("test.png")


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
    file_key = "-e9q8FxVGyeY5wHuiZOOeg/file/EAohUKxD"# "kAoS2QqT"
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


if __name__ == "__main__":
    # remove_dup_links(sys.argv[1])
    # nonlocal_test()
    # parse_pixiv_links()
    mega_test()
    #print(parse_url("https://mega.nz/folder/hAhFzTBB#-e9q8FxVGyeY5wHuiZOOeg/file/EAohUKxD"))
    # remove_dup_links("gdriveLinks.txt", False)
    # progress_bar()
    # selenium_testing()
    # sc_merge()
