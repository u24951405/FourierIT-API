namespace FourierIT_API.Models
{
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string FromAddress { get; set; } = "lathithailitha@gmail.com";
        public string FromDisplayName { get; set; } = "DocuVault";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
    }
}
