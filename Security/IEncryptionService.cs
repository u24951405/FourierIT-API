namespace FourierIT_API.Security
{
    public interface IEncryptionService
    {
        byte[] EncryptData(byte[] data, string key);
        byte[] DecryptData(byte[] encryptedData, string key);
        string GenerateEncryptionKey();
    }
}
