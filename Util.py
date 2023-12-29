from __future__ import annotations

from urllib.parse import urlparse

from Config import Config

SCHEME: str = "https://"

# Global Variables
logged_in: bool

SUPPORTED_SITES: set[str] = {
    "https://imhentai.xxx/", "https://hotgirl.asia/", "https://www.redpornblog.com/",
    "https://www.cup-e.club/", "https://girlsreleased.com/", "https://www.bustybloom.com/",
    "https://www.morazzia.com/", "https://www.novojoy.com/", "https://www.hqbabes.com/",
    "https://www.silkengirl.com/", "https://www.babesandgirls.com/",
    "https://www.babeimpact.com/", "https://www.100bucksbabes.com/",
    "https://www.sexykittenporn.com/", "https://www.babesbang.com/",
    "https://www.exgirlfriendmarket.com/",
    "https://www.novoporn.com/", "https://www.hottystop.com/",
    "https://www.babeuniversum.com/",
    "https://www.babesandbitches.net/", "https://www.chickteases.com/",
    "https://www.wantedbabes.com/", "https://cyberdrop.me/",
    "https://www.sexy-egirls.com/", "https://www.pleasuregirl.net/",
    "https://www.sexyaporno.com/",
    "https://www.theomegaproject.org/", "https://www.babesmachine.com/",
    "https://www.babesinporn.com/",
    "https://www.livejasminbabes.net/", "https://www.grabpussy.com/",
    "https://www.simply-cosplay.com/",
    "https://www.simply-porn.com/", "https://pmatehunter.com/", "https://www.elitebabes.com/",
    "https://www.xarthunter.com/",
    "https://www.joymiihub.com/", "https://www.metarthunter.com/",
    "https://www.femjoyhunter.com/", "https://www.ftvhunter.com/",
    "https://www.hegrehunter.com/", "https://hanime.tv/", "https://members.hanime.tv/",
    "https://www.babesaround.com/", "https://www.8boobs.com/",
    "https://www.decorativemodels.com/", "https://www.girlsofdesire.org/",
    "http://www.hqsluts.com/", "https://www.foxhq.com/",
    "https://www.rabbitsfun.com/", "https://www.erosberry.com/", "https://www.novohot.com/",
    "https://eahentai.com/",
    "https://www.nightdreambabe.com/", "https://xmissy.nl/", "https://www.glam0ur.com/",
    "https://www.dirtyyoungbitches.com/", "https://www.rossoporn.com/",
    "https://www.nakedgirls.xxx/", "https://www.mainbabes.com/",
    "https://www.hotstunners.com/", "https://www.sexynakeds.com/",
    "https://www.nudity911.com/", "https://www.pbabes.com/",
    "https://www.sexybabesart.com/", "https://www.heymanhustle.com/", "https://sexhd.pics/",
    "http://www.gyrls.com/",
    "https://www.pinkfineart.com/", "https://www.sensualgirls.org/",
    "https://www.novoglam.com/", "https://www.cherrynudes.com/", "https://www.join2babes.com/",
    "https://gofile.io/", "https://www.babecentrum.com/", "http://www.cutegirlporn.com/",
    "https://everia.club/",
    "https://imgbox.com/", "https://nonsummerjack.com/", "https://myhentaigallery.com/",
    "https://buondua.com/", "https://f5girls.com/", "https://hentairox.com/",
    "https://www.redgifs.com/", "https://kemono.su/", "https://www.sankakucomplex.com/",
    "https://www.luscious.net/", "https://sxchinesegirlz.one/", "https://agirlpic.com/",
    "https://www.v2ph.com/",
    "https://nudebird.biz/", "https://bestprettygirl.com/", "https://coomer.su/",
    "https://imgur.com/", "https://www.8kcosplay.com/", "https://www.inven.co.kr/",
    "https://arca.live/",
    "https://www.cool18.com/", "https://maturewoman.xyz/", "https://putmega.com/",
    "https://thotsbay.com/",
    "https://tikhoe.com/", "https://lovefap.com/", "https://comics.8muses.com/",
    "https://www.jkforum.net/",
    "https://leakedbb.com/", "https://e-hentai.org/", "https://jpg.church/",
    "https://www.artstation.com/",
    "https://porn3dx.com/", "https://www.deviantart.com/", "https://readmanganato.com/",
    "https://manganato.com/",
    "https://sfmcompile.club/", "https://www.tsumino.com/", "https://danbooru.donmai.us/",
    "https://www.flickr.com/", "https://rule34.xxx/", "https://titsintops.com/",
    "https://gelbooru.com/", "https://999hentai.to/", "https://fapello.com/",
    "https://nijie.info/", "https://faponic.com/", "https://erothots.co/",
    "https://bitchesgirls.com/", "https://thothub.lol/", "https://influencersgonewild.com/",
    "https://www.erome.com/", "https://ggoorr.net/", "https://drive.google.com/",
    "https://www.dropbox.com/", "https://simpcity.su/", "https://bunkrr.su/",
    "https://omegascans.org/", "https://toonily.me/"
}


def get_login_creds(site_name: str) -> tuple[str, str]:
    login = Config.config.logins[site_name]
    return login["Username"], login["Password"]


def url_check(given_url: str) -> bool:
    """
        Check the url to make sure it is from valid site
    """
    parsed_uri = urlparse(given_url)
    base_url = '{uri.scheme}://{uri.netloc}/'.format(uri=parsed_uri)
    return base_url in SUPPORTED_SITES or "newgrounds.com" in base_url


if __name__ == "__main__":
    pass
