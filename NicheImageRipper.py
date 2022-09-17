import json
import os
import sys
from queue import Queue

import requests
from PyQt5 import QtCore, QtGui
from PyQt5.QtGui import QFont
from PyQt5.QtWidgets import QApplication, QLineEdit, QWidget, QFormLayout, QPushButton, QHBoxLayout, QTabWidget, \
    QDesktopWidget, QTextEdit, QTableWidget, QTableWidgetItem, QLabel, QCheckBox, QFileDialog, QComboBox, QMessageBox

from FilenameScheme import FilenameScheme
from rippers import read_config, write_config, url_check


class NicheImageRipper(QWidget):
    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)
        self.title: str = "NicheImagerRipper"
        self.latest_version: str = self.get_git_version()
        self.url_queue = Queue()
        self.live_update: bool = False
        self.rerip_ask: bool = True
        self.file_scheme: FilenameScheme = FilenameScheme.ORIGINAL
        self.version: str = "v3.0.0"
        self.save_folder: str = "."

        self.setGeometry(0, 0, 768, 432)

        qtRectangle = self.frameGeometry()
        centerPoint = QDesktopWidget().availableGeometry().center()
        qtRectangle.moveCenter(centerPoint)
        self.move(qtRectangle.topLeft())

        # region UI Construction

        first_row = QHBoxLayout()

        # region First Row Construction

        self.url_field = QLineEdit()
        self.url_field.setFont(QFont("Arial"))
        rip_button = QPushButton("Rip")
        pause_button = QPushButton()
        pause_button.setIcon(QtGui.QIcon("./Icons/pause.svg"))
        self.status_label = QLabel("TestingTestingTestingTesting")
        first_row.addWidget(self.url_field)
        first_row.addWidget(rip_button)
        first_row.addWidget(pause_button)
        first_row.addWidget(self.status_label)

        # endregion

        # region Tab Creation

        tab_widget = QTabWidget()

        # region Log Tab

        self.log_field = QTextEdit()
        self.log_field.setFont(QFont("Arial"))
        self.log_field.setReadOnly(True)

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
        self.file_scheme_combobox.setCurrentIndex(self.file_scheme.value)
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
        save_folder_button.clicked.connect(self.set_save_folder)
        load_url_button.clicked.connect(self.load_json_file)
        check_update_button.clicked.connect(self.check_latest_version)

        # endregion

        # region Load Data

        if os.path.isfile('config.ini'):
            self.load_config()
        if os.path.isfile('RipHistory.json'):
            self.load_history()

        # endregion

    def add_history_entry(self, name: str, url: str, date: str, count: int):
        row_pos = self.history_table.rowCount()
        self.history_table.insertRow(row_pos)
        self.history_table.setItem(row_pos, 0, QTableWidgetItem(name))
        self.history_table.setItem(row_pos, 1, QTableWidgetItem(url))
        self.history_table.setItem(row_pos, 2, QTableWidgetItem(date))
        self.history_table.setItem(row_pos, 3, QTableWidgetItem(str(count)))

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
                if url_check(url) and url not in self.url_queue.queue:
                    self.add_to_url_queue(url)
        self.url_field.clear()
        self.update_url_queue()

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
        else:  # If user always wants to re-rip or duplicate entry was not found
            self.url_queue.put(item)

    def popup_yes_no(self, message: str) -> QMessageBox.StandardButton:
        message_box = QMessageBox()
        return message_box.question(self, '', message, QMessageBox.Yes | QMessageBox.No)

    def get_column_data(self, column_index: int):
        data = []
        for i in range(self.history_table.rowCount()):
            data.append(self.history_table.item(i, column_index).text())
        return data

    def update_url_queue(self):
        self.queue_field.clear()
        for url in self.url_queue.queue:
            self.queue_field.append(url)

    def set_save_folder(self):
        folder = str(QFileDialog.getExistingDirectory(self, "Select Directory", self.save_folder_label.text()))
        self.save_folder_label.setText(folder)
        write_config('DEFAULT', 'SavePath', self.save_folder_label.text())

    def load_json_file(self):
        file = QFileDialog.getOpenFileName(self, "Select File", filter="*.json")[0]
        with open(file, 'r') as load_file:
            loaded_urls = json.load(load_file)
        for url in loaded_urls:
            self.add_to_url_queue(url)
        self.update_url_queue()

    def file_scheme_changed(self, new_value: str):
        self.file_scheme = FilenameScheme[new_value.upper()]
        write_config('DEFAULT', 'FilenameScheme', self.file_scheme.name.title())

    def load_config(self):
        self.save_folder = read_config('DEFAULT', 'SavePath')
        self.save_folder_label.setText(self.save_folder)
        saved_filename_scheme = FilenameScheme[read_config('DEFAULT', 'FilenameScheme').upper()]
        self.file_scheme_combobox.setCurrentIndex(saved_filename_scheme.value)
        self.rerip_ask = read_config('DEFAULT', 'AskToReRip') == "True"
        self.rerip_checkbox.setChecked(self.rerip_ask)
        self.live_update = read_config('DEFAULT', 'LiveHistoryUpdate') == "True"
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
            self.check_update_label.setText(f"{self.version} is the latest version")
            self.check_update_label.setStyleSheet("color: green")
        else:
            self.check_update_label.setText("Update available")
            self.check_update_label.setStyleSheet("color: red")

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
