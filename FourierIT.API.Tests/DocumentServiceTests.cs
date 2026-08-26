using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Repositories;
using FourierIT_API.Security;
using FourierIT_API.Service;
using FourierIT_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace FourierIT.API.Tests;

public class DocumentServiceTests
{
    [Fact]
    public async Task UploadThenDownload_RoundTripsContentAndSetsStatus()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentServiceTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 901, TypeName = "Test Document" });
        await context.SaveChangesAsync();

        var scanner = new Mock<IFileScanService>();
        scanner.Setup(x => x.ScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(FileScanResult.Clean());
        var audit = new Mock<IAuditLogService>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:SigningKey"] = "test-signing-key-that-is-long-enough-for-jwt-and-aes"
            })
            .Build();
        var repository = new DocumentRepository(context);
        var encryption = new AesEncryptionService();
        var service = new DocumentService(repository, encryption, configuration, context, scanner.Object, audit.Object);
        var content = System.Text.Encoding.UTF8.GetBytes("document round trip");

        var uploaded = await service.UploadDocumentAsync("user-1", "round-trip.pdf", content, 901);
        var downloaded = await service.DownloadDocumentAsync(uploaded.DocumentId, "user-1");
        var stored = await context.Documents.SingleAsync(d => d.DocumentId == uploaded.DocumentId);

        Assert.Equal("Uploaded", stored.CurrentStatus);
        Assert.Equal(content, downloaded);
    }
}
