using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CQGAPI.Helpers;

public class AESDecryptor
{
    private readonly ILogger _logger;
    public AESDecryptor(ILogger<AESDecryptor> logger)
    {
        _logger = logger;
    }

    // Inside AESDecryptor
    public string Decrypt(string encryptedText, string key)
    {
        try
        {
            var parts = encryptedText.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException("Invalid encrypted text format");

            byte[] iv = Convert.FromBase64String(parts[0]);
            byte[] cipherText = Convert.FromBase64String(parts[1]);
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);

            if (keyBytes.Length != 32)
                throw new ArgumentException("Key must be 32 bytes (256 bits) long");

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = keyBytes;
                aesAlg.IV = iv;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                using (var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV))
                {
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🔐 Decryption failed");
            return encryptedText;
        }
    }



    public async Task<string> GetEncryptedServiceAccountAsync(string url)
    {
        try
        {
            using var httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("❌ Unauthorized (401). Please check authentication credentials.");
                return null;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("🚫 Forbidden (403). You do not have permission to access this resource.");
                return null;
            }

            string encryptedBase64 = await response.Content.ReadAsStringAsync();
            string encryptedSecret = string.Empty;
            using (JsonDocument doc = JsonDocument.Parse(encryptedBase64))
            {
                encryptedSecret = doc.RootElement.GetProperty("encryptedSecret").GetString();
                _logger.LogInformation("🔒 Encrypted Secret received.");
            }

            _logger.LogInformation("✅ Encrypted service account received.");
            return encryptedSecret;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "🔴 HTTP request failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🔴 Unexpected error");
            return null;
        }
    }



}

