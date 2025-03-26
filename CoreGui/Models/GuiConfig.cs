using Core.Configuration;
using JetBrains.Annotations;

namespace CoreGui.Models;

public class GuiConfig : GeneralConfig
{
    [UsedImplicitly]
    public GuiConfig() : base()
    {
        
    }

    protected GuiConfig(bool _) : base(_)
    {
        
    }
}