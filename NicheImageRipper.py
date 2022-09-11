import sys

from PyQt5.QtGui import QFont
from PyQt5.QtWidgets import QApplication, QLineEdit, QWidget, QFormLayout, QPushButton, QHBoxLayout, QVBoxLayout, \
    QTabWidget, QDesktopWidget, QTextEdit


class NicheImageRipper(QWidget):
    def __init__(self, parent: QWidget | None = None):
        super().__init__(parent)
        self.title = "NicheImagerRipper"
        self.version = "v3.0.0"
        self.setGeometry(0, 0, 300, 200)

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


if __name__ == "__main__":
    app = QApplication(sys.argv)
    win = NicheImageRipper()
    win.show()
    sys.exit(app.exec_())
