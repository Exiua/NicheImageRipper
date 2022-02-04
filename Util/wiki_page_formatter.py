import json
import os
import re

import requests

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
        self.sites.sort()
        self.save()

    def add_from_file(self):
        with open("input.txt", "r") as f:
            text = f.read()
        text = text.replace("\n", "").replace("\"", "")
        text_list = text.split(",")
        text_list = [t.strip() for t in text_list]
        self.add(text_list)

    def valid_site(self, url: str) -> bool:
        try:
            requests.get(url)
            return True
        except:
            return False

    def view(self):
        print(self.sites)

    def wiki_format(self):
        with open("output.txt", "w+") as f:
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
    formatter = WikiPageFormatter()
    while True:
        user_in = input("Enter: ")
        if user_in.lower() in ("stop", "esc", "end", "q", "quit"):
            break
        elif user_in.lower() == "add":
            user_in = input("Add: ")
            user_in = user_in.replace("www.", "").replace("https:", "").replace("/", "").replace("\"", "").replace("http:", "")
            if "," in user_in: 
                user_in = user_in.split(", ")
            formatter.add(user_in)
        elif user_in.lower() in ("format", "f"):
            formatter.wiki_format()
        elif user_in.lower() in ("fileadd", "add from file", "file"):
            formatter.add_from_file()
        elif user_in.lower() in ("v", "view"):
            formatter.view()
        