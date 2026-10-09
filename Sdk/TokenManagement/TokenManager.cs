using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.TokenManagement;

/// <summary>
///     Class for managing tokens that are dynamically generated (e.g., redgif tokens)
/// </summary>
public class TokenManager
{
    private const string TokenPath = "temp_tokens.json";

    public static TokenManager Instance { get; } = new();

    private TokenState State { get; }

    private TokenManager()
    {
        State = File.Exists(TokenPath)
            ? JsonUtility.Deserialize<TokenState>(TokenPath)!
            : new TokenState();
    }

    private void Save() => JsonUtility.Serialize(TokenPath, State);

    /// <summary>
    /// Returns a cached, non-expired token for <paramref name="key"/>, or calls
    /// <paramref name="generate"/> to produce and cache a new one.
    /// </summary>
    public async Task<Token> GetOrGenerate(
        string key,
        Func<CancellationToken, Task<Token>> generate,
        CancellationToken ct = default)
    {
        if (State.Tokens.TryGetValue(key, out var token) && token.Expiration >= DateTime.Now)
        {
            return token;
        }

        token = await generate(ct);
        State.Tokens[key] = token;
        Save();
        return token;
    }

    /// <summary>
    /// Returns the current token in a rotation, advancing to the next one if
    /// <paramref name="maxDuration"/> of use has elapsed since the last advance.
    /// </summary>
    public string GetWithRotation(string key, TimeSpan maxDuration, string[] tokens)
    {
        var rotation = State.Rotations.TryGetValue(key, out var rot)
            ? rot
            : State.Rotations[key] = new TokenRotation();

        var elapsed = rotation.Paused ? TimeSpan.Zero : DateTime.Now - rotation.LastUsed;
        var usedDuration = elapsed + rotation.UsedDuration;

        int currentIndex;
        if (usedDuration >= maxDuration)
        {
            rotation.UsedDuration = TimeSpan.Zero;
            currentIndex = (rotation.CurrentIndex + 1) % tokens.Length;
            rotation.CurrentIndex = currentIndex;
        }
        else
        {
            rotation.UsedDuration = usedDuration;
            currentIndex = rotation.CurrentIndex;
        }

        rotation.LastUsed = DateTime.Now;
        rotation.Paused = false;
        Save();
        return tokens[currentIndex];
    }

    /// <summary>
    /// Marks a rotation as paused (e.g. on rate-limit) so elapsed time isn't
    /// counted against it until it's used again.
    /// </summary>
    public void PauseRotation(string key)
    {
        if (!State.Rotations.TryGetValue(key, out var rotation))
        {
            return;
        }

        rotation.UsedDuration += DateTime.Now - rotation.LastUsed;
        rotation.LastUsed = DateTime.Now;
        rotation.Paused = true;
        Save();
    }
}