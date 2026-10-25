namespace ImpersonateClient;

public sealed class ImpersonateHttpHeaders
{
    private readonly Dictionary<string, List<string>> _headers =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> All =>
        _headers.ToDictionary(
            pair => pair.Key, IReadOnlyList<string> (pair) => pair.Value,
            StringComparer.OrdinalIgnoreCase);

    public void Add(string name, string value)
    {
        if (!_headers.TryGetValue(name, out var values))
        {
            values = [];
            _headers[name] = values;
        }

        values.Add(value);
    }

    public bool TryGetValues(string name, out IReadOnlyList<string> values)
    {
        if (_headers.TryGetValue(name, out var existing))
        {
            values = existing;
            return true;
        }

        values = [];
        return false;
    }

    public string? GetFirstOrDefault(string name)
    {
        return _headers.TryGetValue(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}