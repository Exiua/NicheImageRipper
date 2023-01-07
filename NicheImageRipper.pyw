from __future__ import annotations

import json
import os
import sys
import threading
from datetime import datetime
from queue import Queue
from threading import Timer

import requests
from PyQt5 import QtCore, QtGui
from PyQt5.QtGui import QFont, QTextCursor
from PyQt5.QtWidgets import QApplication, QLineEdit, QWidget, QFormLayout, QPushButton, QHBoxLayout, QTabWidget, \
    QDesktopWidget, QTextEdit, QTableWidget, QTableWidgetItem, QLabel, QCheckBox, QFileDialog, QComboBox, QMessageBox, \
    QTextBrowser

from Config import Config
from FilenameScheme import FilenameScheme
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


class NicheImageRipper(QWidget):
    display_sync: threading.Semaphore = threading.Semaphore(0)
    update_display: QtCore.pyqtSignal = QtCore.pyqtSignal(ImageRipper, str)

    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)
        self.title: str = "NicheImagerRipper"
        self.latest_version: str = self.get_git_version()
        self.url_queue = Queue()
        self.live_update: bool = False
        self.rerip_ask: bool = True
        self.interrupted: bool = False
        # noinspection PyTypeChecker
        self.ripper: ImageRipper = None
        self.filename_scheme: FilenameScheme = FilenameScheme.ORIGINAL
        self.status_sync: StatusSync = StatusSync()
        self.ripper_thread: threading.Thread = threading.Thread()
        self.version: str = "v2.1.0"
        self.save_folder: str = "."

        ImageRipper.status_sync = self.status_sync

        stdout = OutputRedirect(self, True)
        stderr = OutputRedirect(self, False)

        stdout.outputWritten.connect(self.redirect_output)
        stderr.outputWritten.connect(self.redirect_output)
        self.update_display.connect(self.update_display_sequence)

        self.setGeometry(0, 0, 768, 432)

        qt_rectangle = self.frameGeometry()
        center_point = QDesktopWidget().availableGeometry().center()
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

        save_folder_button = QPushButton()
        save_folder_button.setText("Browse")
        save_folder_button.setFixedWidth(75)

        load_url_button = QPushButton()
        load_url_button.setText("Browse")
        load_url_button.setFixedWidth(75)

        check_update_hbox = QHBoxLayout()
        check_update_button = QPushButton()
        check_update_button.setText("Check")
        check_update_button.setFixedWidth(75)
        self.check_update_label = QLabel()
        check_update_hbox.addWidget(check_update_button)
        check_update_hbox.addWidget(self.check_update_label)

        self.file_scheme_combobox = QComboBox()
        self.file_scheme_combobox.addItems(("Original", "Hash", "Chronological"))
        self.file_scheme_combobox.setFixedWidth(100)
        self.file_scheme_combobox.setCurrentIndex(self.filename_scheme.value)
        self.file_scheme_combobox.currentTextChanged.connect(self.file_scheme_changed)

        checkbox_row = QHBoxLayout()
        checkbox_row.setAlignment(QtCore.Qt.AlignLeft)
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
        vbox.addRow("Filename Scheme:", self.file_scheme_combobox)
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

        # region Connect Buttons

        self.url_field.returnPressed.connect(self.queue_url)
        rip_button.clicked.connect(self.queue_url)
        self.pause_button.clicked.connect(self.toggle_ripper)
        save_folder_button.clicked.connect(self.set_save_folder)
        load_url_button.clicked.connect(self.load_json_file)
        check_update_button.clicked.connect(self.check_latest_version)

        # endregion

        # region Connect CheckBoxes

        self.rerip_checkbox.toggled.connect(self.set_rerip)
        self.live_update_checkbox.toggled.connect(self.set_live_update)

        # endregion

        # region Load Data

        if os.path.isfile('config.json'):
            self.load_config()
        if os.path.isfile('RipHistory.json'):
            self.load_history()

        # endregion

    def closeEvent(self, event: QtGui.QCloseEvent):
        # self.save_to_json('RipHistory.json', self.get_history_data())  # Save history data
        if self.url_queue.not_empty:
            self.save_to_json('UnfinishedRips.json', list(self.url_queue.queue))  # Save queued urls
        if self.interrupted and self.ripper.current_index > 1:
            with open(".ripIndex", "w") as f:
                f.write(str(self.ripper.current_index))
        self.save_to_json('RipHistory.json', self.get_history_data())
        Config.config['SavePath'] = self.save_folder  # Update the config
        # Config.config['DEFAULT', 'Theme'] = self.theme_color
        Config.config['FilenameScheme'] = self.filename_scheme.name.title()
        Config.config['AskToReRip'] = self.rerip_ask
        Config.config['LiveHistoryUpdate'] = self.live_update
        # Config.config['DEFAULT', 'NumberOfThreads'] = str(self.max_threads)

    def redirect_output(self, text: str, stderr: bool):
        self.log_field.moveCursor(QTextCursor.End)
        self.log_field.insertPlainText(text)

    @staticmethod
    def save_to_json(file_name: str, data: any):
        """Save data to json file"""
        with open(file_name, 'w') as save_file:
            json.dump(data, save_file, indent=4)

    def add_history_entry(self, name: str, url: str, date: str, count: int):
        row_pos = self.history_table.rowCount()
        self.history_table.insertRow(row_pos)
        self.history_table.setItem(row_pos, 0, QTableWidgetItem(name))
        self.history_table.setItem(row_pos, 1, QTableWidgetItem(url))
        self.history_table.setItem(row_pos, 2, QTableWidgetItem(date))
        self.history_table.setItem(row_pos, 3, QTableWidgetItem(str(count)))

    def get_history_data(self) -> list[list[str]]:
        row_count = self.history_table.rowCount()
        column_count = self.history_table.columnCount()
        table_data = []
        for row in range(row_count):
            row_data = []
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
        with open("RipHistory.json", 'r') as load_file:
            rip_history: list[list[str]] = json.load(load_file)
        for entry in rip_history:
            self.add_history_entry(*entry)

    def queue_url(self):
        raw_urls = self.url_field.text()
        url_list = self.separate_string(raw_urls, "https://")
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
                        self.set_label_text(self.status_label, "Already queued", "green", 2.5)
                else:
                    self.set_label_text(self.status_label, "Not a support site", "red", 2.5)
        self.url_field.clear()
        self.update_url_queue()
        self.rip_urls_starter()

    def rip_urls_starter(self):
        if not self.ripper_thread.is_alive() and self.url_queue.qsize() != 0:
            self.ripper_thread = threading.Thread(target=self.rip_urls, daemon=True)
            self.ripper_thread.start()

    def rip_urls(self):
        """Rips files from urls"""
        while self.url_queue.qsize() != 0:
            url = self.url_queue.queue[0]
            print(url)
            self.ripper = ImageRipper(self.filename_scheme)
            self.interrupted = True
            self.ripper.rip(url)
            self.interrupted = False
            self.url_queue.get()
            self.update_display.emit(self.ripper, url)
            self.display_sync.acquire()

    def update_display_sequence(self, ripper: ImageRipper, url: str):
        self.update_history(ripper, url)
        self.update_url_queue()
        self.display_sync.release()

    def update_history(self, ripper: ImageRipper, url: str):
        """Update the table with new values"""
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

    @staticmethod
    def separate_string(base_string: str, delimiter: str) -> list[str]:
        string_list = base_string.split(delimiter)  # Split by delimiter
        if string_list[0] == "":
            string_list.pop(0)
        string_list = ["".join([delimiter, string.strip()]) for string in string_list]
        return string_list

    def add_to_url_queue(self, item: str):
        # If user wants to be prompted and if url is in the history
        if self.rerip_ask and item in self.get_column_data(1):
            if self.popup_yes_no('Do you want to re-rip URL?') == QMessageBox.Yes:  # Ask user to re-rip
                self.url_queue.put(item)
        else:  # If user always wants to re-rip
            self.url_queue.put(item)

    def popup_yes_no(self, message: str) -> QMessageBox.StandardButton:
        message_box = QMessageBox()
        return message_box.question(self, '', message, QMessageBox.Yes | QMessageBox.No)

    def update_url_queue(self):
        self.queue_field.clear()
        for url in self.url_queue.queue:
            self.queue_field.append(url)

    def toggle_ripper(self):
        if self.status_sync.pause:
            self.pause_button.setIcon(QtGui.QIcon("./Icons/pause.svg"))
            self.status_sync.pause = False
        else:
            self.pause_button.setIcon(QtGui.QIcon("./Icons/play.svg"))
            self.status_sync.pause = True

    def set_rerip(self, value: bool):
        self.rerip_ask = value

    def set_live_update(self, value: bool):
        self.live_update = value

    def set_save_folder(self):
        folder = str(QFileDialog.getExistingDirectory(self, "Select Directory", self.save_folder_label.text()))
        self.save_folder_label.setText(folder)
        Config.config['SavePath'] = folder

    def load_json_file(self):
        file = QFileDialog.getOpenFileName(self, "Select File", filter="*.json")[0]
        with open(file, 'r') as load_file:
            loaded_urls = json.load(load_file)
        for url in loaded_urls:
            self.add_to_url_queue(url)
        self.update_url_queue()

    def file_scheme_changed(self, new_value: str):
        self.filename_scheme = FilenameScheme[new_value.upper()]
        Config.config['FilenameScheme'] = self.filename_scheme.name.title()

    def load_config(self):
        self.save_folder = Config.config['SavePath']
        self.save_folder_label.setText(self.save_folder)
        saved_filename_scheme = FilenameScheme[Config.config['FilenameScheme'].upper()]
        self.file_scheme_combobox.setCurrentIndex(saved_filename_scheme.value)
        self.rerip_ask = Config.config['AskToReRip']
        self.rerip_checkbox.setChecked(self.rerip_ask)
        self.live_update = Config.config['LiveHistoryUpdate']
        self.live_update_checkbox.setChecked(self.live_update)

    def check_latest_version(self):
        v1 = self.version.replace("v", "").split(".")
        v2 = self.latest_version.replace("v", "").split(".")

        if int(v1[0]) == int(v2[0]):
            if int(v1[1]) == int(v2[1]):
                is_latest_version = int(v1[2]) >= int(v2[2])
            else:
                is_latest_version = int(v1[1]) > int(v2[1])
        else:
            is_latest_version = int(v1[0]) > int(v2[0])

        if is_latest_version:
            self.set_label_text(self.check_update_label, f"{self.version} is the latest version", "green", 5)
        else:
            self.set_label_text(self.check_update_label, "Update available", "red", 5)

    def set_label_text(self, label: QLabel, text: str, color: str = None, display_time: float = 0):
        label.setText(text)
        if color is not None:
            label.setStyleSheet(f"color: {color}")
        if display_time != 0:
            timer = Timer(display_time, self.clear_label, (label,))
            timer.start()

    @staticmethod
    def clear_label(label: QLabel):
        label.setText("")

    @staticmethod
    def get_git_version() -> str:
        try:
            response = requests.get("https://api.github.com/repos/Exiua/NicheImageRipper/releases/latest")
        except requests.exceptions.ConnectionError:
            return "v0.0.0"
        return response.json()['tag_name']


if __name__ == "__main__":
    app = QApplication(sys.argv)
    win = NicheImageRipper()
    win.show()
    sys.exit(app.exec_())
