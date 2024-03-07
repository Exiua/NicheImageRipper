import sys
import datetime
from cProfile import Profile
from pstats import SortKey, Stats

from PyQt6.QtWidgets import QApplication
from qt_material import apply_stylesheet

from NicheImageRipper import NicheImageRipperGUI

def main():
    with Profile() as profile:
        app = QApplication(sys.argv)
        win = NicheImageRipperGUI()

        apply_stylesheet(app, theme='dark_red.xml')
        win.dark_mode = True

        win.show()
        exit_code = app.exec()
        with open("profile.txt", "a") as f:
            f.write(f"--------------------{datetime.datetime.now().isoformat()}--------------------")
            Stats(profile, stream=f).strip_dirs().sort_stats(SortKey.TIME).print_stats()
        sys.exit(exit_code)

if __name__ == "__main__":
    main()