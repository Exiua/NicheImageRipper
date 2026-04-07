using Core.Configuration;
using JetBrains.Annotations;

namespace GuiThin.Models;

public class GuiThinConfig : GeneralConfig
{
    public string ApiKey { get; set; } = null!;
    public string EndpointUri { get; set; } = null!;
    
    [UsedImplicitly]
    public GuiThinConfig() : base()
    {
        
    }

    protected GuiThinConfig(bool _) : base(_)
    {
        ApiKey = "";
        EndpointUri = "";
    }
}