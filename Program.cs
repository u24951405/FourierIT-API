using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Repositories;
using FourierIT_API.Security;
using FourierIT_API.Service;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            // Allow enums to be sent/received as strings (e.g. "OTP_SENT") from the frontend
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var traceId = context.HttpContext.TraceIdentifier;
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The supplied value is invalid."
                        : error.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new
        {
            message = "One or more validation errors occurred.",
            statusCode = StatusCodes.Status400BadRequest,
            traceId,
            errors
        });
    };
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy.WithOrigins(
                "https://docuvault-001-site1.ktempuri.com",
                "http://docuvault-001-site1.ktempuri.com",
                "http://localhost:4200",
                "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var maxRetryCount = builder.Environment.IsDevelopment() ? 2 : 5;
var maxRetryDelaySeconds = builder.Environment.IsDevelopment() ? 5 : 30;

builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: maxRetryCount,
            maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
            errorNumbersToAdd: null)
    ));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "FourierIT-API", Version = "v1" });
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });
});

builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IFileScanService, FileScanService>();
builder.Services.AddScoped<IInAppNotificationService, InAppNotificationService>();
// Notification emails are queued and sent in the background, so requests never wait on the mail server.
builder.Services.AddSingleton<NotificationEmailQueue>();
builder.Services.AddSingleton<INotificationEmailQueue>(sp => sp.GetRequiredService<NotificationEmailQueue>());
builder.Services.AddHostedService<NotificationEmailSender>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IComplianceService, ComplianceService>();
builder.Services.AddSingleton<DocumentValidityCalculator>();
builder.Services.AddScoped<DocumentValidityPolicyService>();

// Register Audit Log Service
builder.Services.AddScoped<FourierIT_API.Interfaces.IAuditLogService, FourierIT_API.Services.AuditLogService>();

// Register Azure Blob Client
builder.Services.AddSingleton(sp =>
    new Azure.Storage.Blobs.BlobServiceClient(
        sp.GetRequiredService<IConfiguration>()["AzureBlobStorage:ConnectionString"]));

// Register Backup Service
builder.Services.AddScoped<FourierIT_API.Interfaces.IBackupService, FourierIT_API.Services.BackupService>();
// Runs manual backups in the background (one at a time) so the page doesn't wait on the upload.
builder.Services.AddSingleton<FourierIT_API.Services.BackupJobTracker>();

// Register Daily Backup Service as a Hosted Service
builder.Services.AddHostedService<DailyBackupService>();
// Register expired document compliance scheduler
builder.Services.AddHostedService<ExpiredDocumentComplianceService>();
// Access-ending warnings for institutions, reminders for unanswered requests, and escalation of overdue ones.
builder.Services.AddHostedService<AccessRequestReminderService>();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.Configure<FourierIT_API.Models.EntityVerificationOptions>(
    builder.Configuration.GetSection("EntityVerification"));
builder.Services.AddScoped<FourierIT_API.Services.LocalEntityVerificationService>();
builder.Services.AddHttpClient<FourierIT_API.Services.ExternalEntityVerificationService>();
builder.Services.AddScoped<FourierIT_API.Services.ExternalEntityVerificationService>();
builder.Services.AddScoped<FourierIT_API.Interfaces.IEntityVerificationService, FourierIT_API.Services.FallbackEntityVerificationService>();

builder.Services.AddIdentity<User, Role>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
    options.DefaultChallengeScheme =
    options.DefaultForbidScheme =
    options.DefaultScheme =
    options.DefaultSignInScheme =
    options.DefaultSignOutScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["JWT:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["JWT:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JWT:SigningKey"] ?? throw new System.InvalidOperationException("JWT:SigningKey is required."))
            )
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireClaim("superadmin", "true"));

    foreach (var permission in new[]
    {
        "Documents.View", "Documents.Upload", "Documents.Manage", "Compliance.View",
        "Compliance.Manage", "Users.Manage", "Roles.Manage", "Reports.View",
        "Audit.View", "Backup.Manage"
    })
    {
        options.AddPolicy(permission, policy => policy.Requirements.Add(new PermissionRequirement(permission)));
    }
});

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<DepartmentRequestValidationService>();
// Register SuperAdmin authorization handler so the seeded Super Admin user bypasses role-based checks
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, FourierIT_API.Security.SuperAdminRoleHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AngularClient");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Startup database initialization is intentionally configurable to avoid
// expensive startup delays on every API run.
var runDbInit = builder.Configuration.GetValue("StartupTasks:RunDatabaseInitialization", builder.Environment.IsDevelopment());
var runDevSeed = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue("StartupTasks:RunDevSeed", false);

if (runDbInit)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var startupTimer = Stopwatch.StartNew();

    db.Database.SetCommandTimeout(120);

    try
    {
        var pending = await db.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            logger.LogInformation("Applying {Count} pending migrations...", pending.Count());
            await db.Database.MigrateAsync();
        }
        else
        {
            logger.LogInformation("No pending EF migrations.");
        }
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 1801)
    {
        logger.LogWarning(ex, "Database '{Database}' already exists. Skipping creation.", db.Database.GetDbConnection().Database);
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (
        ex.Number == 2 ||
        ex.Number == 40 ||
        ex.Number == 53 ||
        ex.Number == -2 ||
        ex.Number == 4060 ||
        ex.Number == 18456 ||
        ex.Number == 18487)
    {
        logger.LogWarning(ex,
            "SQL Server is unavailable during startup database initialization. Continuing without migrations so the API can still boot. Configure a reachable connection string to enable startup initialization.");
    }

    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InstitutionInvitations_TokenString')
            BEGIN
                CREATE INDEX IX_InstitutionInvitations_TokenString ON InstitutionInvitations(TokenString);
            END

            IF EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('InstitutionSessionTokens')
                  AND name = 'TokenString'
                  AND max_length = -1
            )
            BEGIN
                ALTER TABLE InstitutionSessionTokens ALTER COLUMN TokenString nvarchar(255) NOT NULL;
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InstitutionSessionTokens_TokenString')
            BEGIN
                CREATE INDEX IX_InstitutionSessionTokens_TokenString ON InstitutionSessionTokens(TokenString);
            END
        ");
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (
        ex.Number == -2 ||
        ex.Number == 2 ||
        ex.Number == 40 ||
        ex.Number == 53 ||
        ex.Number == 4060 ||
        ex.Number == 18456 ||
        ex.Number == 18487)
    {
        logger.LogWarning(ex, "Database index/column migration could not run because SQL Server is unavailable. The app will continue without this tuning step.");
    }

    if (runDevSeed)
    {
        // Seed lookup data: institutions and departments (without demo data)
        await DevLookupSeed.EnsureBranchesExistAsync(db);
        await DevLookupSeed.EnsureDepartmentsAndRequirementsAsync(db);

        // Skip demo data seeding to keep database clean
        // await DevelopmentDataSeeder.SeedAsync(
        //     db,
        //     scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
        //     scope.ServiceProvider.GetRequiredService<RoleManager<Role>>(),
        //     scope.ServiceProvider.GetRequiredService<IDocumentService>(),
        //     scope.ServiceProvider.GetRequiredService<IComplianceService>());
    }

    startupTimer.Stop();
    logger.LogInformation("Startup database initialization completed in {ElapsedMs} ms.", startupTimer.ElapsedMilliseconds);
}

// Ensure the seeded Super Admin user exists on startup (best-effort).
// This user is granted full access via a dedicated claim, without relying on a special seeded role.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var userMgr = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<FourierIT_API.Models.User>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var superUserName = builder.Configuration["SuperAdmin:Username"] ?? "superadmin";
        // The password comes from User Secrets (dotnet user-secrets set "SuperAdmin:Password" "...") or an environment
        // variable (SuperAdmin__Password); it is never kept in a tracked file.
        var superPassword = builder.Configuration["SuperAdmin:Password"];
        var superEmail = builder.Configuration["SuperAdmin:Email"] ?? "superadmin@fourier.local";

        var superUser = await userMgr.FindByNameAsync(superUserName);
        if (superUser == null && string.IsNullOrWhiteSpace(superPassword))
        {
            logger.LogWarning("No Super Admin password is configured, so the Super Admin account was not created. Set it with: dotnet user-secrets set \"SuperAdmin:Password\" \"<password>\"");
        }
        else if (superUser == null)
        {
            superUser = new FourierIT_API.Models.User
            {
                UserName = superUserName,
                NormalizedUserName = superUserName.ToUpperInvariant(),
                Email = superEmail,
                NormalizedEmail = superEmail.ToUpperInvariant(),
                EmailConfirmed = true,
                AccountStatus = "Active"
            };

            var createResult = await userMgr.CreateAsync(superUser, superPassword);
            if (!createResult.Succeeded)
            {
                logger.LogWarning("Failed to seed Super Admin user: {Errors}", string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // If the configured Super Admin password has changed since the account was first seeded,
            // reset the password so the configured credential remains valid.
            if (!string.IsNullOrWhiteSpace(superPassword) && !await userMgr.CheckPasswordAsync(superUser, superPassword))
            {
                var resetToken = await userMgr.GeneratePasswordResetTokenAsync(superUser);
                var resetResult = await userMgr.ResetPasswordAsync(superUser, resetToken, superPassword);
                if (!resetResult.Succeeded)
                {
                    logger.LogWarning("Failed to reset Super Admin password: {Errors}", string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                }
            }
        }
    }
    catch (Exception ex)
    {
        // Best-effort only; do not crash the app if seeding fails.
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Super Admin seeding encountered an exception.");
    }
}

app.Run();
