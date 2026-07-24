using FourierIT_API.Interfaces;
using FourierIT_API.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Threading.Tasks;

namespace FourierIT_API.Services
{
    public class ExternalEntityVerificationService : IEntityVerificationService
    {
        private readonly HttpClient _httpClient;
        private readonly EntityVerificationOptions _options;

        public ExternalEntityVerificationService(HttpClient httpClient, IOptions<EntityVerificationOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<EntityVerificationResult> VerifyEntityAsync(int entityTypeId, string identificationNumber)
        {
            if (!_options.EnableExternalVerification)
                return EntityVerificationResult.Valid(false);

            var endpoint = GetEndpointForEntity(entityTypeId);
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return EntityVerificationResult.Invalid("No external verification endpoint is configured for this entity type.", false, true);
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Replace("{id}", Uri.EscapeDataString(identificationNumber)));
                if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                {
                    request.Headers.Add(_options.ApiKeyHeaderName, _options.ApiKey);
                }

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return EntityVerificationResult.Invalid($"External verification failed with status code {response.StatusCode}.", true, true);
                }

                var payload = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(payload);

                if (IsValidResponse(doc.RootElement))
                {
                    return EntityVerificationResult.Valid(true);
                }

                var errorMessage = GetErrorMessage(doc.RootElement)
                    ?? "External verification provider returned invalid data.";
                return EntityVerificationResult.Invalid(errorMessage, true, false);
            }
            catch (JsonException)
            {
                return EntityVerificationResult.Invalid("Received unexpected response from external verification provider.", true, true);
            }
            catch (HttpRequestException ex)
            {
                return EntityVerificationResult.Invalid($"External verification request failed: {ex.Message}", true, true);
            }
            catch (TaskCanceledException)
            {
                return EntityVerificationResult.Invalid("External verification request timed out.", true, true);
            }
        }

        private static bool IsValidResponse(JsonElement root)
        {
            return TryGetBoolean(root, "isValid", out var isValid) && isValid
                || TryGetBoolean(root, "valid", out var valid) && valid
                || TryGetBoolean(root, "verified", out var verified) && verified
                || TryGetString(root, "status", out var status) && status.Equals("verified", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetErrorMessage(JsonElement root)
        {
            if (TryGetString(root, "message", out var message))
            {
                return message;
            }

            if (TryGetString(root, "error", out var error))
            {
                return error;
            }

            return null;
        }

        private static bool TryGetBoolean(JsonElement root, string propertyName, out bool value)
        {
            value = false;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var token))
            {
                return false;
            }

            if (token.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }

            if (token.ValueKind == JsonValueKind.False)
            {
                return true;
            }

            return false;
        }

        private static bool TryGetString(JsonElement root, string propertyName, out string value)
        {
            value = string.Empty;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var token))
            {
                return false;
            }

            if (token.ValueKind == JsonValueKind.String)
            {
                value = token.GetString() ?? string.Empty;
                return true;
            }

            return false;
        }

        private string GetEndpointForEntity(int entityTypeId)
        {
            return entityTypeId switch
            {
                1 => _options.SouthAfricanIdApiUrl,
                2 => _options.PassportApiUrl,
                3 => _options.CompanyRegistrationApiUrl,
                4 => _options.TrustRegistrationApiUrl,
                5 => _options.PartnershipRegistrationApiUrl,
                6 => _options.OtherEntityApiUrl,
                _ => string.Empty
            };
        }
    }
}
