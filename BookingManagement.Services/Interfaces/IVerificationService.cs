using System.Threading.Tasks;

namespace BookingManagement.Services.Interfaces
{
    public interface IVerificationService
    {
        string GenerateVerificationCode();
        Task<bool> StoreVerificationCodeAsync(string email, string code);
        Task<bool> ValidateVerificationCodeAsync(string email, string code);
        Task<bool> IsVerificationCodeValidAsync(string email, string code);
        Task RemoveVerificationCodeAsync(string email);
    }
}
