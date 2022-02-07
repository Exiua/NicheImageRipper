import json
import os
import re
import argparse
import requests
from urllib.parse import urlparse

regex = re.compile(
        r'^(?:http|ftp)s?://' # http:// or https://
        r'(?:(?:[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?\.)+(?:[A-Z]{2,6}\.?|[A-Z0-9-]{2,}\.?)|' #domain...
        r'localhost|' #localhost...
        r'\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})' # ...or ip
        r'(?::\d+)?' # optional port
        r'(?:/?|[/?]\S+)$', re.IGNORECASE)

class WikiPageFormatter():
    def __init__(self):
        self.sites: list[str] = []
        if os.path.exists("SupportedSites.json"):
            self.load()

    def add(self, thing: str or list):
            if isinstance(thing, str):
                if "," in thing:
                    thing = thing.split(",")
                    for t in thing:
                        if t not in self.sites and self.valid_site(t):
                            self.sites.append(t)
                elif thing not in self.sites and self.valid_site(thing):
                    self.sites.append(thing)
            else:
                for t in thing:
                    if t not in self.sites and self.valid_site(t):
                        self.sites.append(t)
            self.sites.sort(key = lambda x: urlparse(x).netloc.split(".")[-2])
            self.save()

    def update(self):
        with open("rippers.py", "r") as f:
            data = f.readlines()
            print(data)
        start = 0
        end = 0
        for i, line in enumerate(data):
            if "def url_check(given_url: str) -> bool:" in line:
                start = i + 2
            if "return any(x in given_url for x in sites)" in line:
                end = i
                break
        data = [l.strip() for l in data[start:end]]
        data = "".join(data).replace(",", ", ").replace("sites = (", "").replace(")", "")
        self.add(data.split(", "))
        print(self.sites)

    def valid_site(self, url: str) -> bool:
        try:
            requests.get(url)
            return True
        except:
            return False

    def view(self):
        print(self.sites)

    def wiki_format(self, dest: str):
        with open(dest, "w+") as f:
            for site in self.sites:
                f.write("".join(["- [", site.replace("www.", "").replace("https:", "").replace("http:", "").replace("/", ""), "](", site, ")\n"]))

    def save(self):
        """Save data to file"""
        with open("SupportedSites.json", 'w+') as save_file:
            json.dump(self.sites, save_file, indent=4)

    def load(self):
        """Read data from file"""
        with open("SupportedSites.json", 'r') as f:
            self.sites = json.load(f)

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', '-o', action='store_true')
    parser.add_argument('--list', '-l', action='store_true')
    parser.add_argument('--update', '-u', action='store_true')
    parser.add_argument('dir', nargs='?', default="./Util/Supported-Sites.md")
    args = parser.parse_args()
    
    formatter = WikiPageFormatter()
    if args.update:
        formatter.update()
    if args.output:
        formatter.wiki_format(args.dir)
    if args.list:
        formatter.view()  