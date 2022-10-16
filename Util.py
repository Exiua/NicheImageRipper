"""DO NOT USE, THIS MODULE IS DEPRECATED; WORKING ON REFACTORING CODE INTO SEPARATE MODULES"""
from __future__ import annotations

import argparse
import configparser
import os
import time
from os import path
from typing import Callable

from selenium import webdriver
from selenium.webdriver.firefox.options import Options

from RipInfo import RipInfo

SCHEME: str = "https://"
CONFIG: str = 'config.ini'

# Global Variables
logged_in: bool


class Config:
    def __init__(self, config_path: str = CONFIG):
        self._config: configparser.ConfigParser = configparser.ConfigParser()
        self._config_path: str = config_path
        if not os.path.isfile(config_path):
            self.__create_config()
        else:
            self._config.read(config_path)

    def __getitem__(self, item: tuple[str, str]) -> str:
        section, key = item
        return self._config[section][key]

    def __setitem__(self, item, value):
        section, key = item
        self._config[section][key] = value
        self.__save_config()

    def __create_config(self):
        self._config['DEFAULT'] = {}
        self._config['DEFAULT']['SavePath'] = 'Rips/'
        self._config['DEFAULT']['Theme'] = 'Dark'
        self._config['DEFAULT']['FilenameScheme'] = 'Original'
        self._config['DEFAULT']['AskToReRip'] = 'True'
        self._config['DEFAULT']['LiveHistoryUpdate'] = 'False'
        self._config['DEFAULT']['NumberOfThreads'] = '1'
        self._config['LOGINS'] = {}
        self._config['LOGINS']['Sexy-EgirlsU'] = ''
        self._config['LOGINS']['Sexy-EgirlsP'] = ''
        self._config['LOGINS']['V2PhU'] = ''
        self._config['LOGINS']['V2PhP'] = ''
        self._config['LOGINS']['Porn3dxU'] = ''
        self._config['LOGINS']['Porn3dxP'] = ''
        self._config['LOGINS']['DeviantArtU'] = ''
        self._config['LOGINS']['DeviantArtP'] = ''
        self._config['KEYS'] = {}
        self._config['KEYS']['Imgur'] = ''
        self.__save_config()

    def __save_config(self):
        with open(self._config_path, 'w') as f:  # save
            self._config.write(f)


def url_check(given_url: str) -> bool:
    """Check the url to make sure it is from valid site"""
    sites = ("https://imhentai.xxx/", "https://hotgirl.asia/", "https://www.redpornblog.com/",
             "https://www.cup-e.club/", "https://girlsreleased.com/", "https://www.bustybloom.com/",
             "https://www.morazzia.com/", "https://www.novojoy.com/", "https://www.hqbabes.com/",
             "https://www.silkengirl.com/", "https://www.babesandgirls.com/", "https://www.babeimpact.com/",
             "https://www.100bucksbabes.com/", "https://www.sexykittenporn.com/", "https://www.babesbang.com/",
             "https://www.exgirlfriendmarket.com/", "https://www.novoporn.com/", "https://www.hottystop.com/",
             "https://www.babeuniversum.com/", "https://www.babesandbitches.net/", "https://www.chickteases.com/",
             "https://www.wantedbabes.com/", "https://cyberdrop.me/", "https://www.sexy-egirls.com/",
             "https://www.pleasuregirl.net/", "https://www.sexyaporno.com/", "https://www.theomegaproject.org/",
             "https://www.babesmachine.com/", "https://www.babesinporn.com/", "https://www.livejasminbabes.net/",
             "https://www.grabpussy.com/", "https://www.simply-cosplay.com/", "https://www.simply-porn.com/",
             "https://pmatehunter.com/", "https://www.elitebabes.com/", "https://www.xarthunter.com/",
             "https://www.joymiihub.com/", "https://www.metarthunter.com/", "https://www.femjoyhunter.com/",
             "https://www.ftvhunter.com/", "https://www.hegrehunter.com/", "https://hanime.tv/",
             "https://members.hanime.tv/", "https://www.babesaround.com/", "https://www.8boobs.com/",
             "https://www.decorativemodels.com/", "https://www.girlsofdesire.org/", "https://www.tuyangyan.com/",
             "http://www.hqsluts.com/", "https://www.foxhq.com/", "https://www.rabbitsfun.com/",
             "https://www.erosberry.com/", "https://www.novohot.com/", "https://eahentai.com/",
             "https://www.nightdreambabe.com/", "https://xmissy.nl/", "https://www.glam0ur.com/",
             "https://www.dirtyyoungbitches.com/", "https://www.rossoporn.com/", "https://www.nakedgirls.xxx/",
             "https://www.mainbabes.com/", "https://www.hotstunners.com/", "https://www.sexynakeds.com/",
             "https://www.nudity911.com/", "https://www.pbabes.com/", "https://www.sexybabesart.com/",
             "https://www.heymanhustle.com/", "https://sexhd.pics/", "http://www.gyrls.com/",
             "https://www.pinkfineart.com/", "https://www.sensualgirls.org/", "https://www.novoglam.com/",
             "https://www.cherrynudes.com/", "https://www.join2babes.com/", "https://gofile.io/",
             "https://www.babecentrum.com/", "http://www.cutegirlporn.com/", "https://everia.club/",
             "https://imgbox.com/", "https://nonsummerjack.com/", "https://myhentaigallery.com/",
             "https://buondua.com/", "https://f5girls.com/", "https://hentairox.com/",
             "https://www.redgifs.com/", "https://kemono.party/", "https://www.sankakucomplex.com/",
             "https://www.luscious.net/", "https://sxchinesegirlz.one/", "https://agirlpic.com/",
             "https://www.v2ph.com/", "https://nudebird.biz/", "https://bestprettygirl.com/",
             "https://coomer.party/", "https://imgur.com/", "https://www.8kcosplay.com/",
             "https://www.inven.co.kr/", "https://arca.live/", "https://www.cool18.com/",
             "https://maturewoman.xyz/", "https://putmega.com/", "https://thotsbay.com/",
             "https://tikhoe.com/", "https://lovefap.com/", "https://comics.8muses.com/",
             "https://www.jkforum.net/", "https://leakedbb.com/", "https://e-hentai.org/",
             "https://jpg.church/", "https://www.artstation.com/", "https://porn3dx.com/",
             "https://www.deviantart.com/", "https://readmanganato.com/", "https://manganato.com/")
    return any(given_url.startswith(x) for x in sites)


if __name__ == "__main__":
    pass
