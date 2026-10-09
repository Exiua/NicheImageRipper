namespace NicheImageRipper.SiteModules.Modules.GoFile;

public class GoFileConfig
{
    public required string AccountToken { get; set; }
    public required string LoginLink { get; set; }
        
    // ReSharper disable once MemberHidesStaticFromOuterClass
    public static GoFileConfig New()
    {
        return new GoFileConfig
        {
            AccountToken = "",
            LoginLink = ""
        };
    }
}