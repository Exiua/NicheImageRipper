import json
import os
import sys

import requests
from PyQt5 import QtCore
from PyQt5.QtGui import QFont
from PyQt5.QtWidgets import QApplication, QLineEdit, QWidget, QFormLayout, QPushButton, QHBoxLayout, QTabWidget, \
    QDesktopWidget, QTextEdit, QTableWidget, QTableWidgetItem, QLabel, QCheckBox, QFileDialog

from rippers import read_config


class NicheImageRipper(QWidget):
    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)
        self.title: str = "NicheImagerRipper"
        self.latest_version: str = self.get_git_version()
        self.version: str = "v3.0.0"
        self.save_folder: str = read_config('DEFAULT', 'SavePath')

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
        first_row.addWidget(self.url_field)
        first_row.addWidget(rip_button)

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

        checkbox_row = QHBoxLayout()
        checkbox_row.setAlignment(QtCore.Qt.AlignLeft)
        self.rerip_checkbox = QCheckBox()
        self.rerip_checkbox.setText("Ask to re-rip url")
        self.live_update_checkbox = QCheckBox()
        self.live_update_checkbox.setText("Live update history table")
        checkbox_row.addWidget(self.rerip_checkbox)
        checkbox_row.addWidget(self.live_update_checkbox)

        vbox.addRow("Save Location:", self.save_folder_label)
        vbox.addRow("Select Save Folder:", save_folder_button)
        vbox.addRow("Load Unfinished Urls:", load_url_button)
        vbox.addRow("Check For Updates:", check_update_hbox)
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

        rip_button.clicked.connect(self.queue_url)
        save_folder_button.clicked.connect(self.set_save_folder)
        load_url_button.clicked.connect(self.load_json_file)
        check_update_button.clicked.connect(self.check_latest_version)

        # endregion

        # region Load Data

        if os.path.isfile('RipHistory.json'):
            self.load_history()

        # endregion

    def add_row(self, name: str, url: str, date: str, count: int):
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
            self.add_row(*entry)

    def queue_url(self):
        self.queue_field.append(self.url_field.text())
        self.url_field.clear()

    def set_save_folder(self):
        folder = str(QFileDialog.getExistingDirectory(self, "Select Directory", self.save_folder_label.text()))
        self.save_folder_label.setText(folder)

    def load_json_file(self):
        file = QFileDialog.getOpenFileName(self, "Select File", filter="*.json")[0]
        print(file)
        
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
        response = requests.get("https://api.github.com/repos/Exiua/NicheImageRipper/releases/latest")
        return response.json()['tag_name']


if __name__ == "__main__":
    app = QApplication(sys.argv)
    win = NicheImageRipper()
    win.show()
    sys.exit(app.exec_())