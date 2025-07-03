using System;
using System.IO;
using System.Security.Cryptography;

namespace Convey.Security.Internals;

internal sealed class Encryptor : IEncryptor
{
    public byte[] Encrypt(byte[] data, byte[] iv, byte[] key)
    {
        if (data is null || data.Length == 0)
        {
            throw new ArgumentException("Data to be encrypted cannot be empty.", nameof(data));
        }

        if (iv is null || iv.Length == 0)
        {
            throw new ArgumentException("Initialization vector cannot be empty.", nameof(iv));
        }

        if (key is null || key.Length == 0)
        {
            throw new ArgumentException("Encryption key cannot be empty.", nameof(key));
        }

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        var transform = aes.CreateEncryptor(aes.Key, aes.IV);
        using var memoryStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);
        cryptoStream.Write(data, 0, data.Length);
        cryptoStream.FlushFinalBlock();

        return memoryStream.ToArray();
    }

    public byte[] Decrypt(byte[] data, byte[] iv, byte[] key)
    {
        if (data is null || data.Length == 0)
        {
            throw new ArgumentException("Data to be decrypted cannot be empty.", nameof(data));
        }

        if (iv is null || iv.Length == 0)
        {
            throw new ArgumentException("Initialization vector cannot be empty.", nameof(iv));
        }

        if (key is null || key.Length == 0)
        {
            throw new ArgumentException("Encryption key cannot be empty.", nameof(key));
        }

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        var transform = aes.CreateDecryptor(aes.Key, aes.IV);
        using var memoryStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);
        cryptoStream.Write(data, 0, data.Length);
        cryptoStream.FlushFinalBlock();

        return memoryStream.ToArray();
    }
}