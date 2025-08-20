using BookingManagement.Services.DTOs;
using BookingManagement.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookingManagement.User.Razor.Pages.Register
{
    public class VerificationModel : PageModel
    {
        private readonly IAuthService _authService;
        private readonly ILogger<VerificationModel> _logger;

        public VerificationModel(IAuthService authService, ILogger<VerificationModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [BindProperty]
        public VerificationDto Input { get; set; } = new VerificationDto();

        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        public void OnGet(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                Response.Redirect("/Register");
                return;
            }

            Email = email;
            Input.Email = email;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _authService.CompleteRegistrationAsync(Input);

            if (result.Success)
            {
                _logger.LogInformation($"User {Input.Email} completed registration successfully");
                return RedirectToPage("/Login/Index", new { successMessage = "Đăng ký tài khoản thành công! Vui lòng đăng nhập." });
            }

            _logger.LogWarning($"Registration verification failed for {Input.Email}: {result.Message}");
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        public async Task<IActionResult> OnPostResendCodeAsync()
        {
            if (string.IsNullOrEmpty(Input.Email))
            {
                return new JsonResult(new { success = false, message = "Email không hợp lệ" });
            }

            var result = await _authService.ResendVerificationCodeAsync(Input.Email);

            if (result)
            {
                _logger.LogInformation($"Verification code resent to {Input.Email}");
                return new JsonResult(new { success = true, message = "Mã xác nhận đã được gửi lại" });
            }

            _logger.LogWarning($"Failed to resend verification code to {Input.Email}");
            return new JsonResult(new { success = false, message = "Không thể gửi lại mã xác nhận. Vui lòng thử lại." });
        }
    }
}
