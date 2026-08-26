using FourierIT_API.Controllers;
using FourierIT_API.Data;
using FourierIT_API.DTOs.Department;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourierIT.API.Tests;

public class DepartmentHierarchyControllerTests
{
    [Fact]
    public async Task CreateHierarchyNode_StoresNestedDepartment_AndDeleteParentWithChildrenIsBlocked()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepartmentHierarchyControllerTests_{Guid.NewGuid():N}")
            .Options;

        await using var context = new AppDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        var institutionType = new InstitutionType
        {
            InstitutionTypeId = 100,
            InstitutionTypeName = "Bank"
        };

        var institution = new Institution
        {
            InstitutionId = 10,
            InstitutionName = "Test Institution",
            VerifiedDomain = "testinstitution.com",
            RegNumber = 12345,
            TypeId = institutionType.InstitutionTypeId,
            InstitutionType = institutionType
        };

        var branch = new Branch
        {
            BranchId = 10,
            InstitutionId = institution.InstitutionId,
            Institution = institution,
            BranchName = "Head Office",
            City = "Pretoria"
        };

        context.Institutions.Add(institution);
        context.Branches.Add(branch);
        await context.SaveChangesAsync();

        var userManager = new Mock<UserManager<User>>(MockBehavior.Loose,
            new Mock<IUserStore<User>>().Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<User>>.Instance);

        var controller = new DepartmentController(context, userManager.Object);

        var rootResult = await controller.CreateHierarchyNode(new DepartmentHierarchyRequestDto
        {
            DepartmentName = "Finance",
            BranchId = branch.BranchId,
            ParentId = null
        });

        var rootOk = Assert.IsType<OkObjectResult>(rootResult);
        var createdRoot = Assert.IsType<DepartmentHierarchyNodeDto>(rootOk.Value);
        Assert.Equal("Finance", createdRoot.DepartmentName);

        var childResult = await controller.CreateHierarchyNode(new DepartmentHierarchyRequestDto
        {
            DepartmentName = "Accounts",
            BranchId = branch.BranchId,
            ParentId = createdRoot.DepartmentId
        });

        var childOk = Assert.IsType<OkObjectResult>(childResult);
        var createdChild = Assert.IsType<DepartmentHierarchyNodeDto>(childOk.Value);
        Assert.Equal("Accounts", createdChild.DepartmentName);
        Assert.Equal(createdRoot.DepartmentId, createdChild.ParentId);

        var hierarchyResult = await controller.GetHierarchy();
        var hierarchyOk = Assert.IsType<OkObjectResult>(hierarchyResult);
        var rootNodes = Assert.IsAssignableFrom<List<DepartmentHierarchyNodeDto>>(hierarchyOk.Value);

        var financeNode = Assert.Single(rootNodes);
        Assert.Equal("Finance", financeNode.DepartmentName);
        var accountNode = Assert.Single(financeNode.Children);
        Assert.Equal("Accounts", accountNode.DepartmentName);

        var deleteResult = await controller.DeleteHierarchyNode(financeNode.DepartmentId);
        var conflict = Assert.IsType<ConflictObjectResult>(deleteResult);
        Assert.NotNull(conflict.Value);
        Assert.Contains("child departments", conflict.Value.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
