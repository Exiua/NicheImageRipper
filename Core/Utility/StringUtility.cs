using System.Security.Cryptography;
using System.Text;

namespace Core.Utility;

public static class StringUtility
{
    public static string HashStringMd5(string input)
    {
        return BytesToString(MD5.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private static string BytesToString(byte[] bytes)
    {
        var sb = new StringBuilder(32);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("X2"));
        }
        
        return sb.ToString();
    }
}