namespace Sdk.Enums;

public enum DownloadMode
{
    List,          // per-item FileLink list — today's default path
    Generate,      // numeric-increment manual generation (imhentai/hentairox style)
    ExternalTool,  // whole-rip delegated to an external tool (deviantart/gallery-dl)
}