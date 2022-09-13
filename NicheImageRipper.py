import json
import os
import sys

import requests
from PyQt5 import QtWidgets
from PyQt5.QtGui import QFont
from PyQt5.QtWidgets import QApplication, QLineEdit, QWidget, QFormLayout, QPushButton, QHBoxLayout, QVBoxLayout, \
    QTabWidget, QDesktopWidget, QTextEdit, QTableWidget, QTableWidgetItem, QHeaderView

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

        self.log_tab = QTextEdit()
        self.log_tab.setFont(QFont("Arial"))
        self.log_tab.setReadOnly(True)

        # endregion

        # region Queue Tab

        self.queue_tab = QTextEdit()
        self.queue_tab.setFont(QFont("Arial"))
        self.queue_tab.setReadOnly(True)

        # endregion

        # region History Tab

        self.history_tab = QTextEdit()
        self.history_tab.setFont(QFont("Arial"))
        self.history_tab.setReadOnly(True)

        # endregion

        # region Settings Tab

        self.settings_tab = QTextEdit()
        self.settings_tab.setFont(QFont("Arial"))
        self.settings_tab.setReadOnly(True)

        # endregion

        tab_widget.addTab(self.log_tab, "Logs")
        tab_widget.addTab(self.queue_tab, "Queue")
        tab_widget.addTab(self.history_tab, "History")
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

        # endregion

    def queue_url(self):
        self.queue_tab.append(self.url_field.text())
        self.url_field.clear()


if __name__ == "__main__":
    app = QApplication(sys.argv)
    win = NicheImageRipper()
    win.show()
    sys.exit(app.exec_())
