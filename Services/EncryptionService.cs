using System.Security.Cryptography;
using DevHabit.Api.Settings;
using Microsoft.Extensions.Options;

namespace DevHabit.Api.Services;

public sealed class EncryptionService(IOptions<EncryptionOptions> options)
{
    private readonly byte[] _masterKey = Convert.FromBase64String(options.Value.Key);
    private const int IvSize = 16; // AES block size is 16 bytes
    public string Encrypt(string plainText)
    {
        try {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _masterKey;
            aes.IV = RandomNumberGenerator.GetBytes(IvSize);

            using var memoryStream = new MemoryStream();
            memoryStream.Write(aes.IV, 0, IvSize); // Prepend IV to the ciphertext

            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
            using (var streamWriter = new StreamWriter(cryptoStream))
            {
                streamWriter.Write(plainText);
            }

            return Convert.ToBase64String(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            // Handle or log the exception as needed
            throw new InvalidOperationException("An error occurred during encryption.", ex);
        }
    }

    public string Decrypt(string cipherText)
    {
        try {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            if (cipherBytes.Length < IvSize)
            {
                throw new ArgumentException("Invalid ciphertext.");
            }
            // Extract the IV from the beginning of the ciphertext
            byte[] iv = new byte[IvSize];
            byte[] encryptedData = new byte[cipherBytes.Length - IvSize];
            Buffer.BlockCopy(cipherBytes, 0, iv, 0, IvSize);
            Buffer.BlockCopy(cipherBytes, IvSize, encryptedData, 0, encryptedData.Length);

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _masterKey;
            aes.IV = iv;

            using var memoryStream = new MemoryStream(encryptedData);
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream);
            
            return streamReader.ReadToEnd();
        }
        catch (Exception ex)
        {
            // Handle or log the exception as needed
            throw new InvalidOperationException("An error occurred during decryption.", ex);
        }
    }
}
