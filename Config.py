import configparser
import os

CONFIG: str = 'config.ini'


class Config:
    config: Config = None

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
        self._config['LOGINS']['MegaU'] = ''
        self._config['LOGINS']['MegaP'] = ''
        self._config['KEYS'] = {}
        self._config['KEYS']['Imgur'] = ''
        self.__save_config()

    def __save_config(self):
        with open(self._config_path, 'w') as f:  # save
            self._config.write(f)


if __name__ != '__main__':
    Config.config = Config()
