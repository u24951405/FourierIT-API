using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    public interface ITokenService
    {
        Task<string> CreateTokenAsync(User user);
    }
}
