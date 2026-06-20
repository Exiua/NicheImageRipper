using System.Text;

namespace NicheImageRipper.Core.Utility;

public static class ObfuscationUtility
{
    public static string DeobfuscateJpg5Href(string encryptedString)
    {
        var key = "seltilovessimpcity@simpcityhatesscrapers"u8.ToArray();
        var encrypted = Convert.FromBase64String(encryptedString);
        var encryptedHex = Encoding.UTF8.GetString(encrypted);
        encrypted = Convert.FromHexString(encryptedHex);
        
        var div = key.Length;
        var decrypted = new byte[encrypted.Length];
        for (var i = 0; i < encrypted.Length; i++)
        {
            decrypted[i] = (byte)(encrypted[i] ^ key[i % div]);
        }
        
        var decryptedString = Encoding.UTF8.GetString(decrypted);
        return decryptedString;
    }
}