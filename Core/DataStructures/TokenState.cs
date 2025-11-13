using Core.Enums;

namespace Core.DataStructures;

public class TokenState
{
    public TokenStateTokens Tokens { get; } = new();
    public TokenStateRotations Rotations { get; } = new();

    public class TokenStateTokens
    {
        public Token Redgifs { get; set; }

        public TokenStateTokens()
        {
            Redgifs = new Token("", DateTime.MinValue);
        }
        
        public bool TryGetValue(TokenKey key, out Token? token)
        {
            switch (key)
            {
                case TokenKey.Redgifs:
                    token = Redgifs;
                    return true;
                default:
                    token = null;
                    return false;
            }
        }
    }
    
    public class TokenStateRotations
    {
        public TokenRotation Pixiv { get; set; } = new();

        public bool TryGetValue(RotationKey key, out TokenRotation? token)
        {
            switch (key)
            {
                case RotationKey.Pixiv:
                    token = Pixiv;
                    return true;
                default:
                    token = null;
                    return false;
            }
        }
    }
}