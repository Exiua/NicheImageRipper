import os


def main():
    os.system(r'"C:\Program Files\WinRAR\Rar.exe" a NicheImageRipper.rar ./Icons/ NicheRipper.pyw NicheImageRipper.pyw '
              r'Enums.py HtmlParser.py ImageRipper.py requirements.txt RipInfo.py RipperExceptions.py '
              r'Util.py StatusSync.py Config.py README.md')


if __name__ == "__main__":
    main()
