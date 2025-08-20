using System.Threading.Tasks;

namespace BookingManagement.Services.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendVerificationEmailAsync(string email, string verificationCode);
        Task<bool> SendEmailAsync(string email, string subject, string htmlMessage);
    }
}
