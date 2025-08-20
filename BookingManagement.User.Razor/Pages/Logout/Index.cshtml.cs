using BookingManagement.Services.Interfaces;
using BookingManagement.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace BookingManagement.User.Razor.Pages.Logout
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IAuthService _authService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IAuthService authService, ILogger<IndexModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userName = User.FindFirstValue(ClaimTypes.Name);
            await _authService.SignOutAsync(HttpContext);
            _logger.LogInformation($"Người dùng {userName} đã đăng xuất");
            
            // Redirect về login với thông báo thành công
            return RedirectToPage("/Login/Index", new { successMessage = "Đăng xuất thành công!" });
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userName = User.FindFirstValue(ClaimTypes.Name);
            await _authService.SignOutAsync(HttpContext);
            _logger.LogInformation($"Người dùng {userName} đã đăng xuất");
            
            // Redirect về login với thông báo thành công
            return RedirectToPage("/Login/Index", new { successMessage = "Đăng xuất thành công!" });
        }
    }
}
