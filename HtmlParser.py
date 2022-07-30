from selenium.webdriver.common.by import By
from selenium import webdriver
from selenium.webdriver.firefox.options import Options

from rippers import DRIVER_HEADER

logged_in: bool


class HtmlParser:
    def __init__(self, site_name: str = None):
        global logged_in
        options = Options()
        options.headless = site_name != "v2ph" or logged_in
        options.add_argument = DRIVER_HEADER
        self.driver = webdriver.Firefox(options=options)

    def parse_site(self, url: str):
        url = url.replace("members.", "www.")
        self.driver.get(url)

    def site_login(self, site_name: str, given_url: str, logins: dict[str, str]):
        curr_url = self.driver.current_url
        if site_name == "sexy-egirls" and "forum." in given_url:
            self.driver.implicitly_wait(10)
            self.driver.get("https://forum.sexy-egirls.com/login/")
            self.driver.find_element(By.XPATH, "//input[@type='text']").send_keys(logins["sexy-egirls"][0])
            self.driver.find_element(By.XPATH, "//input[@type='password']").send_keys(logins["sexy-egirls"][1])
            self.driver.find_element(By.XPATH, "//button[@type='submit']").click()
        self.driver.get(curr_url)
