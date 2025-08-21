using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookingManagement.Services.Interfaces;
using BookingManagement.Services.DTOs;
using System.Security.Claims;

namespace BookingManagement.User.Razor.Pages.User
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly IUserService _userService;

        public ProfileModel(IUserService userService)
        {
            _userService = userService;
        }

        [BindProperty]
        public UserDto? UserProfile { get; set; }

        [BindProperty]
        public string MSSV { get; set; } = string.Empty;

        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrEmpty(userEmail))
                {
                    return RedirectToPage("/Login/Index");
                }

                UserProfile = await _userService.GetUserByEmailAsync(userEmail);
                if (UserProfile == null)
                {
                    return RedirectToPage("/Login/Index");
                }

                // Extract MSSV from email (part before @fpt.edu.vn)
                if (UserProfile.Email.EndsWith("@fpt.edu.vn"))
                {
                    MSSV = UserProfile.Email.Replace("@fpt.edu.vn", "");
                }

                return Page();
            }
            catch (Exception)
            {
                return RedirectToPage("/Error");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(ConfirmPassword))
                {
                    TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin.";
                    return RedirectToPage();
                }

                if (NewPassword.Length < 6)
                {
                    TempData["ErrorMessage"] = "Mật khẩu phải có ít nhất 6 ký tự.";
                    return RedirectToPage();
                }

                if (NewPassword != ConfirmPassword)
                {
                    TempData["ErrorMessage"] = "Mật khẩu xác nhận không khớp.";
                    return RedirectToPage();
                }

                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrEmpty(userEmail))
                {
                    return RedirectToPage("/Login/Index");
                }

                var user = await _userService.GetUserByEmailAsync(userEmail);
                if (user == null)
                {
                    return RedirectToPage("/Login/Index");
                }

                var changePasswordDto = new ChangePasswordDto
                {
                    UserId = user.UserId,
                    NewPassword = NewPassword
                };

                var result = await _userService.ChangePasswordAsync(changePasswordDto);
                if (result)
                {
                    TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi đổi mật khẩu. Vui lòng thử lại.";
                }

                return RedirectToPage();
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra. Vui lòng thử lại.";
                return RedirectToPage();
            }
        }
    }
}
