import sys

from PyQt5.QtWidgets import QApplication, QLineEdit, QWidget, QFormLayout, QPushButton, QHBoxLayout
from PyQt5.QtGui import QIntValidator, QDoubleValidator, QFont, QIcon
from PyQt5.QtCore import pyqtSlot

class NicheImageRipper(QWidget):
    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)
        self.title = "NicheImagerRipper"
        self.version = "v3.0.0"
        self.initUI()

    def initUI(self):
        first_row = QHBoxLayout()

        #region First Row Construction

        url_field = QLineEdit()
        url_field.setFont(QFont("Arial"))
        rip_button = QPushButton("Rip")
        first_row.addWidget(url_field)
        first_row.addWidget(rip_button)

        #endregion

        form_layout = QFormLayout()
        form_layout.addRow("Enter URL:", first_row)

        self.setLayout(form_layout)
        self.setWindowTitle(f"{self.title} {self.version}")

if __name__ == "__main__":
    app = QApplication(sys.argv)
    win = NicheImageRipper()
    win.show()
    sys.exit(app.exec_())