from __future__ import annotations

import copy
import json
import os

CONFIG: str = 'config.json'
CONFIG_TEMPLATE: dict = {
    "SavePath": "./Rips/",
    "Theme": "Dark",
    "FilenameScheme": "Original",
    "UnzipProtocol": "None",
    "AskToReRip": True,
    "LiveHistoryUpdate": False,
    "NumberOfThreads": 1,
    "Logins": {
        "Sexy-Egirls": {
            "Username": "",
            "Password": ""
        },
        "V2Ph": {
            "Username": "",
            "Password": ""
        },
        "Porn3dx": {
            "Username": "",
            "Password": ""
        },
        "DeviantArt": {
            "Username": "",
            "Password": ""
        },
        "Mega": {
            "Username": "",
            "Password": ""
        },
        "TitsInTops": {
            "Username": "",
            "Password": ""
        },
        "Newgrounds": {
            "Username": "",
            "Password": ""
        },
        "Nijie": {
            "Username": "",
            "Password": ""
        },
        "SimpCity": {
            "Username": "",
            "Password": ""
        }
    },
    "Keys": {
        "Imgur": "",
        "Google": "",
        "Dropbox": "",
        "Pixeldrain": ""
    },
    "Cookies": {
        "Twitter": "",
        "Newgrounds": ""
    }
}


class Config:
    config: Config = None

    def __init__(self, config_path: str = CONFIG):
        self._config: dict = {}
        self._config_path: str = config_path
        if not os.path.isfile(config_path):
            self.__create_config()
        else:
            with open(config_path, "r") as f:
                self._config = json.load(f)
            self.__validate_config()

    def __getitem__(self, key: str) -> str:
        return self._config[key]

    def __setitem__(self, key: str, value: str):
        self._config[key] = value
        self.__save_config()

    @property
    def logins(self) -> dict[str, dict[str, str]]:
        return self._config["Logins"]

    @property
    def keys(self) -> dict[str, str]:
        return self._config["Keys"]
    
    @property
    def cookies(self) -> dict[str, str]:
        return self._config["Cookies"]

    def __create_config(self):
        self._config = copy.deepcopy(CONFIG_TEMPLATE)
        self.__save_config()

    def __save_config(self):
        with open(self._config_path, 'w') as f:  # save
            json.dump(self._config, f, indent=4)

    def __validate_config(self):
        for key in CONFIG_TEMPLATE:
            if key not in self._config:
                if key == "Logins":
                    self._config[key] = copy.deepcopy(CONFIG_TEMPLATE[key])
                else:
                    self._config[key] = CONFIG_TEMPLATE[key]
        logins = CONFIG_TEMPLATE["Logins"]
        config_logins = self._config["Logins"]
        for key in logins:
            if key not in config_logins:
                config_logins[key] = {
                    "Username": "",
                    "Password": ""
                }
        keys = CONFIG_TEMPLATE["Keys"]
        config_keys = self._config["Keys"]
        for key in keys:
            if key not in config_keys:
                config_keys[key] = ""


if __name__ != '__main__':
    Config.config = Config()
