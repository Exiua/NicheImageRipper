import os
import subprocess


def main():
    # os.system(r'"C:\Program Files\WinRAR\Rar.exe" a NicheImageRipper.rar ./Icons/ NicheRipper.pyw NicheImageRipper.pyw '
    #           r'Enums.py HtmlParser.py ImageRipper.py requirements.txt RipInfo.py RipperExceptions.py '
    #           r'Util.py StatusSync.py Config.py README.md b_cdn_drm_vod_dl.py ImageLink.py')
    cmd = ["7z", "a", "NicheImageRipper.7z", "./Icons/", "NicheImageRipper.py", "Enums.py", 
    "HtmlParser.py", "ImageRipper.py", "requirements.txt", "RipInfo.py", "RipperExceptions.py", "Util.py", "StatusSync.py", "Config.py", 
    "README.md", "b_cdn_drm_vod_dl.py", "ImageLink.py", "MegaApi.py", "TemporaryTokenManager.py", "UrlUtility.py"]
    subprocess.run(cmd)


if __name__ == "__main__":
    main()
