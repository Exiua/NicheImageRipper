namespace Core.Configuration;

public class CustomConfig
{
    public V2PHConfig V2PH { get; set; } = null!;
    public GoFileConfig GoFile { get; set; } = null!;
    
    public static CustomConfig New()
    {
        return new CustomConfig
        {
            V2PH = V2PHConfig.New(),
            GoFile = GoFileConfig.New()
        };
    }
    
    public class V2PHConfig
    {
        public required string Frontend { get; set; }
        public required string FrontendRmt { get; set; }
        public required string CfClearance { get; set; }
        
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static V2PHConfig New()
        {
            return new V2PHConfig
            {
                Frontend = "",
                FrontendRmt = "",
                CfClearance = ""
            };
        }
    }
    
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
}