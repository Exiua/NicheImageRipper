from __future__ import annotations

import pytest
import json
from selenium import webdriver
from selenium.webdriver.firefox.options import Options
import rippers
from rippers import ImageRipper

with open("parameters.json", "r", encoding='utf-8') as f:
    test_data = json.load(f)


@pytest.mark.parametrize("parser,url,count,dir_name", test_data)
def test_parser(parser: str, url: str, count: int, dir_name: str):
    parser = eval("".join(["rippers.", parser, "_parse"]))
    driver = None
    try:
        options = Options()
        options.headless = True
        options.add_argument = (
            "user-agent=Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z‡ Safari/537.36")
        driver = webdriver.Firefox(options=options)
        driver.get(url)
        parser_data = parser(driver)
    finally:
        driver.quit()
    assert parser_data[1] == count
    assert parser_data[2] == dir_name
