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

    [Fact]
    public async Task ReplaceDocumentFile_UpdatesExistingDocumentWithoutCreatingDuplicate()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DocumentServiceTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        context.DocumentTypes.Add(new DocumentType { DocumentTypeId = 901, TypeName = "Test Document" });
        context.Users.Add(new User { Id = "user-1", UserName = "owner@test.local" });
        context.Documents.Add(new Document
        {
            DocumentId = 50,
            FileName = "original.pdf",
            CurrentStatus = "Rejected",
            UserId = "user-1",
            DocumentTypeId = 901,
            FileSizeBytes = 3,
            DocumentBlob = new DocumentBlob { FileData = new byte[] { 1, 2, 3 }, FileHash = "old", VersionNumber = 1 }
        });
        await context.SaveChangesAsync();

        var scanner = new Mock<IFileScanService>();
        scanner.Setup(x => x.ScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(FileScanResult.Clean());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:SigningKey"] = "test-signing-key-that-is-long-enough-for-jwt-and-aes"
            })
            .Build();
        var repository = new DocumentRepository(context);
        var service = new DocumentService(repository, new AesEncryptionService(), configuration, context, scanner.Object, new Mock<IAuditLogService>().Object);
        var replacement = System.Text.Encoding.UTF8.GetBytes("replacement file");

        var document = await repository.GetDocumentByIdAsync(50);
        await service.ReplaceDocumentFileAsync(document!, "replacement.pdf", replacement, 901);
        await repository.UpdateDocumentAsync(document!);

        var stored = await context.Documents.Include(d => d.DocumentBlob).ThenInclude(b => b.BlobHistories).SingleAsync();
        Assert.Equal(50, stored.DocumentId);
        Assert.Equal("replacement.pdf", stored.FileName);
        Assert.Equal(replacement.Length, stored.FileSizeBytes);
        Assert.Equal(2, stored.DocumentBlob.VersionNumber);
        Assert.Single(stored.DocumentBlob.BlobHistories);
        Assert.Equal(1, await context.DocumentBlobs.CountAsync());
        Assert.Equal(replacement, await service.DownloadDocumentAsync(50, "user-1"));
    }
}
