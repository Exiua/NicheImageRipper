namespace Core.Configuration;

public static class ConfigKeys
{
    public static class LoginKeys
    {
        public const string DeviantArt = "DeviantArt";
        public const string Mega = "Mega";
        public const string TitsInTops = "TitsInTops";
        public const string Nijie = "Nijie";
        public const string Danbooru = "Danbooru";
        public const string Gelbooru = "Gelbooru";
        public const string Rule34 = "Rule34";
        public const string Yandere = "Yandere";
        public const string E621 = "E621";
        public const string EHentai = "E-Hentai";
        
        public static readonly string[] All = [
            DeviantArt,
            Mega,
            TitsInTops,
            Nijie,
            Danbooru,
            Gelbooru,
            Rule34,
            Yandere,
            E621,
            EHentai
        ];
    }

    public static class KeyKeys
    {
        public const string Imgur = "Imgur";
        public const string Google = "Google";
        public const string Dropbox = "Dropbox";
        public const string Pixeldrain = "Pixeldrain";
        
        public static readonly string[] All = [
            Imgur,
            Google,
            Dropbox,
            Pixeldrain
        ];
    }
    
    public static class CookieKeys
    {
        public const string Twitter = "Twitter";
        public const string Newgrounds = "Newgrounds";
        public const string Porn3dx = "Porn3dx";
        public const string Pornhub = "Pornhub";
        public const string Thothub = "Thothub";
        public const string Kemono = "Kemono";
        public const string SimpCity = "SimpCity";
        
        public static readonly string[] All = [
            Twitter,
            Newgrounds,
            Porn3dx,
            Pornhub,
            Thothub,
            Kemono,
            SimpCity
        ];
    }

    public static class CustomKeys
    {
        public const string V2PH = "V2PH";
        public const string GoFile = "GoFile";
    }
}