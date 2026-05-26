using System.Security.Cryptography;
using System.Text;

namespace AutoScreenshot.Services;

/// <summary>Windows DPAPI でテキストを暗号化/復号するヘルパー (NF-04)</summary>
public static class DpapiHelper
{
    /// <summary>平文を DPAPI で暗号化し Base64 文字列で返す。空文字の場合は空文字を返す。</summary>
    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        byte[] data      = Encoding.UTF8.GetBytes(plainText);
        byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>Base64 暗号文を DPAPI で復号して平文を返す。失敗/空の場合は空文字を返す。</summary>
    public static string Unprotect(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        try
        {
            byte[] data      = Convert.FromBase64String(cipherText);
            byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return "";
        }
    }
}
