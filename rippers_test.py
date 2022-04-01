from __future__ import annotations

import pytest
import json
from selenium import webdriver
from selenium.webdriver.firefox.options import Options
import rippers
from rippers import ImageRipper

def test_parsers():
    parameters: list[list[str | int]] = parameter_parser()
    for param in parameters:
        parser_test(param)

def parameter_parser():
    with open("parameters.json", "r") as f:
        params = json.load(f)
    return params

def parser_test(parameters: list[str]):
    print("".join(["Testing ", parameters[0]]))
    parser_data = parser_driver(eval("".join(["rippers.", parameters[0]])), parameters[1])
    assert parser_data[1] == parameters[2]
    assert parser_data[2] == parameters[3]

def parser_driver(parser, url):
    driver = None
    try:
        options = Options()
        options.headless = True
        options.add_argument = ("user-agent=Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z‡ Safari/537.36")
        driver = webdriver.Firefox(options=options)
        driver.get(url)
        return parser(driver)
    finally:
        driver.quit()