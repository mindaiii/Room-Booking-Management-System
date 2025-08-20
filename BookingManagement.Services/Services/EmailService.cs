using BookingManagement.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace BookingManagement.Services.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendVerificationEmailAsync(string email, string verificationCode)
        {
            var subject = "Xác nhận đăng ký tài khoản FPTU-RBS";
            var htmlMessage = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                    <div style='background: linear-gradient(135deg, #f27125, #ff8a50); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
                        <h1 style='color: white; margin: 0; font-size: 28px;'>FPTU-RBS</h1>
                        <p style='color: white; margin: 10px 0 0 0; font-size: 16px;'>Hệ thống đặt phòng FPT University</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border: 1px solid #ddd; border-top: none;'>
                        <h2 style='color: #333; margin-bottom: 20px;'>Xác nhận đăng ký tài khoản</h2>
                        <p style='color: #666; line-height: 1.6; margin-bottom: 20px;'>
                            Xin chào,<br><br>
                            Bạn đã đăng ký tài khoản tại hệ thống đặt phòng FPT University. 
                            Vui lòng sử dụng mã xác nhận bên dưới để hoàn tất quá trình đăng ký:
                        </p>
                        
                        <div style='background: #f8f9fa; border: 2px dashed #f27125; padding: 25px; text-align: center; margin: 25px 0; border-radius: 8px;'>
                            <p style='margin: 0; color: #666; font-size: 14px; margin-bottom: 10px;'>Mã xác nhận của bạn:</p>
                            <div style='background: white; border: 1px solid #f27125; border-radius: 5px; padding: 15px; display: inline-block;'>
                                <h1 style='margin: 0; color: #f27125; font-size: 36px; letter-spacing: 5px; font-family: monospace; font-weight: bold;'>{verificationCode}</h1>
                            </div>
                        </div>
                        
                        <p style='color: #666; line-height: 1.6; margin-bottom: 20px;'>
                            Mã xác nhận này có hiệu lực trong <strong>5 phút</strong>. 
                            Vui lòng không chia sẻ mã này với bất kỳ ai khác.
                        </p>
                        
                        <div style='background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 0; color: #856404; font-size: 14px;'>
                                <strong>Lưu ý:</strong> Nếu bạn không thực hiện đăng ký này, vui lòng bỏ qua email này.
                            </p>
                        </div>
                    </div>
                    
                    <div style='background: #f8f9fa; padding: 20px; text-align: center; border-radius: 0 0 10px 10px; border: 1px solid #ddd; border-top: none;'>
                        <p style='margin: 0; color: #666; font-size: 12px;'>
                            © 2025 FPT University - Room Booking System<br>
                            Email này được gửi tự động, vui lòng không trả lời.
                        </p>
                    </div>
                </div>";

            return await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task<bool> SendEmailAsync(string email, string subject, string htmlMessage)
        {
            try
            {
                // Cấu hình SMTP - bạn có thể thay đổi trong appsettings.json
                var smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["EmailSettings:Username"] ?? "your-email@gmail.com";
                var smtpPassword = _configuration["EmailSettings:Password"] ?? "your-app-password";
                var fromEmail = _configuration["EmailSettings:FromEmail"] ?? smtpUsername;
                var fromName = _configuration["EmailSettings:FromName"] ?? "FPTU-RBS System";

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Email sent successfully to {email}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {email}");
                return false;
            }
        }
    }
}
