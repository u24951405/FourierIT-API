using FourierIT_API.Data;
using FourierIT_API.DTOs.User;
using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Linq.Expressions;
using System.Net;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;

namespace FourierIT_API.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly SignInManager<User> _signInManager;
        private readonly AppDbContext _context;
        private readonly RoleManager<Role> _roleManager;
        private readonly IEntityVerificationService _entityVerificationService;
        private readonly IConfiguration _configuration;
        private readonly IAuditLogService _auditLogService;
        private readonly IEmailService _emailService;
        private readonly IFileScanService _fileScanService;

        public UserController(
            UserManager<User> userManager,
            ITokenService tokenService,
            SignInManager<User> signInManager,
            AppDbContext context,
            RoleManager<Role> roleManager,
            IEntityVerificationService entityVerificationService,
            IConfiguration configuration,
            IAuditLogService auditLogService,
            IEmailService emailService,
            IFileScanService fileScanService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
            _entityVerificationService = entityVerificationService;
            _configuration = configuration;
            _auditLogService = auditLogService;
            _emailService = emailService;
            _fileScanService = fileScanService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)//This login method first checks if the user’s input is valid, then looks for the user in the database, verifies that the password is correct, creates a login token if everything matches, and finally returns the user’s details and token so they can access the system
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var username = loginDto.Username?.Trim() ?? string.Empty;
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                // Fall back to searching by normalized username if direct lookup fails.
                user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.NormalizedUserName == username.ToUpperInvariant());
            }

            if (user == null)
            {
                await TryCreateAuditLogAsync(new AuditLog
                {
                    UserId = null,
                    ActionCode = "LOGIN_FAILURE",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = "Failed login attempt with invalid username.",
                    TableAffected = "Users",
                    RecordID = null
                });
                return Unauthorized("Invalid username");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded)
            {
                await TryCreateAuditLogAsync(new AuditLog
                {
                    UserId = user.Id,
                    ActionCode = "LOGIN_FAILURE",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = "Failed login attempt due to incorrect credentials.",
                    TableAffected = "Users",
                    RecordID = null
                });
                return Unauthorized("Username not found and/or password incorrect");
            }

            if (!string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                await TryCreateAuditLogAsync(new AuditLog
                {
                    UserId = user.Id,
                    ActionCode = "LOGIN_FAILURE",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = "Blocked login for account that is awaiting email verification.",
                    TableAffected = "Users",
                    RecordID = null
                });
                return Unauthorized("Please verify your email address before signing in.");
            }

            var token = await _tokenService.CreateTokenAsync(user);

            await TryCreateAuditLogAsync(new AuditLog
            {
                UserId = user.Id,
                ActionCode = "LOGIN_SUCCESS",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = "User successfully logged in.",
                TableAffected = "Users",
                RecordID = null
            });

            return Ok(
                new NewUserDto
                {
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Token = token
                });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentAccount()
        {
            // JWT short names (e.g. "email", "sub") are often not mapped to ClaimTypes.* unless MapInboundClaims is configured.
            var user = await ResolveCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { error = "Invalid token." });

            var roles = (await _userManager.GetRolesAsync(user)).ToList();
            var profile = await _context.Profiles
                .AsNoTracking()
                .Include(p => p.ProfileImageBlob)
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            var department = user.DepartmentId.HasValue
                ? await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == user.DepartmentId.Value)
                : null;

            var dto = new CurrentAccountDto
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                AccountStatus = user.AccountStatus,
                PhoneNumber = user.PhoneNumber,
                Roles = roles,
                ProfileId = profile?.ProfileId,
                FirstName = profile?.FirstName,
                LastName = profile?.LastName,
                ProfilePhoneNumber = profile?.PhoneNumber,
                JobTitle = profile?.JobTitle,
                DateOfBirth = profile?.DateOfBirth,
                ProfileImageUrl = profile?.ProfileImageBlob != null
                    ? BuildProfileImageUrl(user.Id)
                    : null,
                DepartmentId = user.DepartmentId,
                DepartmentName = department?.DepartmentName
            };

            return Ok(dto);
        }

        [Authorize]
        [HttpPost("profile-image")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> UploadProfileImage([FromForm] IFormFile file)
        {
            var user = await ResolveCurrentUserAsync();
            if (user == null) return Unauthorized(new { error = "Invalid token." });
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "An image file is required." });
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { error = "Profile images must be 5 MB or smaller." });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".jpg" or ".jpeg" when file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) => "image/jpeg",
                ".png" when file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) => "image/png",
                ".webp" when file.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) => "image/webp",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(mimeType))
                return BadRequest(new { error = "Only JPG, PNG, and WebP images are supported." });

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var fileData = stream.ToArray();
            stream.Position = 0;
            var scanResult = await _fileScanService.ScanFileAsync(stream);
            if (!scanResult.IsClean)
                return BadRequest(new { error = scanResult.Message });

            var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                profile = new Profile
                {
                    UserId = user.Id,
                    FirstName = user.UserName ?? "User",
                    LastName = "",
                    DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18)),
                    PhoneNumber = string.Empty,
                    JobTitle = string.Empty
                };
                _context.Profiles.Add(profile);
                await _context.SaveChangesAsync();
            }

            var imageBlob = await _context.ProfileImageBlobs
                .FirstOrDefaultAsync(blob => blob.ProfileId == profile.ProfileId);
            if (imageBlob == null)
            {
                imageBlob = new ProfileImageBlob { ProfileId = profile.ProfileId };
                _context.ProfileImageBlobs.Add(imageBlob);
            }

            imageBlob.FileData = fileData;
            imageBlob.MimeType = mimeType;
            imageBlob.FileHash = Convert.ToHexString(SHA256.HashData(fileData));
            imageBlob.UploadedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Profile image uploaded successfully.",
                imageUrl = BuildProfileImageUrl(user.Id)
            });
        }

        [AllowAnonymous]
        [HttpGet("profile-image/{userId}")]
        public async Task<IActionResult> GetProfileImage(string userId)
        {
            var imageBlob = await _context.ProfileImageBlobs
                .AsNoTracking()
                .Include(blob => blob.Profile)
                .FirstOrDefaultAsync(blob => blob.Profile.UserId == userId);

            return imageBlob == null
                ? NotFound()
                : File(imageBlob.FileData, imageBlob.MimeType);
        }

        private string BuildProfileImageUrl(string userId) =>
            $"{Request.Scheme}://{Request.Host}/api/user/profile-image/{Uri.EscapeDataString(userId)}";

        [Authorize]
        [HttpDelete("profile-image")]
        public async Task<IActionResult> DeleteProfileImage()
        {
            var user = await ResolveCurrentUserAsync();
            if (user == null) return Unauthorized(new { error = "Invalid token." });

            var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null) return NotFound(new { error = "Profile not found." });

            var imageBlob = await _context.ProfileImageBlobs
                .FirstOrDefaultAsync(blob => blob.ProfileId == profile.ProfileId);
            if (imageBlob != null)
            {
                _context.ProfileImageBlobs.Remove(imageBlob);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Profile image removed." });
        }

        private async Task<User?> ResolveCurrentUserAsync()
        {
            var email =
                User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.Claims.FirstOrDefault(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))?.Value;

            if (!string.IsNullOrWhiteSpace(email))
            {
                // Trim email to handle cases where email has leading/trailing whitespace
                var trimmedEmail = email.Trim();
                var byEmail = await _userManager.FindByEmailAsync(trimmedEmail);
                if (byEmail != null) return byEmail;
            }

            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? User.Claims.FirstOrDefault(c => c.Type.Equals("sub", StringComparison.OrdinalIgnoreCase))?.Value;

            if (!string.IsNullOrWhiteSpace(userId))
                return await _userManager.FindByIdAsync(userId);

            return null;
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = request.EmailAddress.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Do not reveal that the email is not registered.
                return Ok(new { message = "If an account exists for this email, a password reset link has been sent." });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var frontendBase = _configuration["EmailSettings:FrontendBaseUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
            var resetLink = $"{frontendBase}/auth/reset-password?email={WebUtility.UrlEncode(email)}&token={encodedToken}";
            var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

            await _emailService.SendPasswordResetEmailAsync(email, resetLink, expiresAt);

            return Ok(new { message = "If an account exists for this email, a password reset link has been sent." });
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = request.EmailAddress.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Avoid leaking registered emails.
                return BadRequest(new { error = "Invalid password reset request." });
            }

            var resetResult = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!resetResult.Succeeded)
            {
                return BadRequest(new
                {
                    error = "Password reset failed.",
                    details = resetResult.Errors.Select(e => e.Description)
                });
            }

            return Ok(new { message = "Your password has been reset successfully." });
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserDto userDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = userDto.EmailAddress.Trim();
            var normalizedEmail = email.ToUpperInvariant();
            if (await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail || u.Email == email))
            {
                return Conflict(new
                {
                    error = "An account with this email address already exists. Please sign in or use a different email address."
                });
            }

                // Build requested roles early so we can decide entity assignment and validate roles before creating the user
                var requestedRoles = (userDto.Roles ?? new List<string>())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Backward compatibility for older clients sending a single role.
                if (requestedRoles.Count == 0 && !string.IsNullOrWhiteSpace(userDto.Role))
                {
                    requestedRoles.Add(userDto.Role.Trim());
                }

                // Only Super Admin can assign roles other than Document Owner during registration
                // All normal users are registered as Document Owner by default
                var currentUser = await _userManager.GetUserAsync(User);
                var isSuperAdminRequest = currentUser != null && IsSuperAdminUser(currentUser);

                if (!isSuperAdminRequest)
                {
                    // Normal user registration: always Document Owner, ignore any requested roles
                    requestedRoles = new List<string> { "Document Owner" };
                }
                else if (requestedRoles.Count == 0)
                {
                    // Super Admin can explicitly request roles, or default to Document Owner
                    requestedRoles.Add("Document Owner");
                }

                if (requestedRoles.Count > 2)
                {
                    return BadRequest(new { error = "A user can be assigned a maximum of 2 roles during registration." });
                }

                // Validate roles exist in DB and stakeholder exclusivity BEFORE creating the user
                var allowedRoleNames = await _context.Roles
                    .Select(r => r.Name ?? string.Empty)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToListAsync();

                var invalidRoles = requestedRoles
                    .Where(r => !allowedRoleNames.Any(ar => ar.Equals(r, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (invalidRoles.Any())
                {
                    return BadRequest(new { error = "Invalid role(s)", invalidRoles, allowedRoles = allowedRoleNames });
                }

                if (StakeholderRolePolicy.ViolatesStakeholderExclusivity(requestedRoles))
                {
                    return BadRequest(new
                    {
                        error = "Stakeholder cannot be combined with other roles. Choose only Stakeholder, or remove Stakeholder and pick other role(s)."
                    });
                }

                var verificationNumber = userDto.EntityIdentificationNumber?.Trim();
                var requiresEntityVerification = requestedRoles.Any(r =>
                    string.Equals(r, "Document Owner", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r, "Department Admin", StringComparison.OrdinalIgnoreCase)
                );

                if (requiresEntityVerification)
                {
                    if (!userDto.EntityTypeId.HasValue)
                    {
                        return BadRequest(new { error = "Please select a valid entity type." });
                    }

                    var entityType = await _context.EntityTypes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(et => et.EntityTypeId == userDto.EntityTypeId.Value);

                    if (entityType == null)
                    {
                        return BadRequest(new { error = "Please select a valid entity type." });
                    }

                    if (string.IsNullOrWhiteSpace(verificationNumber))
                    {
                        return BadRequest(new { error = "Please enter an identification or registration number for the selected entity type." });
                    }

                    var expectedLengthMessage = ValidateEntityIdentificationNumber(userDto.EntityTypeId.Value, verificationNumber);
                    if (expectedLengthMessage != null)
                    {
                        return BadRequest(new { error = expectedLengthMessage });
                    }

                    var verificationResult = await _entityVerificationService.VerifyEntityAsync(userDto.EntityTypeId.Value, verificationNumber);
                    if (!verificationResult.IsValid)
                    {
                        return BadRequest(new { error = verificationResult.ErrorMessage ?? "Entity verification failed." });
                    }
                }

                var otpCode = GenerateOtpCode();
                var otpExpiry = DateTimeOffset.UtcNow.AddMinutes(15);
                var otpHash = HashOtpCode(otpCode);
                var pending = await _context.PendingRegistrations
                    .FirstOrDefaultAsync(r => r.NormalizedEmail == normalizedEmail);

                if (pending != null && pending.OtpExpiry <= DateTimeOffset.UtcNow)
                {
                    _context.PendingRegistrations.Remove(pending);
                    await _context.SaveChangesAsync();
                    pending = null;
                }

                var passwordHash = _userManager.PasswordHasher.HashPassword(new User(), userDto.Password!);
                if (pending == null)
                {
                    pending = new PendingRegistration();
                    _context.PendingRegistrations.Add(pending);
                }

                pending.Email = email;
                pending.NormalizedEmail = normalizedEmail;
                pending.UserName = userDto.Username.Trim().ToLowerInvariant();
                pending.NormalizedUserName = pending.UserName.ToUpperInvariant();
                pending.PasswordHash = passwordHash;
                pending.FirstName = userDto.FirstName.Trim();
                pending.LastName = userDto.LastName.Trim();
                pending.DateOfBirth = userDto.DateOfBirth;
                pending.PhoneNumber = userDto.PhoneNumber?.Trim() ?? string.Empty;
                pending.JobTitle = userDto.JobTitle.Trim();
                pending.EntityTypeId = userDto.EntityTypeId;
                pending.EntityIdentificationNumber = verificationNumber ?? string.Empty;
                pending.RequestedRolesJson = JsonSerializer.Serialize(requestedRoles);
                pending.OtpHash = otpHash;
                pending.OtpExpiry = otpExpiry;
                pending.CreatedAt = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync();

                try
                {
                    await _emailService.SendUserRegistrationOtpEmailAsync(pending.Email, otpCode, otpExpiry);
                }
                catch
                {
                    _context.PendingRegistrations.Remove(pending);
                    await _context.SaveChangesAsync();
                    throw;
                }

                await TryCreateAuditLogAsync(new AuditLog
                {
                    UserId = null,
                    ActionCode = "USER_REGISTRATION_PENDING_VERIFICATION",
                    TimeStamp = DateTimeOffset.UtcNow,
                    Description = "New pending registration created and awaiting email verification.",
                    TableAffected = "PendingRegistrations",
                    RecordID = null
                });

                return Ok(new
                {
                    message = "We sent a verification code to your email. Enter it to complete registration.",
                    email = pending.Email,
                    requiresVerification = true
                });
        }

        [AllowAnonymous]
        [HttpPost("verify-registration-otp")]
        public async Task<IActionResult> VerifyRegistrationOtp([FromBody] VerifyRegistrationOtpRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var email = request.EmailAddress?.Trim() ?? string.Empty;
            var pending = await _context.PendingRegistrations
                .FirstOrDefaultAsync(r => r.NormalizedEmail == email.ToUpperInvariant());
            if (pending == null)
            {
                return BadRequest(new { error = "We could not find a pending registration for this email address." });
            }

            if (pending.OtpExpiry <= DateTimeOffset.UtcNow)
            {
                _context.PendingRegistrations.Remove(pending);
                await _context.SaveChangesAsync();
                return BadRequest(new { error = "The verification code has expired. Please register again." });
            }

            if (!VerifyOtpCode(request.Otp, pending.OtpHash, new User()))
            {
                return BadRequest(new { error = "The verification code is invalid." });
            }

            var requestedRoles = JsonSerializer.Deserialize<List<string>>(pending.RequestedRolesJson) ?? new List<string>();
            var user = new User
            {
                UserName = pending.UserName,
                NormalizedUserName = pending.NormalizedUserName,
                Email = pending.Email,
                NormalizedEmail = pending.NormalizedEmail,
                PasswordHash = pending.PasswordHash,
                PhoneNumber = pending.PhoneNumber,
                AccountStatus = "Active",
                EmailVerified = true,
                EntityTypeId = pending.EntityTypeId,
                EntityIdentificationNumber = pending.EntityIdentificationNumber
            };

            var materializeResult = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = _context.Database.IsRelational()
                    ? await _context.Database.BeginTransactionAsync()
                    : null;

                try
                {
                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        return (Result: (IActionResult?)BadRequest(createResult.Errors), Commit: false);
                    }

                    var roleResult = await _userManager.AddToRolesAsync(user, requestedRoles);
                    if (!roleResult.Succeeded)
                    {
                        return (Result: (IActionResult?)StatusCode(StatusCodes.Status500InternalServerError, roleResult.Errors), Commit: false);
                    }

                    _context.Profiles.Add(new Profile
                    {
                        FirstName = pending.FirstName,
                        LastName = pending.LastName,
                        DateOfBirth = pending.DateOfBirth,
                        PhoneNumber = pending.PhoneNumber,
                        JobTitle = pending.JobTitle,
                        UserId = user.Id
                    });
                    _context.PendingRegistrations.Remove(pending);
                    await _context.SaveChangesAsync();

                    if (transaction != null)
                    {
                        await transaction.CommitAsync();
                    }

                    return (Result: (IActionResult?)null, Commit: true);
                }
                catch
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync();
                    }

                    throw;
                }
            });

            if (materializeResult.Result != null)
            {
                return materializeResult.Result;
            }

            var token = await _tokenService.CreateTokenAsync(user);

            await TryCreateAuditLogAsync(new AuditLog
            {
                UserId = user.Id,
                ActionCode = "USER_EMAIL_VERIFIED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = "User email verification completed successfully.",
                TableAffected = "Users",
                RecordID = null
            });

            return Ok(new
            {
                message = "Email verified successfully. You can now sign in.",
                token
            });
        }

        private static string GenerateOtpCode()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(4);
            var value = BitConverter.ToInt32(randomBytes, 0);
            var normalized = Math.Abs(value % 1000000);
            return normalized.ToString("D6");
        }

        private static string HashOtpCode(string otpCode)
        {
            var passwordHasher = new PasswordHasher<User>();
            return passwordHasher.HashPassword(new User(), otpCode);
        }

        private static bool VerifyOtpCode(string otpCode, string? storedHash, User user)
        {
            if (string.IsNullOrWhiteSpace(otpCode) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            var passwordHasher = new PasswordHasher<User>();
            return passwordHasher.VerifyHashedPassword(user, storedHash, otpCode) == PasswordVerificationResult.Success;
        }

        [AllowAnonymous]
        [HttpPost("verify-entity")]
        public async Task<IActionResult> VerifyEntity([FromBody] VerifyEntityRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _entityVerificationService.VerifyEntityAsync(request.EntityTypeId, request.IdentificationNumber?.Trim() ?? string.Empty);
            return Ok(new
            {
                isValid = result.IsValid,
                message = result.ErrorMessage,
                providerUnavailable = result.ProviderUnavailable
            });
        }

        [AllowAnonymous]
        [HttpGet("entity-types")]
        public async Task<IActionResult> GetEntityTypes()
        {
            var entityTypes = await _context.EntityTypes
                .AsNoTracking()
                .OrderBy(et => et.EntityTypeId)
                .Select(et => new
                {
                    et.EntityTypeId,
                    et.Name
                })
                .ToListAsync();

            return Ok(entityTypes);
        }

        [AllowAnonymous]
        [HttpGet("password-policy")]
        public IActionResult GetPasswordPolicy()
        {
            var passwordOptions = _userManager.Options.Password;
            
            return Ok(new
            {
                requireDigit = passwordOptions.RequireDigit,
                requireLowercase = passwordOptions.RequireLowercase,
                requireUppercase = passwordOptions.RequireUppercase,
                requireNonAlphanumeric = passwordOptions.RequireNonAlphanumeric,
                requiredLength = passwordOptions.RequiredLength
            });
        }

        private bool IsSuperAdminUser(User? user)
        {
            if (user == null) return false;
            var superUserName = _configuration["SuperAdmin:Username"] ?? "superadmin";
            return string.Equals(user.UserName, superUserName, StringComparison.OrdinalIgnoreCase);
        }

        private async Task TryCreateAuditLogAsync(AuditLog auditLog)
        {
            if (auditLog == null) return;

            try
            {
                await _auditLogService.CreateAuditLogAsync(auditLog);
            }
            catch
            {
                // Swallow audit failures so auth flows are not affected.
            }
        }

        private static string? ValidateEntityIdentificationNumber(int entityTypeId, string verificationNumber)
        {
            return entityTypeId switch
            {
                1 when !verificationNumber.All(char.IsDigit) || verificationNumber.Length != 13 => "South African ID numbers must be exactly 13 digits.",
                2 when verificationNumber.Length < 4 => "Passport numbers must be at least 4 characters.",
                3 when verificationNumber.Length < 4 => "Company registration numbers must be at least 4 characters.",
                4 when verificationNumber.Length < 4 => "Trust registration numbers must be at least 4 characters.",
                5 when verificationNumber.Length < 4 => "Partnership registration numbers must be at least 4 characters.",
                6 when verificationNumber.Length < 4 => "Please enter a reference number for this entity type.",
                _ => null
            };
        }

        [Authorize(Policy = "Users.Manage")]
        [Authorize]
        [HttpPut("profile/{profileId}")]
        public async Task<IActionResult> UpdateManagedUser([FromRoute] int profileId, [FromBody] UpdateUserManagementRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var profile = await _context.Profiles.Include(p => p.User).FirstOrDefaultAsync(p => p.ProfileId == profileId);
            if (profile == null) return NotFound(new { error = "Profile not found." });

            var user = profile.User ?? await _userManager.FindByIdAsync(profile.UserId);
            if (user == null) return NotFound(new { error = "User not found for the profile." });
            var currentUser = await ResolveCurrentUserAsync();
            var isSelfEdit = currentUser?.Id == user.Id;
            if (!isSelfEdit && !User.IsInRole("Admin") && !User.IsInRole("Department Admin"))
                return Forbid();

            var roleName = dto.Role.Trim();
            if (!isSelfEdit && !await _roleManager.RoleExistsAsync(roleName))
            {
                var allowedRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
                return BadRequest(new { error = "Invalid role", allowedRoles });
            }

            profile.FirstName = dto.FirstName.Trim();
            profile.LastName = dto.LastName.Trim();
            profile.DateOfBirth = dto.DateOfBirth;
            profile.PhoneNumber = dto.PhoneNumber.Trim();
            profile.JobTitle = dto.JobTitle.Trim();

            user.Email = dto.EmailAddress.Trim();
            user.NormalizedEmail = dto.EmailAddress.Trim().ToUpperInvariant();
            user.AccountStatus = string.IsNullOrWhiteSpace(dto.AccountStatus) ? "Active" : dto.AccountStatus.Trim();
            var userUpdateResult = await _userManager.UpdateAsync(user);
            if (!userUpdateResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, userUpdateResult.Errors);

            if (!isSelfEdit)
            {
                var existingRoles = await _userManager.GetRolesAsync(user);
                var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
                if (!removeRolesResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, removeRolesResult.Errors);

                var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (!addRoleResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, addRoleResult.Errors);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "User and profile updated." });
        }

        [Authorize]
        [HttpDelete("profile/{profileId}")]
        public async Task<IActionResult> DeleteManagedUser([FromRoute] int profileId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var currentUserIsSuperAdmin = IsSuperAdminUser(currentUser);
            var currentUserIsDepartmentAdmin = await _userManager.IsInRoleAsync(currentUser, "Department Admin");
            if (!currentUserIsSuperAdmin && !currentUserIsDepartmentAdmin)
                return Forbid();

            var profile = await _context.Profiles.Include(p => p.User).FirstOrDefaultAsync(p => p.ProfileId == profileId);
            if (profile == null) return NotFound(new { error = "Profile not found." });

            var user = profile.User ?? await _userManager.FindByIdAsync(profile.UserId);
            if (user == null) return NotFound(new { error = "User not found for the profile." });
            if (IsSuperAdminUser(user)) return BadRequest(new { error = "The Super Admin account cannot be deleted." });

            await DeleteUserRelatedRecordsAsync(user);

            _context.Profiles.Remove(profile);
            await _context.SaveChangesAsync();

            var deleteUserResult = await _userManager.DeleteAsync(user);
            if (!deleteUserResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, deleteUserResult.Errors);

            await TryCreateAuditLogAsync(new AuditLog
            {
                UserId = currentUser.Id,
                ActionCode = "USER_DELETED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"User {user.Id} and related records deleted by {(currentUserIsSuperAdmin ? "Super Admin" : "Department Admin")}",
                TableAffected = "Users",
                RecordID = int.TryParse(user.Id, out var parsedId) ? parsedId : (int?)null
            });

            return NoContent();
        }

        [Authorize]
        [HttpDelete("by-user/{userId}")]
        public async Task<IActionResult> DeleteUserById([FromRoute] string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var currentUserIsSuperAdmin = IsSuperAdminUser(currentUser);
            var currentUserIsDepartmentAdmin = await _userManager.IsInRoleAsync(currentUser, "Department Admin");
            if (!currentUserIsSuperAdmin && !currentUserIsDepartmentAdmin)
                return Forbid();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { error = "User not found." });
            if (IsSuperAdminUser(user)) return BadRequest(new { error = "The Super Admin account cannot be deleted." });

            await DeleteUserRelatedRecordsAsync(user);

            var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile != null)
            {
                _context.Profiles.Remove(profile);
                await _context.SaveChangesAsync();
            }

            var deleteUserResult = await _userManager.DeleteAsync(user);
            if (!deleteUserResult.Succeeded) return StatusCode(StatusCodes.Status500InternalServerError, deleteUserResult.Errors);

            await TryCreateAuditLogAsync(new AuditLog
            {
                UserId = currentUser.Id,
                ActionCode = "USER_DELETED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"User {user.Id} and related records deleted by {(currentUserIsSuperAdmin ? "Super Admin" : "Department Admin")}",
                TableAffected = "Users",
                RecordID = int.TryParse(user.Id, out var parsedId) ? parsedId : (int?)null
            });

            return NoContent();
        }

        [Authorize(Policy = "Users.Manage")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .ToListAsync();

            var userDtos = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var profile = await _context.Profiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                userDtos.Add(new
                {
                    ProfileId = profile?.ProfileId,
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.PhoneNumber,
                    user.AccountStatus,
                    user.DepartmentId,
                    Roles = roles,
                    Profile = new
                    {
                        profile?.FirstName,
                        profile?.LastName,
                        profile?.JobTitle,
                        profile?.DateOfBirth
                    }
                });
            }

            return Ok(userDtos);
        }

        private async Task DeleteUserRelatedRecordsAsync(User user)
        {
            // Delete related document access, compliance, and audit records for the user.
            var documents = await _context.Documents
                .Include(d => d.DocumentBlob)
                .Include(d => d.CertificationDetails)
                .Include(d => d.DocumentStatusHistories)
                .Include(d => d.AccessLists)
                .Include(d => d.SharedWith)
                .Include(d => d.AccessLogs)
                .Where(d => d.UserId == user.Id)
                .ToListAsync();

            foreach (var document in documents)
            {
                _context.DocumentAccessLogs.RemoveRange(document.AccessLogs);
                _context.DocumentAccesses.RemoveRange(document.SharedWith);
                _context.AccessLists.RemoveRange(document.AccessLists);
                _context.DocumentStatusHistories.RemoveRange(document.DocumentStatusHistories);
                _context.CertificationDetails.RemoveRange(document.CertificationDetails);
                if (document.DocumentBlob != null)
                {
                    _context.BlobHistories.RemoveRange(document.DocumentBlob.BlobHistories);
                    _context.DocumentBlobs.Remove(document.DocumentBlob);
                }
                _context.Documents.Remove(document);
            }

            var documentAccessApprovals = await _context.DocumentAccessApprovals
                .Where(daa => daa.ApprovedByUserId == user.Id)
                .ToListAsync();
            _context.DocumentAccessApprovals.RemoveRange(documentAccessApprovals);

            var institutionEnquiryRequests = await _context.InstitutionEnquiryRequests
                .Where(ier => ier.TargetUserId == user.Id)
                .ToListAsync();
            _context.InstitutionEnquiryRequests.RemoveRange(institutionEnquiryRequests);

            var documentAccesses = await _context.DocumentAccesses
                .Where(da => da.GrantedToUserId == user.Id)
                .ToListAsync();
            _context.DocumentAccesses.RemoveRange(documentAccesses);

            var documentAccessLogs = await _context.DocumentAccessLogs
                .Where(dal => dal.AccessedByUserId == user.Id)
                .ToListAsync();
            _context.DocumentAccessLogs.RemoveRange(documentAccessLogs);

            var userSecurityQuestions = await _context.UserSecurityQuestions
                .Where(usq => usq.UserId == user.Id)
                .ToListAsync();
            _context.UserSecurityQuestions.RemoveRange(userSecurityQuestions);

            var userNotifications = await _context.UserNotifications
                .Where(un => un.UserId == user.Id)
                .ToListAsync();
            _context.UserNotifications.RemoveRange(userNotifications);

            var clientEnlistments = await _context.ClientEnlistments
                .Where(ce => ce.UserId == user.Id)
                .ToListAsync();
            _context.ClientEnlistments.RemoveRange(clientEnlistments);

            var accessToken = await _context.AccessTokens
                .FirstOrDefaultAsync(at => at.UserId == user.Id);
            if (accessToken != null)
            {
                _context.AccessTokens.Remove(accessToken);
            }

            var enquiryComment = await _context.EnquiryComments
                .FirstOrDefaultAsync(ec => ec.UserId == user.Id);
            if (enquiryComment != null)
            {
                _context.EnquiryComments.Remove(enquiryComment);
            }

            var complianceStatus = await _context.ComplianceStatuses
                .FirstOrDefaultAsync(cs => cs.UserId == user.Id);
            if (complianceStatus != null)
            {
                _context.DocumentComplianceChecks.RemoveRange(_context.DocumentComplianceChecks.Where(dc => dc.ComplianceStatusId == complianceStatus.ComplianceStatusId));
                _context.ComplianceResults.RemoveRange(_context.ComplianceResults.Where(cr => cr.ComplianceStatusId == complianceStatus.ComplianceStatusId));
                _context.ComplianceHistories.RemoveRange(_context.ComplianceHistories.Where(ch => ch.ComplianceStatusId == complianceStatus.ComplianceStatusId));
                _context.ComplianceAlerts.RemoveRange(_context.ComplianceAlerts.Where(ca => ca.UserId == user.Id));
                _context.ComplianceAuditLogs.RemoveRange(_context.ComplianceAuditLogs.Where(al => al.PerformedBy == user.Id));
                _context.ComplianceStatuses.Remove(complianceStatus);
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// [SUPER ADMIN ONLY] Get unassigned Department Admin users (all with department info shown).
        /// </summary>
        [Authorize]
        [HttpGet("departments/admins/unassigned")]
        public async Task<IActionResult> GetUnassignedDepartmentAdmins()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !IsSuperAdminUser(currentUser))
                return Forbid();

            var allUsers = await _userManager.Users
                .ToListAsync();

            var adminDtos = new List<object>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Department Admin"))
                {
                    // Get department name if assigned
                    string? departmentName = null;
                    if (user.DepartmentId.HasValue)
                    {
                        var department = await _context.Departments
                            .FirstOrDefaultAsync(d => d.DepartmentId == user.DepartmentId.Value);
                        departmentName = department?.DepartmentName;
                    }

                    adminDtos.Add(new
                    {
                        userId = user.Id,
                        userName = user.UserName,
                        email = user.Email,
                        phoneNumber = user.PhoneNumber,
                        departmentId = user.DepartmentId,
                        departmentName = departmentName
                    });
                }
            }

            return Ok(adminDtos.OrderBy(a => ((dynamic)a).userName));
        }

        /// <summary>
        /// [SUPER ADMIN ONLY] Get department admin (if assigned) for a specific department
        /// </summary>
        [Authorize]
        [HttpGet("departments/{departmentId}/admin")]
        public async Task<IActionResult> GetDepartmentAdmin([FromRoute] int departmentId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !IsSuperAdminUser(currentUser))
                return Forbid();

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == departmentId);
            if (department == null)
                return NotFound(new { error = "Department not found." });

            var admin = await _userManager.Users
                .Where(u => u.DepartmentId == departmentId)
                .FirstOrDefaultAsync();

            if (admin == null)
                return Ok(new { departmentId, admin = (object?)null });

            var hasAdminRole = await _userManager.IsInRoleAsync(admin, "Department Admin");
            if (!hasAdminRole)
                return Ok(new { departmentId, admin = (object?)null });

            var profile = await _context.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == admin.Id);

            return Ok(new
            {
                departmentId,
                admin = new DepartmentAdminDto
                {
                    UserId = admin.Id,
                    UserName = admin.UserName ?? string.Empty,
                    Email = admin.Email ?? string.Empty,
                    DepartmentId = admin.DepartmentId,
                    DepartmentName = department.DepartmentName,
                    FirstName = profile?.FirstName ?? string.Empty,
                    LastName = profile?.LastName ?? string.Empty,
                    JobTitle = profile?.JobTitle ?? string.Empty
                }
            });
        }

        /// <summary>
        /// [SUPER ADMIN ONLY] Create or assign a Department Admin to a department.
        /// Only one active Department Admin per department is allowed.
        /// </summary>
        [Authorize]
        [HttpPost("departments/{departmentId}/admin")]
        public async Task<IActionResult> CreateOrAssignDepartmentAdmin(
            [FromRoute] int departmentId,
            [FromBody] CreateDepartmentAdminRequestDto dto)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !IsSuperAdminUser(currentUser))
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == departmentId);
            if (department == null)
                return NotFound(new { error = "Department not found." });

            if (string.IsNullOrWhiteSpace(dto.UserId))
                return BadRequest(new { error = "An existing verified UserId is required to assign a Department Admin." });

            var targetUser = await _userManager.FindByIdAsync(dto.UserId.Trim());
            if (targetUser == null)
                return NotFound(new { error = "User not found." });

            if (!targetUser.EmailConfirmed && !targetUser.EmailVerified)
                return BadRequest(new { error = "The user must complete email verification before department assignment." });

            // Check if user is already assigned to another department
            if (targetUser.DepartmentId.HasValue && targetUser.DepartmentId != departmentId)
                return BadRequest(new { error = "User is already assigned to another department." });

            // Ensure user is assigned to department
            var departmentChanged = !targetUser.DepartmentId.HasValue || targetUser.DepartmentId != departmentId;
            if (departmentChanged)
            {
                targetUser.DepartmentId = departmentId;
                await _userManager.UpdateAsync(targetUser);

                var complianceStatus = await _context.ComplianceStatuses
                    .FirstOrDefaultAsync(cs => cs.UserId == targetUser.Id);
                if (complianceStatus != null)
                {
                    complianceStatus.LastChecked = DateTime.UtcNow;
                    _context.ComplianceStatuses.Update(complianceStatus);
                    await _context.SaveChangesAsync();
                }
            }

            var departmentAdminUsers = await _userManager.Users
                .Where(u => u.DepartmentId == departmentId && u.Id != targetUser.Id)
                .ToListAsync();

            foreach (var departmentAdminUser in departmentAdminUsers)
            {
                var isCurrentAdmin = await _userManager.IsInRoleAsync(departmentAdminUser, "Department Admin");
                if (isCurrentAdmin)
                {
                    await _userManager.RemoveFromRoleAsync(departmentAdminUser, "Department Admin");
                }
            }

            // Remove any existing Department Admin role
            var existingRoles = await _userManager.GetRolesAsync(targetUser);
            if (existingRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(targetUser, existingRoles);
            }

            // Assign Department Admin role
            var roleResult = await _userManager.AddToRoleAsync(targetUser, "Department Admin");
            if (!roleResult.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, roleResult.Errors);

            var adminProfile = await _context.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == targetUser.Id);

            await _auditLogService.CreateAuditLogAsync(new AuditLog
            {
                UserId = currentUser?.Id ?? "system",
                ActionCode = "DEPARTMENT_ADMIN_ASSIGNED",
                TimeStamp = DateTimeOffset.UtcNow,
                Description = $"Assigned Department Admin '{targetUser.UserName}' ({targetUser.Email}) to department '{department.DepartmentName}' (DepartmentId={departmentId}).",
                TableAffected = "Departments",
                RecordID = departmentId
            });

            return Ok(new
            {
                message = "Department Admin assigned successfully.",
                admin = new DepartmentAdminDto
                {
                    UserId = targetUser.Id,
                    UserName = targetUser.UserName ?? string.Empty,
                    Email = targetUser.Email ?? string.Empty,
                    DepartmentId = targetUser.DepartmentId,
                    DepartmentName = department.DepartmentName,
                    FirstName = adminProfile?.FirstName ?? string.Empty,
                    LastName = adminProfile?.LastName ?? string.Empty,
                    JobTitle = adminProfile?.JobTitle ?? string.Empty,
                    DateOfBirth = adminProfile?.DateOfBirth
                }
            });
        }

        /// <summary>
        /// [SUPER ADMIN ONLY] Remove a Department Admin from a department (does not delete user).
        /// </summary>
        [Authorize]
        [HttpDelete("departments/{departmentId}/admin")]
        public async Task<IActionResult> RemoveDepartmentAdmin([FromRoute] int departmentId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !IsSuperAdminUser(currentUser))
                return Forbid();

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == departmentId);
            if (department == null)
                return NotFound(new { error = "Department not found." });

            var admin = await _userManager.Users
                .FirstOrDefaultAsync(u => u.DepartmentId == departmentId);

            if (admin == null)
                return NotFound(new { error = "No Department Admin assigned to this department." });

            var removeResult = await _userManager.RemoveFromRoleAsync(admin, "Department Admin");
            if (!removeResult.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, removeResult.Errors);

            return Ok(new { message = "Department Admin removed successfully." });
        }
    }
}
