using NicheImageRipper.Sdk.DataStructures;

namespace NicheImageRipper.Sdk.TokenManagement;

public class TokenState
{
    public Dictionary<string, Token> Tokens { get; init; } = new();
    public Dictionary<string, TokenRotation> Rotations { get; init; } = new();
}