using FourierIT_API.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FourierIT_API.Services
{
    public class FileScanService : IFileScanService
    {
        private readonly ILogger<FileScanService> _logger;
        private readonly string _clamHost;
        private readonly int _clamPort;
        private readonly bool _clamEnabled;
        private readonly bool _failOpenOnConnectionError;

        public FileScanService(IConfiguration configuration, ILogger<FileScanService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clamHost = configuration["ClamAV:Host"] ?? "127.0.0.1";
            _clamPort = int.TryParse(configuration["ClamAV:Port"], out var port) && port > 0 ? port : 3310;
            _clamEnabled = configuration.GetValue("ClamAV:Enabled", true);
            _failOpenOnConnectionError = configuration.GetValue("ClamAV:FailOpenOnConnectionError", false);
        }

        public async Task<FileScanResult> ScanFileAsync(Stream file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (!file.CanSeek)
            {
                var buffer = new MemoryStream();
                await file.CopyToAsync(buffer);
                buffer.Position = 0;
                file = buffer;
            }

            file.Position = 0;
            var headerValidation = await ValidateMagicBytesAsync(file);
            if (!headerValidation.IsClean)
            {
                return headerValidation;
            }

            if (!_clamEnabled)
            {
                _logger.LogWarning("Antivirus scanning is disabled by configuration. Upload will proceed without ClamAV scan.");
                return FileScanResult.Clean("Antivirus scanning is disabled.");
            }

            file.Position = 0;
            return await ScanWithClamAvAsync(file);
        }

        private static readonly (byte[] Signature, int Offset)[] KnownSignatures =
        {
            (new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, 0), // %PDF-
            (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0), // PNG
            (new byte[] { 0xFF, 0xD8, 0xFF }, 0), // JPEG
            (new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, 0), // GIF87a
            (new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, 0), // GIF89a
            (new byte[] { 0x49, 0x49, 0x2A, 0x00 }, 0), // TIFF little-endian
            (new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, 0), // TIFF big-endian
            (new byte[] { 0x50, 0x4B, 0x03, 0x04 }, 0), // ZIP / DOCX / XLSX / PPTX
            (new byte[] { 0x57, 0x45, 0x42, 0x50 }, 8), // WEBP
        };

        private async Task<FileScanResult> ValidateMagicBytesAsync(Stream file)
        {
            var header = new byte[16];
            var bytesRead = await file.ReadAsync(header, 0, header.Length);
            file.Position = 0;

            if (bytesRead == 0)
            {
                return FileScanResult.Error("Uploaded file is empty.");
            }

            foreach (var (signature, offset) in KnownSignatures)
            {
                if (bytesRead >= offset + signature.Length)
                {
                    var match = true;
                    for (var i = 0; i < signature.Length; i++)
                    {
                        if (header[offset + i] != signature[i])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        return FileScanResult.Clean("File signature is valid.");
                    }
                }
            }

            if (IsLikelyText(header, bytesRead))
            {
                return FileScanResult.Clean("File appears to be a valid text document.");
            }

            return FileScanResult.Error("Unsupported or invalid file signature.");
        }

        private static bool IsLikelyText(byte[] data, int length)
        {
            if (length == 0) return false;

            var printableCount = 0;
            for (var i = 0; i < length; i++)
            {
                var value = data[i];
                if (value == 9 || value == 10 || value == 13 || (value >= 32 && value <= 126))
                {
                    printableCount++;
                }
                else if (value == 0)
                {
                    return false;
                }
            }

            return printableCount >= length * 0.9;
        }

        private async Task<FileScanResult> ScanWithClamAvAsync(Stream file)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(_clamHost, _clamPort);

                using var networkStream = client.GetStream();
                networkStream.ReadTimeout = 15000;
                networkStream.WriteTimeout = 15000;

                var command = Encoding.ASCII.GetBytes("zINSTREAM\0");
                await networkStream.WriteAsync(command.AsMemory(0, command.Length));

                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await file.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    var lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytesRead));
                    await networkStream.WriteAsync(lengthBytes.AsMemory(0, lengthBytes.Length));
                    await networkStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                }

                var terminationBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(0));
                await networkStream.WriteAsync(terminationBytes.AsMemory(0, terminationBytes.Length));
                await networkStream.FlushAsync();

                using var reader = new StreamReader(networkStream, Encoding.ASCII, leaveOpen: true);
                var response = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(response))
                {
                    return FileScanResult.Error("No response from ClamAV.");
                }

                if (response.Contains("OK", StringComparison.OrdinalIgnoreCase))
                {
                    return FileScanResult.Clean("ClamAV scan passed.");
                }

                if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    return FileScanResult.Infected($"File scan detected malware: {response.Trim()}");
                }

                return FileScanResult.Error($"Unexpected ClamAV response: {response.Trim()}");
            }
            catch (SocketException ex)
            {
                if (_failOpenOnConnectionError)
                {
                    _logger.LogWarning(ex, "ClamAV is unavailable at {Host}:{Port}. Upload will proceed because FailOpenOnConnectionError is enabled.", _clamHost, _clamPort);
                    return FileScanResult.Clean("Antivirus service unavailable; upload allowed in current configuration.");
                }

                _logger.LogError(ex, "Failed to connect to ClamAV at {Host}:{Port}.", _clamHost, _clamPort);
                return FileScanResult.Error("Failed to connect to antivirus service.");
            }
            catch (Exception ex)
            {
                if (_failOpenOnConnectionError)
                {
                    _logger.LogWarning(ex, "Unexpected ClamAV error at {Host}:{Port}. Upload will proceed because FailOpenOnConnectionError is enabled.", _clamHost, _clamPort);
                    return FileScanResult.Clean("Antivirus scan failed; upload allowed in current configuration.");
                }

                _logger.LogError(ex, "Unexpected error while scanning file with ClamAV.");
                return FileScanResult.Error("An error occurred while scanning the uploaded file.");
            }
        }
    }
}
