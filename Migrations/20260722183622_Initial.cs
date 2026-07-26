using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Backups",
                columns: table => new
                {
                    BackupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DateBackedUp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsManualBackup = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Backups", x => x.BackupId);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.DocumentTypeId);
                });

            migrationBuilder.CreateTable(
                name: "EnquiryFlags",
                columns: table => new
                {
                    EnquiryFlagId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    EnquiryId = table.Column<int>(type: "int", nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnquiryFlags", x => x.EnquiryFlagId);
                });

            migrationBuilder.CreateTable(
                name: "EntityTypes",
                columns: table => new
                {
                    EntityTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityTypes", x => x.EntityTypeId);
                });

            migrationBuilder.CreateTable(
                name: "FICARules",
                columns: table => new
                {
                    RuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ValidityMonths = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FICARules", x => x.RuleId);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionType",
                columns: table => new
                {
                    InstitutionTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionTypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionType", x => x.InstitutionTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                });

            migrationBuilder.CreateTable(
                name: "PEPLists",
                columns: table => new
                {
                    UniqueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SourceLinks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Aliases = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PEPLists", x => x.UniqueId);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionId);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                columns: table => new
                {
                    ProvinceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProvinceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.ProvinceId);
                });

            migrationBuilder.CreateTable(
                name: "RiskVariables",
                columns: table => new
                {
                    RiskVariableId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VarName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WeightMultiplier = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskVariables", x => x.RiskVariableId);
                });

            migrationBuilder.CreateTable(
                name: "SecurityQuestions",
                columns: table => new
                {
                    SecurityQuestionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityQuestions", x => x.SecurityQuestionId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequiredDocuments",
                columns: table => new
                {
                    RequiredDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityTypeId = table.Column<int>(type: "int", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequiredDocuments", x => x.RequiredDocumentId);
                    table.ForeignKey(
                        name: "FK_RequiredDocuments_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "DocumentTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequiredDocuments_EntityTypes_EntityTypeId",
                        column: x => x.EntityTypeId,
                        principalTable: "EntityTypes",
                        principalColumn: "EntityTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentFicaRules",
                columns: table => new
                {
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    FICARuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFicaRules", x => new { x.DocumentTypeId, x.FICARuleId });
                    table.ForeignKey(
                        name: "FK_DocumentFicaRules_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "DocumentTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentFicaRules_FICARules_FICARuleId",
                        column: x => x.FICARuleId,
                        principalTable: "FICARules",
                        principalColumn: "RuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FICARuleHistories",
                columns: table => new
                {
                    RuleHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DateChanged = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FICARuleHistories", x => x.RuleHistoryId);
                    table.ForeignKey(
                        name: "FK_FICARuleHistories_FICARules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "FICARules",
                        principalColumn: "RuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Institutions",
                columns: table => new
                {
                    InstitutionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VerifiedDomain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RegNumber = table.Column<int>(type: "int", nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutions", x => x.InstitutionId);
                    table.ForeignKey(
                        name: "FK_Institutions_InstitutionType_TypeId",
                        column: x => x.TypeId,
                        principalTable: "InstitutionType",
                        principalColumn: "InstitutionTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationHistory",
                columns: table => new
                {
                    NotificationHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationHistory", x => x.NotificationHistoryId);
                    table.ForeignKey(
                        name: "FK_NotificationHistory_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "NotificationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    CityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProvinceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.CityId);
                    table.ForeignKey(
                        name: "FK_Cities_Provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalTable: "Provinces",
                        principalColumn: "ProvinceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Branch",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionId = table.Column<int>(type: "int", nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    City = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branch", x => x.BranchId);
                    table.ForeignKey(
                        name: "FK_Branch_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "InstitutionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Suburbs",
                columns: table => new
                {
                    SuburbId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SuburbName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsAtRisk = table.Column<bool>(type: "bit", nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suburbs", x => x.SuburbId);
                    table.ForeignKey(
                        name: "FK_Suburbs_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.DepartmentId);
                    table.ForeignKey(
                        name: "FK_Departments_Branch_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branch",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    AddressId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AddressLine = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PostalCode = table.Column<int>(type: "int", nullable: false),
                    SuburbId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.AddressId);
                    table.ForeignKey(
                        name: "FK_Addresses_Suburbs_SuburbId",
                        column: x => x.SuburbId,
                        principalTable: "Suburbs",
                        principalColumn: "SuburbId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MfaEnabled = table.Column<bool>(type: "bit", maxLength: 100, nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "int", maxLength: 500, nullable: false),
                    IsPEPStatus = table.Column<bool>(type: "bit", nullable: false),
                    AccountStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HasAccessToken = table.Column<bool>(type: "bit", nullable: false),
                    EntityTypeId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "DepartmentId");
                    table.ForeignKey(
                        name: "FK_AspNetUsers_EntityTypes_EntityTypeId",
                        column: x => x.EntityTypeId,
                        principalTable: "EntityTypes",
                        principalColumn: "EntityTypeId");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Discriminator = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    IsActiveContext = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientEnlistments",
                columns: table => new
                {
                    ClientEnlistmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InstitutionId = table.Column<int>(type: "int", nullable: false),
                    EnlistmentDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EnlistmentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientEnlistments", x => x.ClientEnlistmentId);
                    table.ForeignKey(
                        name: "FK_ClientEnlistments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientEnlistments_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "InstitutionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceStatuses",
                columns: table => new
                {
                    ComplianceStatusId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    OverallStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ComplianceCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalRequiredDocuments = table.Column<int>(type: "int", nullable: false),
                    UploadedDocuments = table.Column<int>(type: "int", nullable: false),
                    CompliantDocuments = table.Column<int>(type: "int", nullable: false),
                    NonCompliantDocuments = table.Column<int>(type: "int", nullable: false),
                    ExpiredDocuments = table.Column<int>(type: "int", nullable: false),
                    MissingDocuments = table.Column<int>(type: "int", nullable: false),
                    NotCertifiedDocuments = table.Column<int>(type: "int", nullable: false),
                    PendingReviewDocuments = table.Column<int>(type: "int", nullable: false),
                    CompliancePercentage = table.Column<int>(type: "int", nullable: false),
                    OverallRiskScore = table.Column<int>(type: "int", nullable: false),
                    ComplianceScore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastChecked = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ComplianceDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresEnhancedDueDiligence = table.Column<bool>(type: "bit", nullable: false),
                    IsSuspicious = table.Column<bool>(type: "bit", nullable: false),
                    IsPEP = table.Column<bool>(type: "bit", nullable: false),
                    HasSanctionFlag = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceStatuses", x => x.ComplianceStatusId);
                    table.ForeignKey(
                        name: "FK_ComplianceStatuses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComplianceStatuses_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ExpiryDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsCertified = table.Column<bool>(type: "bit", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    EncryptionAlgorithm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAccessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_Documents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "DocumentTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnquiryComments",
                columns: table => new
                {
                    EnquiryCommentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnquiryFlagId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MessageText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnquiryComments", x => x.EnquiryCommentId);
                    table.ForeignKey(
                        name: "FK_EnquiryComments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnquiryComments_EnquiryFlags_EnquiryFlagId",
                        column: x => x.EnquiryFlagId,
                        principalTable: "EnquiryFlags",
                        principalColumn: "EnquiryFlagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionEnquiryRequests",
                columns: table => new
                {
                    EnquiryRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionId = table.Column<int>(type: "int", nullable: false),
                    TargetDepartmentId = table.Column<int>(type: "int", nullable: true),
                    TargetUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RequestType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PurposeNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserResponseNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionEnquiryRequests", x => x.EnquiryRequestId);
                    table.ForeignKey(
                        name: "FK_InstitutionEnquiryRequests_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InstitutionEnquiryRequests_AspNetUsers_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InstitutionEnquiryRequests_Departments_TargetDepartmentId",
                        column: x => x.TargetDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "DepartmentId");
                    table.ForeignKey(
                        name: "FK_InstitutionEnquiryRequests_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "InstitutionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionMembers",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InstitutionId = table.Column<int>(type: "int", nullable: false),
                    MembersId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionMembers", x => new { x.MembersId, x.UserId, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_InstitutionMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstitutionMembers_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "InstitutionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    ProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AddressId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.ProfileId);
                    table.ForeignKey(
                        name: "FK_Profiles_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "AddressId");
                    table.ForeignKey(
                        name: "FK_Profiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => new { x.NotificationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_UserNotifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "NotificationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSecurityQuestions",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SecurityQuestionId = table.Column<int>(type: "int", nullable: false),
                    EncryptedAnswer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSecurityQuestions", x => new { x.UserId, x.SecurityQuestionId });
                    table.ForeignKey(
                        name: "FK_UserSecurityQuestions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSecurityQuestions_SecurityQuestions_SecurityQuestionId",
                        column: x => x.SecurityQuestionId,
                        principalTable: "SecurityQuestions",
                        principalColumn: "SecurityQuestionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientRiskRatings",
                columns: table => new
                {
                    RatingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientEnlistmentId = table.Column<int>(type: "int", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRiskRatings", x => x.RatingId);
                    table.ForeignKey(
                        name: "FK_ClientRiskRatings_ClientEnlistments_ClientEnlistmentId",
                        column: x => x.ClientEnlistmentId,
                        principalTable: "ClientEnlistments",
                        principalColumn: "ClientEnlistmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceHistories",
                columns: table => new
                {
                    HistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplianceStatusId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PreviousRiskLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NewRiskLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PreviousComplianceScore = table.Column<int>(type: "int", nullable: true),
                    NewComplianceScore = table.Column<int>(type: "int", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TriggeringDocument = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentsAffected = table.Column<int>(type: "int", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ChangeSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActionTriggered = table.Column<bool>(type: "bit", nullable: false),
                    ActionTriggeredDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanBeRolledBack = table.Column<bool>(type: "bit", nullable: false),
                    HasBeenRolledBack = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceHistories", x => x.HistoryId);
                    table.ForeignKey(
                        name: "FK_ComplianceHistories_AspNetUsers_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComplianceHistories_AspNetUsers_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComplianceHistories_ComplianceStatuses_ComplianceStatusId",
                        column: x => x.ComplianceStatusId,
                        principalTable: "ComplianceStatuses",
                        principalColumn: "ComplianceStatusId");
                });

            migrationBuilder.CreateTable(
                name: "CertificationDetails",
                columns: table => new
                {
                    CertificationID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CommissionerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CertificationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationDetails", x => x.CertificationID);
                    table.ForeignKey(
                        name: "FK_CertificationDetails_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceAlerts",
                columns: table => new
                {
                    AlertId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplianceStatusId = table.Column<int>(type: "int", nullable: false),
                    AlertType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AlertMessage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    DocumentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RequiredAction = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActionRequiredFromUser = table.Column<bool>(type: "bit", nullable: false),
                    ActionRequiredFromAdmin = table.Column<bool>(type: "bit", nullable: false),
                    NotificationSent = table.Column<bool>(type: "bit", nullable: false),
                    NotificationAttempts = table.Column<int>(type: "int", nullable: false),
                    LastNotificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEscalated = table.Column<bool>(type: "bit", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedTo = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CanBeAutoResolved = table.Column<bool>(type: "bit", nullable: false),
                    WasAutoResolved = table.Column<bool>(type: "bit", nullable: false),
                    RelatedAlertId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceAlerts", x => x.AlertId);
                    table.ForeignKey(
                        name: "FK_ComplianceAlerts_AspNetUsers_AcknowledgedBy",
                        column: x => x.AcknowledgedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComplianceAlerts_AspNetUsers_EscalatedTo",
                        column: x => x.EscalatedTo,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComplianceAlerts_AspNetUsers_ResolvedBy",
                        column: x => x.ResolvedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ComplianceAlerts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComplianceAlerts_ComplianceStatuses_ComplianceStatusId",
                        column: x => x.ComplianceStatusId,
                        principalTable: "ComplianceStatuses",
                        principalColumn: "ComplianceStatusId");
                    table.ForeignKey(
                        name: "FK_ComplianceAlerts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                });

            migrationBuilder.CreateTable(
                name: "DocumentAccesses",
                columns: table => new
                {
                    DocumentAccessId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    GrantedToUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AccessLevel = table.Column<int>(type: "int", nullable: false),
                    GrantedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAccesses", x => x.DocumentAccessId);
                    table.ForeignKey(
                        name: "FK_DocumentAccesses_AspNetUsers_GrantedToUserId",
                        column: x => x.GrantedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentAccesses_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                });

            migrationBuilder.CreateTable(
                name: "DocumentAccessLogs",
                columns: table => new
                {
                    DocumentAccessLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    AccessedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccessDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IPAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAccessLogs", x => x.DocumentAccessLogId);
                    table.ForeignKey(
                        name: "FK_DocumentAccessLogs_AspNetUsers_AccessedByUserId",
                        column: x => x.AccessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentAccessLogs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                });

            migrationBuilder.CreateTable(
                name: "DocumentBlobs",
                columns: table => new
                {
                    DocumentBlobId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    FileData = table.Column<byte[]>(type: "varbinary(max)", maxLength: 50485760, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentBlobs", x => x.DocumentBlobId);
                    table.ForeignKey(
                        name: "FK_DocumentBlobs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentComplianceChecks",
                columns: table => new
                {
                    CheckId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ComplianceStatusId = table.Column<int>(type: "int", nullable: false),
                    CheckStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NonComplianceReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsExpiryValid = table.Column<bool>(type: "bit", nullable: false),
                    ExpiryCheckDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DaysUntilExpiry = table.Column<int>(type: "int", nullable: true),
                    IsCertified = table.Column<bool>(type: "bit", nullable: false),
                    CertificationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsRecent = table.Column<bool>(type: "bit", nullable: false),
                    DocumentAgeInMonths = table.Column<int>(type: "int", nullable: true),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
                    IsVirusFree = table.Column<bool>(type: "bit", nullable: false),
                    QualityScore = table.Column<int>(type: "int", nullable: false),
                    IsHighQuality = table.Column<bool>(type: "bit", nullable: false),
                    IsLegible = table.Column<bool>(type: "bit", nullable: false),
                    RequiresManualReview = table.Column<bool>(type: "bit", nullable: false),
                    ManualReviewReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsManuallyApproved = table.Column<bool>(type: "bit", nullable: false),
                    ManuallyReviewedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ManualReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemediationAction = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ActionDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionCompleted = table.Column<bool>(type: "bit", nullable: false),
                    ActionCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IndividualRiskScore = table.Column<int>(type: "int", nullable: false),
                    RiskCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    AppliedRuleId = table.Column<int>(type: "int", nullable: true),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: true),
                    LastReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatchId = table.Column<int>(type: "int", nullable: true),
                    IsPartOfBatch = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentComplianceChecks", x => x.CheckId);
                    table.ForeignKey(
                        name: "FK_DocumentComplianceChecks_AspNetUsers_CheckedBy",
                        column: x => x.CheckedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentComplianceChecks_AspNetUsers_ManuallyReviewedBy",
                        column: x => x.ManuallyReviewedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentComplianceChecks_ComplianceStatuses_ComplianceStatusId",
                        column: x => x.ComplianceStatusId,
                        principalTable: "ComplianceStatuses",
                        principalColumn: "ComplianceStatusId");
                    table.ForeignKey(
                        name: "FK_DocumentComplianceChecks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_DocumentComplianceChecks_FICARules_AppliedRuleId",
                        column: x => x.AppliedRuleId,
                        principalTable: "FICARules",
                        principalColumn: "RuleId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DocumentStatusHistories",
                columns: table => new
                {
                    StatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    StatusName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DateArchived = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentStatusHistories", x => x.StatusHistoryId);
                    table.ForeignKey(
                        name: "FK_DocumentStatusHistories_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessLists",
                columns: table => new
                {
                    EnquiryRequestId = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    EnquiryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLists", x => new { x.EnquiryRequestId, x.DocumentId, x.EnquiryId });
                    table.ForeignKey(
                        name: "FK_AccessLists_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccessLists_EnquiryFlags_EnquiryId",
                        column: x => x.EnquiryId,
                        principalTable: "EnquiryFlags",
                        principalColumn: "EnquiryFlagId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccessLists_InstitutionEnquiryRequests_EnquiryRequestId",
                        column: x => x.EnquiryRequestId,
                        principalTable: "InstitutionEnquiryRequests",
                        principalColumn: "EnquiryRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessTokens",
                columns: table => new
                {
                    TokenId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnquiryRequestId = table.Column<int>(type: "int", nullable: false),
                    TokenString = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ExpiryTimeStamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessTokens", x => x.TokenId);
                    table.ForeignKey(
                        name: "FK_AccessTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccessTokens_InstitutionEnquiryRequests_EnquiryRequestId",
                        column: x => x.EnquiryRequestId,
                        principalTable: "InstitutionEnquiryRequests",
                        principalColumn: "EnquiryRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAccessApprovals",
                columns: table => new
                {
                    ApprovalId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnquiryRequestId = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAccessApprovals", x => x.ApprovalId);
                    table.ForeignKey(
                        name: "FK_DocumentAccessApprovals_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentAccessApprovals_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                    table.ForeignKey(
                        name: "FK_DocumentAccessApprovals_InstitutionEnquiryRequests_EnquiryRequestId",
                        column: x => x.EnquiryRequestId,
                        principalTable: "InstitutionEnquiryRequests",
                        principalColumn: "EnquiryRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionRequestedDocumentTypes",
                columns: table => new
                {
                    EnquiryRequestId = table.Column<int>(type: "int", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    FICARuleId = table.Column<int>(type: "int", nullable: false),
                    isMandatory = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionRequestedDocumentTypes", x => new { x.EnquiryRequestId, x.DocumentTypeId });
                    table.ForeignKey(
                        name: "FK_InstitutionRequestedDocumentTypes_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "DocumentTypeId");
                    table.ForeignKey(
                        name: "FK_InstitutionRequestedDocumentTypes_FICARules_FICARuleId",
                        column: x => x.FICARuleId,
                        principalTable: "FICARules",
                        principalColumn: "RuleId");
                    table.ForeignKey(
                        name: "FK_InstitutionRequestedDocumentTypes_InstitutionEnquiryRequests_EnquiryRequestId",
                        column: x => x.EnquiryRequestId,
                        principalTable: "InstitutionEnquiryRequests",
                        principalColumn: "EnquiryRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskHistory",
                columns: table => new
                {
                    RiskHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientRiskRatingId = table.Column<int>(type: "int", nullable: false),
                    ScoreAtTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    RecordedDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskHistory", x => x.RiskHistoryId);
                    table.ForeignKey(
                        name: "FK_RiskHistory_ClientRiskRatings_ClientRiskRatingId",
                        column: x => x.ClientRiskRatingId,
                        principalTable: "ClientRiskRatings",
                        principalColumn: "RatingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskRatingVariables",
                columns: table => new
                {
                    RatingId = table.Column<int>(type: "int", nullable: false),
                    RiskVariableId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskRatingVariables", x => new { x.RatingId, x.RiskVariableId });
                    table.ForeignKey(
                        name: "FK_RiskRatingVariables_ClientRiskRatings_RatingId",
                        column: x => x.RatingId,
                        principalTable: "ClientRiskRatings",
                        principalColumn: "RatingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskRatingVariables_RiskVariables_RiskVariableId",
                        column: x => x.RiskVariableId,
                        principalTable: "RiskVariables",
                        principalColumn: "RiskVariableId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlobHistories",
                columns: table => new
                {
                    BlobHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArchivedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActionTaken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentBlobId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlobHistories", x => x.BlobHistoryId);
                    table.ForeignKey(
                        name: "FK_BlobHistories_DocumentBlobs_DocumentBlobId",
                        column: x => x.DocumentBlobId,
                        principalTable: "DocumentBlobs",
                        principalColumn: "DocumentBlobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceAuditLogs",
                columns: table => new
                {
                    AuditLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplianceStatusId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActionDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    DocumentCheckId = table.Column<int>(type: "int", nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AffectsCompliance = table.Column<bool>(type: "bit", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CanBeReversed = table.Column<bool>(type: "bit", nullable: false),
                    HasBeenReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversedByAuditLogId = table.Column<int>(type: "int", nullable: true),
                    BatchId = table.Column<int>(type: "int", nullable: true),
                    IsPartOfBatch = table.Column<bool>(type: "bit", nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceAuditLogs", x => x.AuditLogId);
                    table.ForeignKey(
                        name: "FK_ComplianceAuditLogs_AspNetUsers_PerformedBy",
                        column: x => x.PerformedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComplianceAuditLogs_ComplianceStatuses_ComplianceStatusId",
                        column: x => x.ComplianceStatusId,
                        principalTable: "ComplianceStatuses",
                        principalColumn: "ComplianceStatusId");
                    table.ForeignKey(
                        name: "FK_ComplianceAuditLogs_DocumentComplianceChecks_DocumentCheckId",
                        column: x => x.DocumentCheckId,
                        principalTable: "DocumentComplianceChecks",
                        principalColumn: "CheckId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ComplianceAuditLogs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId");
                });

            migrationBuilder.CreateTable(
                name: "EnquirySessions",
                columns: table => new
                {
                    EnquirySessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenId = table.Column<int>(type: "int", nullable: false),
                    SessionStartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    SessionEndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnquirySessions", x => x.EnquirySessionId);
                    table.ForeignKey(
                        name: "FK_EnquirySessions_AccessTokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "AccessTokens",
                        principalColumn: "TokenId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "DA", null, "Department Admin", "DEPARTMENT ADMIN" },
                    { "DO", null, "Document Owner", "DOCUMENT OWNER" },
                    { "SH", null, "Stakeholder", "STAKEHOLDER" }
                });

            migrationBuilder.InsertData(
                table: "DocumentTypes",
                columns: new[] { "DocumentTypeId", "Description", "TypeName" },
                values: new object[,]
                {
                    { 1, "Green bar-coded identity document (certified copy)", "South African ID Book" },
                    { 2, "South African Smart ID card (certified copy)", "Smart ID Card" },
                    { 3, "Valid South African passport (certified copy)", "South African Passport" },
                    { 4, "Valid foreign passport (certified copy)", "Foreign Passport" },
                    { 5, "Valid South African work permit or visa", "Work Permit" },
                    { 6, "Valid asylum seeker or refugee permit", "Asylum/Refugee Permit" },
                    { 7, "Water/electricity bill (not older than 3 months)", "Utility Bill" },
                    { 8, "Telecommunications or ISP account (not older than 3 months)", "Telkom/Internet Account" },
                    { 9, "Current residential lease agreement", "Lease Agreement" },
                    { 10, "Official bank statement showing address (not older than 3 months)", "Bank Statement" },
                    { 11, "COR14.3 or company registration certificate", "Certificate of Incorporation" },
                    { 12, "MOI (Memorandum of Incorporation)", "Memorandum of Incorporation" },
                    { 13, "Board resolution authorizing account/instruction", "Company Resolution" },
                    { 14, "Current register of shareholders/members", "Shareholder Register" },
                    { 15, "Registered trust deed and letters of authority", "Trust Deed" },
                    { 16, "Resolution from trustees authorizing the transaction", "Trust Resolution" },
                    { 17, "Registered partnership agreement", "Partnership Agreement" },
                    { 18, "ID of authorized representative (certified copy)", "Director/Trustee ID" },
                    { 19, "Proof of residential address for authorized person", "Proof of Address - Representative" }
                });

            migrationBuilder.InsertData(
                table: "EntityTypes",
                columns: new[] { "EntityTypeId", "Name" },
                values: new object[,]
                {
                    { 1, "South African Individual" },
                    { 2, "Foreign National Individual" },
                    { 3, "Company (Pty) Ltd" },
                    { 4, "Trust" },
                    { 5, "Partnership" },
                    { 6, "Legal Entity - Other" }
                });

            migrationBuilder.InsertData(
                table: "RequiredDocuments",
                columns: new[] { "RequiredDocumentId", "Description", "DocumentTypeId", "EntityTypeId", "IsMandatory" },
                values: new object[,]
                {
                    { 1, "One form of SA ID required", 1, 1, true },
                    { 2, "Alternative to ID book", 2, 1, false },
                    { 3, "Alternative to ID book", 3, 1, false },
                    { 4, "One address proof required (not older than 3 months)", 7, 1, true },
                    { 5, "Alternative address proof", 8, 1, false },
                    { 6, "Alternative address proof", 9, 1, false },
                    { 7, "Valid foreign passport required", 4, 2, true },
                    { 8, "Valid SA work permit/visa required", 5, 2, true },
                    { 9, "Alternative to work permit", 6, 2, false },
                    { 10, "Proof of address required", 10, 2, true },
                    { 11, "Certificate of incorporation required", 11, 3, true },
                    { 12, "MOI required", 12, 3, true },
                    { 13, "Board resolution authorizing required", 13, 3, true },
                    { 14, "Current shareholder register required", 14, 3, true },
                    { 15, "ID of authorized director required", 18, 3, true },
                    { 16, "Address proof for director required", 19, 3, true },
                    { 17, "Trust deed and letters of authority required", 15, 4, true },
                    { 18, "Trustee resolution required", 16, 4, true },
                    { 19, "ID of authorized trustee required", 18, 4, true },
                    { 20, "Address proof for trustee required", 19, 4, true },
                    { 21, "Partnership agreement required", 17, 5, true },
                    { 22, "Partnership resolution required", 13, 5, true },
                    { 23, "ID of authorized partner required", 18, 5, true },
                    { 24, "Address proof for partner required", 19, 5, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessLists_DocumentId",
                table: "AccessLists",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLists_EnquiryId",
                table: "AccessLists",
                column: "EnquiryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessTokens_EnquiryRequestId",
                table: "AccessTokens",
                column: "EnquiryRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessTokens_UserId",
                table: "AccessTokens",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_SuburbId",
                table: "Addresses",
                column: "SuburbId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DepartmentId",
                table: "AspNetUsers",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_EntityTypeId",
                table: "AspNetUsers",
                column: "EntityTypeId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BlobHistories_DocumentBlobId",
                table: "BlobHistories",
                column: "DocumentBlobId");

            migrationBuilder.CreateIndex(
                name: "IX_Branch_InstitutionId",
                table: "Branch",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationDetails_DocumentId",
                table: "CertificationDetails",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_ProvinceId",
                table: "Cities",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientEnlistments_InstitutionId",
                table: "ClientEnlistments",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientEnlistments_UserId",
                table: "ClientEnlistments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRiskRatings_ClientEnlistmentId",
                table: "ClientRiskRatings",
                column: "ClientEnlistmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlerts_AcknowledgedBy",
                table: "ComplianceAlerts",
                column: "AcknowledgedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlerts_ComplianceStatusId",
                table: "ComplianceAlerts",
                column: "ComplianceStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlerts_DocumentId",
                table: "ComplianceAlerts",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlerts_EscalatedTo",
                table: "ComplianceAlerts",
                column: "EscalatedTo");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlerts_IsResolved",
                table: "ComplianceAlerts",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlerts_ResolvedBy",
                table: "ComplianceAlerts",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlerts_Severity",
                table: "ComplianceAlerts",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlerts_UserId",
                table: "ComplianceAlerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAuditLogs_ComplianceStatusId",
                table: "ComplianceAuditLogs",
                column: "ComplianceStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAuditLogs_DocumentCheckId",
                table: "ComplianceAuditLogs",
                column: "DocumentCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAuditLogs_DocumentId",
                table: "ComplianceAuditLogs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAuditLogs_PerformedAt",
                table: "ComplianceAuditLogs",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAuditLogs_PerformedBy",
                table: "ComplianceAuditLogs",
                column: "PerformedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceHistories_ApprovedBy",
                table: "ComplianceHistories",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceHistories_ChangedBy",
                table: "ComplianceHistories",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceHistories_ComplianceStatusId",
                table: "ComplianceHistories",
                column: "ComplianceStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceStatuses_DepartmentId",
                table: "ComplianceStatuses",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceStatuses_OverallStatus",
                table: "ComplianceStatuses",
                column: "OverallStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceStatuses_RiskLevel",
                table: "ComplianceStatuses",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceStatuses_UserId",
                table: "ComplianceStatuses",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_BranchId",
                table: "Departments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAccessApprovals_ApprovedByUserId",
                table: "DocumentAccessApprovals",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAccessApprovals_DocumentId",
                table: "DocumentAccessApprovals",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAccessApprovals_EnquiryRequestId",
                table: "DocumentAccessApprovals",
                column: "EnquiryRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAccesses_DocumentId",
                table: "DocumentAccesses",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAccesses_GrantedToUserId",
                table: "DocumentAccesses",
                column: "GrantedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAccessLogs_AccessedByUserId",
                table: "DocumentAccessLogs",
                column: "AccessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAccessLogs_DocumentId",
                table: "DocumentAccessLogs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBlobs_DocumentId",
                table: "DocumentBlobs",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComplianceChecks_AppliedRuleId",
                table: "DocumentComplianceChecks",
                column: "AppliedRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComplianceChecks_CheckedBy",
                table: "DocumentComplianceChecks",
                column: "CheckedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComplianceChecks_ComplianceStatusId",
                table: "DocumentComplianceChecks",
                column: "ComplianceStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComplianceChecks_DocumentId",
                table: "DocumentComplianceChecks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComplianceChecks_ManuallyReviewedBy",
                table: "DocumentComplianceChecks",
                column: "ManuallyReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFicaRules_FICARuleId",
                table: "DocumentFicaRules",
                column: "FICARuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId",
                table: "Documents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentStatusHistories_DocumentId",
                table: "DocumentStatusHistories",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnquiryComments_EnquiryFlagId",
                table: "EnquiryComments",
                column: "EnquiryFlagId");

            migrationBuilder.CreateIndex(
                name: "IX_EnquiryComments_UserId",
                table: "EnquiryComments",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnquirySessions_TokenId",
                table: "EnquirySessions",
                column: "TokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FICARuleHistories_RuleId",
                table: "FICARuleHistories",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionEnquiryRequests_ApprovedByUserId",
                table: "InstitutionEnquiryRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionEnquiryRequests_InstitutionId",
                table: "InstitutionEnquiryRequests",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionEnquiryRequests_TargetDepartmentId",
                table: "InstitutionEnquiryRequests",
                column: "TargetDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionEnquiryRequests_TargetUserId",
                table: "InstitutionEnquiryRequests",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionMembers_InstitutionId",
                table: "InstitutionMembers",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionMembers_UserId",
                table: "InstitutionMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionRequestedDocumentTypes_DocumentTypeId",
                table: "InstitutionRequestedDocumentTypes",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionRequestedDocumentTypes_FICARuleId",
                table: "InstitutionRequestedDocumentTypes",
                column: "FICARuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_TypeId",
                table: "Institutions",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationHistory_NotificationId",
                table: "NotificationHistory",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_AddressId",
                table: "Profiles",
                column: "AddressId",
                unique: true,
                filter: "[AddressId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_UserId",
                table: "Profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequiredDocuments_DocumentTypeId",
                table: "RequiredDocuments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RequiredDocuments_EntityTypeId",
                table: "RequiredDocuments",
                column: "EntityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskHistory_ClientRiskRatingId",
                table: "RiskHistory",
                column: "ClientRiskRatingId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskRatingVariables_RiskVariableId",
                table: "RiskRatingVariables",
                column: "RiskVariableId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Suburbs_CityId",
                table: "Suburbs",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId",
                table: "UserNotifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSecurityQuestions_SecurityQuestionId",
                table: "UserSecurityQuestions",
                column: "SecurityQuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessLists");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Backups");

            migrationBuilder.DropTable(
                name: "BlobHistories");

            migrationBuilder.DropTable(
                name: "CertificationDetails");

            migrationBuilder.DropTable(
                name: "ComplianceAlerts");

            migrationBuilder.DropTable(
                name: "ComplianceAuditLogs");

            migrationBuilder.DropTable(
                name: "ComplianceHistories");

            migrationBuilder.DropTable(
                name: "DocumentAccessApprovals");

            migrationBuilder.DropTable(
                name: "DocumentAccesses");

            migrationBuilder.DropTable(
                name: "DocumentAccessLogs");

            migrationBuilder.DropTable(
                name: "DocumentFicaRules");

            migrationBuilder.DropTable(
                name: "DocumentStatusHistories");

            migrationBuilder.DropTable(
                name: "EnquiryComments");

            migrationBuilder.DropTable(
                name: "EnquirySessions");

            migrationBuilder.DropTable(
                name: "FICARuleHistories");

            migrationBuilder.DropTable(
                name: "InstitutionMembers");

            migrationBuilder.DropTable(
                name: "InstitutionRequestedDocumentTypes");

            migrationBuilder.DropTable(
                name: "NotificationHistory");

            migrationBuilder.DropTable(
                name: "PEPLists");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropTable(
                name: "RequiredDocuments");

            migrationBuilder.DropTable(
                name: "RiskHistory");

            migrationBuilder.DropTable(
                name: "RiskRatingVariables");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "UserSecurityQuestions");

            migrationBuilder.DropTable(
                name: "DocumentBlobs");

            migrationBuilder.DropTable(
                name: "DocumentComplianceChecks");

            migrationBuilder.DropTable(
                name: "EnquiryFlags");

            migrationBuilder.DropTable(
                name: "AccessTokens");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "ClientRiskRatings");

            migrationBuilder.DropTable(
                name: "RiskVariables");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "SecurityQuestions");

            migrationBuilder.DropTable(
                name: "ComplianceStatuses");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "FICARules");

            migrationBuilder.DropTable(
                name: "InstitutionEnquiryRequests");

            migrationBuilder.DropTable(
                name: "Suburbs");

            migrationBuilder.DropTable(
                name: "ClientEnlistments");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Provinces");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "EntityTypes");

            migrationBuilder.DropTable(
                name: "Branch");

            migrationBuilder.DropTable(
                name: "Institutions");

            migrationBuilder.DropTable(
                name: "InstitutionType");
        }
    }
}
