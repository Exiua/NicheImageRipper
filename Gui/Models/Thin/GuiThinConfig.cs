using Core.Configuration;
using JetBrains.Annotations;

namespace Gui.Models.Thin;

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