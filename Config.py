from __future__ import annotations
import json
import os

CONFIG: str = 'config.json'


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

    def __getitem__(self, key: str) -> str:
        return self._config[key]

    def __setitem__(self, key, value):
        self._config[key] = value
        self.__save_config()

    @property
    def logins(self) -> dict[str, dict[str, str]]:
        return self._config["Logins"]

    @property
    def keys(self):
        return self._config["Keys"]

    def __create_config(self):
        self._config = {
            "SavePath": "./Rips/",
            "Theme": "Dark",
            "FilenameScheme": "Original",
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
                }
            },
            "Keys": {
                "Imgur": ""
            }
        }
        self.__save_config()

    def __save_config(self):
        with open(self._config_path, 'w') as f:  # save
            json.dump(self._config, f, indent=4)


if __name__ != '__main__':
    Config.config = Config()
