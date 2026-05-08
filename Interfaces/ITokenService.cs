using FourierIT_API.Models;

namespace FourierIT_API.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}
