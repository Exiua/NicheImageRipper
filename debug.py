from platform import version
import sys
import requests

requests_header = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.190 Safari/537.36',
                    'referer': 'https://kemono.party/',
                    'cookie': '__ddgid=8OYPBCcijNqNLFPG; __ddg2=jJBBC0uFUQodvYkW; __ddg1=7H91n5MBCH1UanO5mMhw'
                    }

def download_file(rip_url: str):
    with open("test.gif", "wb") as handle:    
        response = requests.get(rip_url, headers=requests_header, stream=True)
        if not response.ok:
            print(response)
        for block in response.iter_content(chunk_size=50000):
            if not block:
                break
            handle.write(block)

def get_latests_repo_version():
    response = requests.get("https://api.github.com/repos/Exiua/NicheImageRipper/releases/latest")
    version = response.json()['tag_name']
    print('v2.7.9' < version)

if __name__ == "__main__":
    #download_file(sys.argv[1])
    get_latests_repo_version()