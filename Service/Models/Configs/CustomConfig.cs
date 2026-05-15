namespace Service.Models.Configs;

public class CustomConfig
{
    public V2PHConfig? V2PH { get; set; }
    public GoFileConfig? GoFile { get; set; }
    
    public class V2PHConfig
    {
        public string? Frontend { get; set; }
        public string? FrontendRmt { get; set; }
        public string? CfClearance { get; set; }
    }
    
    public class GoFileConfig
    {
        public string? AccountToken { get; set; }
        public string? LoginLink { get; set; }
    }

    public static CustomConfig FromCoreCustomConfig(Core.Configuration.CustomConfig generalConfigCustom)
    {
        var config = new CustomConfig
        {
            V2PH = new V2PHConfig
            {
                Frontend = generalConfigCustom.V2PH.Frontend,
                FrontendRmt = generalConfigCustom.V2PH.FrontendRmt,
                CfClearance = generalConfigCustom.V2PH.CfClearance,
            },
            GoFile = new GoFileConfig
            {
                AccountToken = generalConfigCustom.GoFile.AccountToken,
                LoginLink = generalConfigCustom.GoFile.LoginLink,
            }
        };
        return config;
    }
}