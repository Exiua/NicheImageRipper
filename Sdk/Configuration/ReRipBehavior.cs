namespace NicheImageRipper.Sdk.Configuration;

/// <summary>What to do when a queued URL is already in the rip history.</summary>
public enum ReRipBehavior
{
    /// <summary>Ask each time.</summary>
    Ask,

    /// <summary>Re-rip without asking.</summary>
    Always,

    /// <summary>Skip it without asking.</summary>
    Never,
}
