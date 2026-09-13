using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.Managers;

/// <summary>
/// File-backed store for link lists cached by <see cref="SiteParsing.TimeSensitiveHtmlParser"/> subclasses
/// during their original <c>Parse()</c> call, so a later <c>UpdateLinks</c> refresh can resume from them.
/// </summary>
public sealed class TimeSensitiveParserStateManager : ITimeSensitiveParserStateManager
{
    private const string StatePath = "time_sensitive_parser_state.json";

    public static ITimeSensitiveParserStateManager Instance { get; } = new TimeSensitiveParserStateManager();

    private Dictionary<string, List<string>> State { get; }

    private TimeSensitiveParserStateManager()
    {
        State = File.Exists(StatePath)
            ? JsonUtility.Deserialize<Dictionary<string, List<string>>>(StatePath) ??
              new Dictionary<string, List<string>>()
            : new Dictionary<string, List<string>>();
    }

    public void StoreLinks(string parserKey, string ripUrl, IReadOnlyList<string> links)
    {
        State[Key(parserKey, ripUrl)] = links.ToList();
        Save();
    }

    public List<string>? GetLinks(string parserKey, string ripUrl) =>
        State.GetValueOrDefault(Key(parserKey, ripUrl));

    private static string Key(string parserKey, string ripUrl) => $"{parserKey}:{ripUrl}";

    private void Save() => JsonUtility.Serialize(StatePath, State);
}