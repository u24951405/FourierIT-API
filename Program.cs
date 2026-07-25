using FourierIT_API.Data;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Repositories;
using FourierIT_API.Security;
using FourierIT_API.Service;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
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
builder.Services.AddScoped<IComplianceService, ComplianceService>();

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
}).
AddEntityFrameworkStores<AppDbContext>();

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
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JWT:SigningKey"])
            )
    };
});

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<DepartmentRequestValidationService>();
// Register SuperAdmin authorization handler so the seeded Super Admin user bypasses role-based checks
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, FourierIT_API.Security.SuperAdminRoleHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AngularClient");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Note: Automatic migration and seeding for development.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DevLookupSeed.EnsureBranchesExistAsync(db);
    await DevLookupSeed.EnsureDepartmentsAndRequirementsAsync(db);
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
        var superPassword = builder.Configuration["SuperAdmin:Password"] ?? "Sup3r@dmin!";
        var superEmail = builder.Configuration["SuperAdmin:Email"] ?? "superadmin@fourier.local";

        var superUser = await userMgr.FindByNameAsync(superUserName);
        if (superUser == null)
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
