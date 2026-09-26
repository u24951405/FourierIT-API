using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using FourierIT_API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FourierIT_API.Service
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;
        private readonly SymmetricSecurityKey _key;
        private readonly UserManager<User> _userManager;
        private readonly ISystemSettingsService _settings;
        public TokenService(IConfiguration config, UserManager<User> userManager, ISystemSettingsService settings)
        {
            _config = config;
            var signingKey = _config["JWT:SigningKey"] ?? throw new System.InvalidOperationException("JWT:SigningKey is required.");
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            _userManager = userManager;
            _settings = settings;
        }
        public async Task<string> CreateTokenAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.GivenName, user.UserName ?? string.Empty)
            };

            // Add roles as claims so the authorization of a specific role works
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // If this is the seeded Super Admin account, add a dedicated bypass claim.
            var superUserName = _config["SuperAdmin:Username"] ?? "superadmin";
            if (string.Equals(user.UserName, superUserName, StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim("superadmin", "true"));
            }

            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);
            // The Super Admin sets how long staff stay signed in (System Settings > Sessions).
            var sessionHours = await _settings.GetAsync(SystemSettingDefinitions.StaffSessionTimeoutHours);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(sessionHours),
                SigningCredentials = creds,
                Issuer = _config["JWT:Issuer"],
                Audience = _config["JWT:Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}
