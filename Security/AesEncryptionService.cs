using System.Security.Cryptography;

namespace FourierIT_API.Security
{
    public class AesEncryptionService : IEncryptionService
    {
        public byte[] EncryptData(byte[] data, string key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(key);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        public byte[] DecryptData(byte[] encryptedData, string key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(key);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var ms = new MemoryStream(encryptedData))
                {
                    byte[] iv = new byte[aes.BlockSize / 8];

                    var read = ms.Read(iv, 0, iv.Length);
                    if (read != iv.Length)
                        throw new InvalidDataException("Encrypted data does not contain a full IV.");

                    aes.IV = iv;

                    // Read decrypted bytes from the crypto stream (use read mode)
                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        using (var resultStream = new MemoryStream())
                        {
                            cs.CopyTo(resultStream);
                            return resultStream.ToArray();
                        }
                }
            }
        }

        public string GenerateEncryptionKey()
        {
            using (Aes aes = Aes.Create())
            {
                aes.GenerateKey();
                return Convert.ToBase64String(aes.Key);
            }
        }
    }
}
