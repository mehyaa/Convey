namespace Convey.Security;

// AES-256
public interface IEncryptor
{
    byte[] Encrypt(byte[] data, byte[] iv, byte[] key);
    byte[] Decrypt(byte[] data, byte[] iv, byte[] key);
}