using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Document;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

public class DocumentControllerTests
{
    [Fact]
    public async Task GetDocumentTypesForMyEntity_DepartmentAdmin_ReturnsCompanyOnlyDocumentTypes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"DocumentControllerTests_Db_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var user = new User { Id = "admin-1", UserName = "admin@test.com", DepartmentId = 1 };
        context.Users.Add(user);

        var documentTypeCorporate = new DocumentType { DocumentTypeId = 90011, TypeName = "Certificate of Incorporation" };
        var documentTypeIndividual = new DocumentType { DocumentTypeId = 90001, TypeName = "South African ID Book" };
        var departmentType = new DepartmentDocumentType { DepartmentDocumentTypeId = 1, DepartmentId = 1, DocumentTypeId = 90011, IsMandatory = true, DocumentType = documentTypeCorporate };
        var departmentIndividualType = new DepartmentDocumentType { DepartmentDocumentTypeId = 2, DepartmentId = 1, DocumentTypeId = 90001, IsMandatory = true, DocumentType = documentTypeIndividual };
        var requiredDocumentCorporate = new RequiredDocument { RequiredDocumentId = 90001, EntityTypeId = 3, DocumentTypeId = 90011, IsMandatory = true, Description = "Certificate of incorporation required" };

        context.Departments.Add(new Department { DepartmentId = 1, DepartmentName = "Fourier IT Innovation", BranchId = 1 });
        context.DocumentTypes.AddRange(documentTypeCorporate, documentTypeIndividual);
        context.DepartmentDocumentTypes.AddRange(departmentType, departmentIndividualType);
        context.RequiredDocuments.Add(requiredDocumentCorporate);
        await context.SaveChangesAsync();

        var userStore = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(MockBehavior.Loose,
            userStore.Object, Options.Create(new IdentityOptions()), new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(), Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null, NullLogger<UserManager<User>>.Instance);
        userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        userManagerMock.Setup(x => x.IsInRoleAsync(user, "Department Admin"))
            .ReturnsAsync(true);

        var documentServiceMock = new Mock<FourierIT_API.Interfaces.IDocumentService>();
        var documentRepositoryMock = new Mock<FourierIT_API.Interfaces.IDocumentRepository>();
        var complianceServiceMock = new Mock<FourierIT_API.Interfaces.IComplianceService>();
        var loggerMock = new Mock<ILogger<DocumentController>>();

        var controller = new DocumentController(documentServiceMock.Object, documentRepositoryMock.Object, userManagerMock.Object, context, complianceServiceMock.Object, loggerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() }
        };

        var result = await controller.GetDocumentTypesForMyEntity();

        Assert.IsType<OkObjectResult>(result);

        var okResult = result as OkObjectResult;
        Assert.NotNull(okResult?.Value);

        var response = okResult.Value;
        Assert.NotNull(response);

        var documentTypesProperty = response!.GetType().GetProperty("DocumentTypes");
        Assert.NotNull(documentTypesProperty);

        var documentTypes = documentTypesProperty!.GetValue(response) as IEnumerable<object>;
        Assert.NotNull(documentTypes);

        var documentTypeNames = documentTypes!
            .Select(d => d.GetType().GetProperty("TypeName")?.GetValue(d)?.ToString())
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();
        Assert.Contains("Certificate of Incorporation", documentTypeNames);
        Assert.DoesNotContain("South African ID Book", documentTypeNames);
    }
}
