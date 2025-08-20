using System.ComponentModel.DataAnnotations;

namespace BookingManagement.Services.DTOs
{
    public class VerificationDto
    {
        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã xác nhận là bắt buộc")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác nhận phải có 6 chữ số")]
        public string VerificationCode { get; set; } = string.Empty;
    }

    public class ResendCodeDto
    {
        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;
    }
}
