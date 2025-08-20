using BookingManagement.Services.DTOs;
using BookingManagement.Services.Interfaces;
using BookingManagement.Services.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookingManagement.User.Razor.Pages.Login
{
    public class IndexModel : PageModel
    {
        private readonly IAuthService _authService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IAuthService authService, ILogger<IndexModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [BindProperty]
        public LoginDto Input { get; set; } = new LoginDto();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SuccessMessage { get; set; }

        public void OnGet(string returnUrl = null, string successMessage = null)
        {
            // Nếu đã đăng nhập thì chuyển hướng
            if (User.Identity?.IsAuthenticated == true)
            {
                Response.Redirect("/");
                return;
            }

            ReturnUrl = returnUrl ?? Url.Content("~/");
            SuccessMessage = successMessage;
            
            // Nếu có TempData success message, lưu vào property
            if (TempData["SuccessMessage"] != null)
            {
                SuccessMessage = TempData["SuccessMessage"].ToString();
            }
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            // Remove SuccessMessage từ ModelState validation
            ModelState.Remove("SuccessMessage");
            ModelState.Remove("ReturnUrl");

            Console.WriteLine($"=== POST LOGIN ATTEMPT ===");
            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
            Console.WriteLine($"Input.Email: {Input.Email}");
            Console.WriteLine($"Input.Password: {Input.Password}");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState errors:");
                foreach (var error in ModelState)
                {
                    Console.WriteLine($"Key: {error.Key}, Errors: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                }
                return Page();
            }

            try 
            {
                Console.WriteLine($"Login attempt for: {Input.Email}");
                
                var (success, role) = await _authService.AuthenticateAsync(Input.Email, Input.Password, HttpContext, Input.RememberMe);

                if (success)
                {
                    _logger.LogInformation($"Người dùng {Input.Email} đăng nhập thành công với vai trò {role}");
                    Console.WriteLine("Authentication successful, redirecting...");
                    return Redirect(returnUrl);
                }

                _logger.LogWarning($"Đăng nhập thất bại cho người dùng {Input.Email}");
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi trong quá trình đăng nhập: {ex.Message}");
                Console.WriteLine($"Login exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra trong quá trình đăng nhập. Vui lòng thử lại.");
                return Page();
            }
        }
    }
}
