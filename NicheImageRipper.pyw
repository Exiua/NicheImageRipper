from __future__ import annotations

import abc
import argparse
import json
import os
import sys
import threading
from abc import ABC
from datetime import datetime
from queue import Queue
from threading import Timer
from typing import Callable

import requests
from PyQt6 import QtCore, QtGui
from PyQt6.QtGui import QFont, QTextCursor, QColor, QScreen
from PyQt6.QtCore import Qt
from PyQt6.QtWidgets import QApplication, QLineEdit, QWidget, QFormLayout, QPushButton, QHBoxLayout, QTabWidget, \
    QTextEdit, QTableWidget, QTableWidgetItem, QLabel, QCheckBox, QFileDialog, QComboBox, QMessageBox, \
    QTextBrowser
from qt_material import apply_stylesheet

from Config import Config
from Enums import FilenameScheme, UnzipProtocol, QueueResult
from ImageRipper import ImageRipper
from StatusSync import StatusSync
from Util import url_check


class OutputRedirect(QtCore.QObject):
    outputWritten = QtCore.pyqtSignal(str, bool)

    def __init__(self, parent, stdout=True):
        super().__init__(parent)
        if stdout:
            self._stream = sys.stdout
            sys.stdout = self
        else:
            self._stream = sys.stderr
            sys.stderr = self
        self._stdout = stdout

    def write(self, text):
        self._stream.write(text)
        self.outputWritten.emit(text, self._stdout)

    def __getattr__(self, name):
        return getattr(self._stream, name)

    def __del__(self):
        try:
            if self._stdout:
                sys.stdout = self._stream
            else:
                sys.stderr = self._stream
        except AttributeError:
            pass


# region pyqt helper functions

def create_button(text: str, width: int = 100) -> QPushButton:
    button = QPushButton()
    button.setText(text)
    button.setFixedWidth(width)
    return button


# endregion

class NicheImageRipper(ABC):
    display_sync: threading.Semaphore = threading.Semaphore(0)
    update_display: QtCore.pyqtSignal = QtCore.pyqtSignal(ImageRipper, str)

    def __init__(self):
        super().__init__()
        self.title: str = "NicheImagerRipper"
        self.latest_version: str = self.get_git_version()
        self.url_queue: Queue = Queue()
        self.live_update: bool = False
        self.rerip_ask: bool = True
        self.interrupted: bool = False
        # noinspection PyTypeChecker
        self.ripper: ImageRipper = None
        self.filename_scheme: FilenameScheme = FilenameScheme.ORIGINAL
        self.unzip_protocol: UnzipProtocol = UnzipProtocol.NONE
        self.status_sync: StatusSync = StatusSync()
        self.ripper_thread: threading.Thread = threading.Thread()
        self.version: str = "v2.2.0"
        self.save_folder: str = "."

    @abc.abstractmethod
    def get_history_data(self) -> list[list[str]]:
        """

        """
        raise NotImplementedError

    @abc.abstractmethod
    def load_history(self):
        """

        """
        raise NotImplementedError

    @abc.abstractmethod
    def update_history(self, ripper: ImageRipper, url: str):
        """

        """
        raise NotImplementedError

    def load_app_data(self):
        if os.path.isfile('config.json'):
            self.load_config()
        if os.path.isfile('RipHistory.json'):
            self.load_history()

    def load_config(self):
        """
            Load application configuration file
        """
        self.save_folder = Config.config['SavePath']
        self.filename_scheme = FilenameScheme[Config.config['FilenameScheme'].upper()]
        self.unzip_protocol = UnzipProtocol[Config.config["UnzipProtocol"].upper()]
        self.rerip_ask = Config.config['AskToReRip']
        self.live_update = Config.config['LiveHistoryUpdate']

    def _load_url_file(self, filepath: str):
        with open(filepath, 'r') as load_file:
            loaded_urls = json.load(load_file)
        for url in loaded_urls:
            self.add_to_url_queue(url)

    @staticmethod
    def _load_history() -> list[list[str]]:
        with open("RipHistory.json", 'r') as load_file:
            rip_history = json.load(load_file)
        return rip_history

    @abc.abstractmethod
    def add_to_url_queue(self, item: str):
        """
            Add a url to the url queue
        :param item: url to add to the url queue
        """
        raise NotImplementedError

    def _queue_urls(self, urls: str) -> QueueResult:
        url_list = self.separate_string(urls, "https://")
        result = QueueResult.SUCCESS
        for url in url_list:
            if url.count("http://") > 1:
                urls = self.separate_string(url, "http://")
                for u in urls:
                    self.add_to_url_queue(u)
            else:
                if url_check(url):
                    if url not in self.url_queue.queue:
                        self.add_to_url_queue(url)
                    else:
                        if result < QueueResult.ALREADY_QUEUED:
                            result = QueueResult.ALREADY_QUEUED
                else:
                    if result < QueueResult.NOT_SUPPORTED:
                        result = QueueResult.NOT_SUPPORTED
        return result

    def rip_urls_starter(self):
        """
            Start a new thread for ripping all images from urls in the url queue
        """
        if not self.ripper_thread.is_alive() and self.url_queue.qsize() != 0:
            self.ripper_thread = threading.Thread(target=self.rip_urls, daemon=True)
            self.ripper_thread.start()

    @abc.abstractmethod
    def rip_urls(self):
        """
            Rip files from all urls in the url queue
        """
        raise NotImplementedError

    def _rip_url(self) -> str:
        url = self.url_queue.queue[0]
        print(url)
        self.ripper = ImageRipper(self.filename_scheme, self.unzip_protocol)
        self.interrupted = True  # Flag to indicate ripper was running when close event executes
        self.ripper.rip(url)
        self.interrupted = False
        self.url_queue.get()
        return url

    def save_data(self):
        if self.url_queue.not_empty:
            self.save_to_json('UnfinishedRips.json', list(self.url_queue.queue))  # Save queued urls
        if self.interrupted and self.ripper.current_index > 1:
            with open(".ripIndex", "w") as f:
                f.write(str(self.ripper.current_index))
        self.save_to_json('RipHistory.json', self.get_history_data())  # Save history data
        Config.config['SavePath'] = self.save_folder  # Update the config
        # Config.config['DEFAULT', 'Theme'] = self.theme_color
        Config.config['FilenameScheme'] = self.filename_scheme.name.title()
        Config.config['AskToReRip'] = self.rerip_ask
        Config.config['LiveHistoryUpdate'] = self.live_update
        # Config.config['DEFAULT', 'NumberOfThreads'] = str(self.max_threads)

    def is_latest_version(self) -> bool:
        """
            Check current version against latest version of application
        """
        v1 = self.version.replace("v", "").split(".")
        v2 = self.latest_version.replace("v", "").split(".")

        if int(v1[0]) == int(v2[0]):
            if int(v1[1]) == int(v2[1]):
                return int(v1[2]) >= int(v2[2])
            else:
                return int(v1[1]) > int(v2[1])
        else:
            return int(v1[0]) > int(v2[0])

    def clear_cache(self):
        """
            Removed the cache files .ripIndex and partial.json
        """
        self.__silently_remove_files(".ripIndex", "partial.json")

    def __silently_remove_files(self, *filepaths: str):
        """
            Remove files without raise an exception
        :param filepaths: filepaths to remove
        """
        for filepath in filepaths:
            self.__silently_remove_file(filepath)

    @staticmethod
    def __silently_remove_file(filepath: str):
        """
            Remove a filepath without raising an exception
        :param filepath: filepath to remove
        """
        try:
            os.remove(filepath)
        except FileNotFoundError:
            pass

    @staticmethod
    def separate_string(base_string: str, delimiter: str) -> list[str]:
        """
            Split a string while keeping the delimiter attached to each part
        :param base_string: string to spilt
        :param delimiter: delimiter to split the string by
        :return: list of split elements of the base_string with delimiters still attached
        """
        string_list = base_string.split(delimiter)  # Split by delimiter
        if string_list[0] == "":
            string_list.pop(0)
        string_list = ["".join([delimiter, string.strip()]) for string in string_list]
        return string_list

    @staticmethod
    def save_to_json(file_name: str, data: any):
        """Save data to json file"""
        with open(file_name, 'w') as save_file:
            json.dump(data, save_file, indent=4)

    @staticmethod
    def get_git_version() -> str:
        """
        Retrieve the version tag from the remote git repo

        :return: latest version tag from the remote git repo or v0.0.0 if unable to connect to the repo
        """
        try:
            response = requests.get("https://api.github.com/repos/Exiua/NicheImageRipper/releases/latest")
        except requests.exceptions.ConnectionError:
            return "v0.0.0"
        try:
            return response.json()['tag_name']
        except KeyError:
            return "v0.0.0"


class NicheImageRipperMeta(type(QWidget), type(NicheImageRipper)):
    pass


class NicheImageRipperGUI(QWidget, NicheImageRipper, metaclass=NicheImageRipperMeta):
    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)
        ImageRipper.status_sync = self.status_sync

        stdout = OutputRedirect(self, True)
        stderr = OutputRedirect(self, False)

        self.setGeometry(0, 0, 768, 432)

        qt_rectangle = self.frameGeometry()
        center_point = self.screen().availableGeometry().center()
        qt_rectangle.moveCenter(center_point)
        self.move(qt_rectangle.topLeft())

        # region UI Construction

        first_row = QHBoxLayout()

        # region First Row Construction

        self.url_field = QLineEdit()
        self.url_field.setFont(QFont("Arial"))
        rip_button = QPushButton("Rip")
        self.pause_button = QPushButton()
        self.pause_button.setIcon(QtGui.QIcon("./Icons/pause.svg"))
        self.status_label = QLabel()
        first_row.addWidget(self.url_field)
        first_row.addWidget(rip_button)
        first_row.addWidget(self.pause_button)
        first_row.addWidget(self.status_label)

        # endregion

        # region Tab Creation

        tab_widget = QTabWidget()

        # region Log Tab

        self.log_field = QTextBrowser()
        self.log_field.setFont(QFont("Arial"))

        # endregion

        # region Queue Tab

        self.queue_field = QTextEdit()
        self.queue_field.setFont(QFont("Arial"))
        self.queue_field.setReadOnly(True)

        # endregion

        # region History Tab

        self.history_table = QTableWidget()
        self.history_table.setColumnCount(4)
        self.history_table.setHorizontalHeaderLabels(["Name", "Url", "Date", "#"])
        self.history_table.verticalHeader().setVisible(False)
        self.history_table.setFont(QFont("Arial"))
        for i, width in enumerate([300, 300, 80, 30]):
            self.history_table.setColumnWidth(i, width)

        # endregion

        # region Settings Tab

        self.settings_tab = QWidget()
        vbox = QFormLayout()
        self.save_folder_label = QLabel()
        self.save_folder_label.setText(self.save_folder)

        save_folder_button = create_button("Browse")

        load_url_button = create_button("Browse")

        check_update_hbox = QHBoxLayout()
        check_update_button = create_button("Check")
        self.check_update_label = QLabel()
        check_update_hbox.addWidget(check_update_button)
        check_update_hbox.addWidget(self.check_update_label)

        clear_cache_hbox = QHBoxLayout()
        clear_cache_button = create_button("Clear")
        clear_cache_hbox.addWidget(clear_cache_button)

        self.file_scheme_combobox = QComboBox()
        self.file_scheme_combobox.addItems(("Original", "Hash", "Chronological"))
        self.file_scheme_combobox.setFixedWidth(100)
        self.file_scheme_combobox.setCurrentIndex(self.filename_scheme.value)
        self.file_scheme_combobox.currentTextChanged.connect(self.file_scheme_changed)

        self.unzip_protocol_combobox = QComboBox()
        self.unzip_protocol_combobox.addItems(("None", "Extract", "Extract and Delete"))
        self.unzip_protocol_combobox.setFixedWidth(100)
        self.unzip_protocol_combobox.setCurrentIndex(self.unzip_protocol.value)
        self.unzip_protocol_combobox.currentTextChanged.connect(self.unzip_protocol_changed)

        checkbox_row = QHBoxLayout()
        checkbox_row.setAlignment(Qt.AlignmentFlag.AlignLeft)
        self.rerip_checkbox = QCheckBox()
        self.rerip_checkbox.setText("Ask to re-rip url")
        self.rerip_checkbox.setChecked(self.rerip_ask)
        self.live_update_checkbox = QCheckBox()
        self.live_update_checkbox.setText("Live update history table")
        self.live_update_checkbox.setChecked(self.live_update)
        checkbox_row.addWidget(self.rerip_checkbox)
        checkbox_row.addWidget(self.live_update_checkbox)

        vbox.addRow("Save Location:", self.save_folder_label)
        vbox.addRow("Select Save Folder:", save_folder_button)
        vbox.addRow("Load Unfinished Urls:", load_url_button)
        vbox.addRow("Check For Updates:", check_update_hbox)
        vbox.addRow("Clear Cache:", clear_cache_hbox)
        vbox.addRow("Filename Scheme:", self.file_scheme_combobox)
        vbox.addRow("Unzip Protocol:", self.unzip_protocol_combobox)
        vbox.addRow(checkbox_row)
        self.settings_tab.setLayout(vbox)

        # endregion

        tab_widget.addTab(self.log_field, "Logs")
        tab_widget.addTab(self.queue_field, "Queue")
        tab_widget.addTab(self.history_table, "History")
        tab_widget.addTab(self.settings_tab, "Settings")

        # endregion

        form_layout = QFormLayout()
        form_layout.addRow("Enter URL:", first_row)
        form_layout.addRow(tab_widget)
        self.setLayout(form_layout)
        self.setWindowTitle(f"{self.title} {self.version}")

        # endregion

        # region Connect Signals

        stdout.outputWritten.connect(self.redirect_output)
        stderr.outputWritten.connect(self.redirect_output)
        self.update_display.connect(self.update_display_sequence)

        # endregion

        # region Connect Buttons

        self.url_field.returnPressed.connect(self.queue_url)
        rip_button.clicked.connect(self.queue_url)
        self.pause_button.clicked.connect(self.toggle_ripper)
        save_folder_button.clicked.connect(self.set_save_folder)
        load_url_button.clicked.connect(self.load_url_file)
        check_update_button.clicked.connect(self.check_latest_version)
        clear_cache_button.clicked.connect(self.clear_cache)

        # endregion

        # region Connect CheckBoxes

        self.rerip_checkbox.toggled.connect(self.set_rerip)
        self.live_update_checkbox.toggled.connect(self.set_live_update)

        # endregion

        # region Load Data

        self.load_app_data()

        # endregion

    def closeEvent(self, event: QtGui.QCloseEvent):
        self.save_data()

    def redirect_output(self, raw_text: str, stderr: bool):
        self.log_field.moveCursor(QTextCursor.MoveOperation.End)
        text, color = self.extract_color(raw_text)
        self.log_field.setTextColor(color)
        self.log_field.insertPlainText(text)

    @staticmethod
    def extract_color(raw_text: str) -> tuple[str, QColor]:
        if raw_text.startswith("{#"):
            parts = raw_text.split("}")
            text = "}".join(parts[1:])
            color_str = parts[0].replace("{#", "")
            color_value = int(color_str, 16)
            color = QColor.fromRgb(color_value)
            return text, color
        else:
            color = QColor.fromRgb(0)
            return raw_text, color

    def add_history_entry(self, name: str, url: str, date: str, count: int):
        row_pos = self.history_table.rowCount()
        self.history_table.insertRow(row_pos)
        self.history_table.setItem(row_pos, 0, QTableWidgetItem(name))
        self.history_table.setItem(row_pos, 1, QTableWidgetItem(url))
        self.history_table.setItem(row_pos, 2, QTableWidgetItem(date))
        self.history_table.setItem(row_pos, 3, QTableWidgetItem(str(count)))

    def get_history_data(self) -> list[list[str]]:
        row_count: int = self.history_table.rowCount()
        column_count: int = self.history_table.columnCount()
        table_data: list[list[str]] = []
        for row in range(row_count):
            row_data: list[str] = []
            for column in range(column_count):
                row_data.append(self.history_table.item(row, column).text())
            table_data.append(row_data)
        return table_data

    def get_column_data(self, column_index: int) -> list[str]:
        data = []
        for i in range(self.history_table.rowCount()):
            data.append(self.history_table.item(i, column_index).text())
        return data

    def get_row_data(self, row_index: int) -> list[str]:
        data = []
        for i in range(self.history_table.columnCount()):
            data.append(self.history_table.item(row_index, i).text())
        return data

    def load_history(self):
        rip_history = self._load_history()
        for entry in rip_history:
            self.add_history_entry(*entry)

    def queue_url(self):
        raw_urls = self.url_field.text()
        result = self._queue_urls(raw_urls)
        if result == QueueResult.ALREADY_QUEUED:
            self.set_label_text(self.status_label, "Already queued", "green", 2.5)
        elif result == QueueResult.NOT_SUPPORTED:
            self.set_label_text(self.status_label, "Not a support site", "red", 2.5)
        self.url_field.clear()
        self.update_url_queue()
        self.rip_urls_starter()

    def rip_urls(self):
        """
            Rip files from all urls in the url queue
        """
        while self.url_queue.qsize() != 0:
            url = self._rip_url()
            self.update_display.emit(self.ripper, url)
            self.display_sync.acquire()  # Wait until display has updated

    def update_display_sequence(self, ripper: ImageRipper, url: str):
        """
            Update url queue and rip history displays
        :param ripper: ImageRipper that stores information of the completed rip job
        :param url: url of the completed rip job
        """
        self.update_history(ripper, url)
        self.update_url_queue()
        self.display_sync.release()

    def update_history(self, ripper: ImageRipper, url: str):
        """
            Update the history table with a new entry
        :param ripper: ImageRipper that stores information of the completed rip job
        :param url: url of the completed rip job
        """
        duplicate_entry = False
        ripped_urls = self.get_column_data(0)
        for i, entry in enumerate(ripped_urls):
            if entry == ripper.folder_info.dir_name:
                duplicate_entry = True
                row_data = self.get_row_data(i)
                self.add_history_entry(*row_data)
                self.history_table.removeRow(i)
                break
        if not duplicate_entry:
            self.add_history_entry(ripper.folder_info.dir_name, url, str(datetime.today().strftime('%Y-%m-%d')),
                                   ripper.folder_info.num_urls)
        ripper.folder_info = []

    def add_to_url_queue(self, item: str):
        """
            Add a url to the url queue
        :param item: url to add to the url queue
        """
        # If user wants to be prompted and if url is in the history
        if self.rerip_ask and item in self.get_column_data(1):
            if self.popup_yes_no('Do you want to re-rip URL?') == QMessageBox.Yes:  # Ask user to re-rip
                self.url_queue.put(item)
        else:  # If user always wants to re-rip
            self.url_queue.put(item)

    def popup_yes_no(self, message: str) -> QMessageBox.StandardButton:
        """
            Create a popup with yes or no options
        :param message: message to be displayed by the popup
        :return: user selection of QMessageBox.Yes or QMessageBox.No based on option selected
        """
        message_box = QMessageBox()
        return message_box.question(self, '', message, QMessageBox.Yes | QMessageBox.No)

    def update_url_queue(self):
        """
            Update the visual display of the url queue
        """
        self.queue_field.clear()
        for url in self.url_queue.queue:
            self.queue_field.append(url)

    def toggle_ripper(self):
        """
            Toggle the ripper's run/pause state
        """
        if self.status_sync.pause:
            self.pause_button.setIcon(QtGui.QIcon("./Icons/pause.svg"))
            self.status_sync.pause = False
        else:
            self.pause_button.setIcon(QtGui.QIcon("./Icons/play.svg"))
            self.status_sync.pause = True

    def set_rerip(self, value: bool):
        """
            Update the rerip_ask value
        :param value: new value of rerip_ask
        """
        self.rerip_ask = value

    def set_live_update(self, value: bool):
        """
            Update the live_update value
        :param value: new value of live_update
        """
        self.live_update = value

    def set_save_folder(self):
        """
            Set directory where all rips will be saved
        """
        folder = str(QFileDialog.getExistingDirectory(self, "Select Directory", self.save_folder_label.text()))
        self.save_folder_label.setText(folder)
        Config.config['SavePath'] = folder

    def load_url_file(self):
        """
            Queue urls from user selected .json file
        """
        file = QFileDialog.getOpenFileName(self, "Select File", filter="*.json")[0]
        self._load_url_file(file)
        self.update_url_queue()

    def file_scheme_changed(self, new_value: str):
        """
            Update rip filename scheme
        :param new_value: new value for filename_scheme
        """
        self.filename_scheme = FilenameScheme[new_value.upper()]
        Config.config['FilenameScheme'] = self.filename_scheme.name.title()

    def unzip_protocol_changed(self, new_value: str):
        """
            Update what to do with downloaded zip files
        :param new_value: new value for unzip_protocol
        """
        if new_value == "Extract and Delete":
            new_value = "EXTRACT_DELETE"
        self.unzip_protocol = UnzipProtocol[new_value.upper()]
        Config.config['UnzipProtocol'] = self.unzip_protocol.name.title()

    def load_config(self):
        """
            Load application configuration file
        """
        super().load_config()
        self.save_folder_label.setText(self.save_folder)
        self.rerip_checkbox.setChecked(self.rerip_ask)
        self.live_update_checkbox.setChecked(self.live_update)
        self.unzip_protocol_combobox.setCurrentIndex(self.unzip_protocol.value)
        self.file_scheme_combobox.setCurrentIndex(self.filename_scheme.value)

    def check_latest_version(self):
        """
            Check current version against latest version of application
        """
        is_latest_version = self.is_latest_version()

        if is_latest_version:
            self.set_label_text(self.check_update_label, f"{self.version} is the latest version", "green", 5)
        else:
            self.set_label_text(self.check_update_label, "Update available", "red", 5)

    def set_label_text(self, label: QLabel, text: str, color: str = None, display_time: float = 0):
        """
            Set text of a given QLabel. If display_time is specified, the label text will be set for display_time seconds
        :param label: QLabel to update the text of
        :param text: New text to display
        :param color: Color of the new text
        :param display_time: How long (in seconds) the text should be displayed for before being cleared
        """
        label.setText(text)
        if color is not None:
            label.setStyleSheet(f"color: {color}")
        if display_time != 0:
            timer = Timer(display_time, self.clear_label, (label,))
            timer.start()

    @staticmethod
    def clear_label(label: QLabel):
        """
            Clear the text of the given label
        :param label: QLabel to clear the text of
        """
        label.setText("")

class NicheImageRipperCLI(NicheImageRipper):
    def __init__(self):
        super().__init__()
        self.rip_history: list[list[str]] = []

    def get_history_data(self) -> list[list[str]]:
        return self.rip_history

    def load_history(self):
        self.rip_history = self._load_history()

    def update_history(self, ripper: ImageRipper, url: str):
        duplicate_entry = False
        folder_info = ripper.folder_info
        for i, entry in enumerate(self.rip_history):
            if entry[0] == folder_info.dir_name:
                duplicate_entry = True
                self.rip_history.append(entry)
                self.rip_history.pop(i)
                break
        if not duplicate_entry:
            self.rip_history.append([folder_info.dir_name, url, str(datetime.today().strftime('%Y-%m-%d')),
                                     folder_info.num_urls])
        ripper.folder_info = []

    def add_to_url_queue(self, item: str):
        if self.rerip_ask and any(item == entry[0] for entry in self.rip_history):
            if self.query_yes_no('Do you want to re-rip URL?'):
                self.url_queue.put(item)
        else:
            self.url_queue.put(item)

    def rip_urls(self):
        pass

    def run(self):
        pass

    def query_yes_no(self, prompt: str) -> bool:
        prompt = f"{prompt} [y/n]: "
        while True:
            response = input(prompt).lower()
            if response in ("y", "yes"):
                return True
            elif response in ("n", "no"):
                return False


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("-c", "--headless", action="store_true")

    args = parser.parse_args()

    if not sys.stdout.isatty():
        # .pyw file is running, no console
        sys.stdout = open("temp.txt", "w")
        sys.stderr = open("temp.txt", "a")

    if args.headless:
        print("This feature is not yet supported")
    else:
        app = QApplication(sys.argv)
        win = NicheImageRipperGUI()

        apply_stylesheet(app, theme='dark_red.xml')

        win.show()
        sys.exit(app.exec())
