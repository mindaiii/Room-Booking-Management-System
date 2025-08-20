using BookingManagement.Repositories.Interfaces;
using BookingManagement.Repositories.Models;
using BookingManagement.Repositories.UnitOfWork;
using BookingManagement.Services.DTOs;
using BookingManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BookingManagement.Services.Services
{

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IVerificationService _verificationService;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

        public AuthService(
            IUserRepository userRepository, 
            IRoleRepository roleRepository, 
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            IVerificationService verificationService,
            Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _verificationService = verificationService;
            _cache = cache;
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            // Nếu password trong database chưa hash (plain text), hash nó
            if (hashedPassword == "123456" || hashedPassword.Length < 20)
            {
                return hashedPassword == password;
            }
            
            // Nếu đã hash, so sánh hash
            string hashOfInput = HashPassword(password);
            return hashOfInput == hashedPassword;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            var users = await _userRepository.FindAsync(u => u.Email == email && u.IsActive == true);
            return users.FirstOrDefault();
        }

        public async Task<Role?> GetRoleByIdAsync(int roleId)
        {
            return await _roleRepository.GetByIdAsync(roleId);
        }

        public async Task<(bool Success, string Role)> AuthenticateAsync(string email, string password, HttpContext httpContext, bool rememberMe = false)
        {
            try
            {
                Console.WriteLine($"=== LOGIN ATTEMPT ===");
                Console.WriteLine($"Email: {email}");
                Console.WriteLine($"Password: {password}");
                
                // Đăng xuất user hiện tại trước khi đăng nhập (để tránh conflict)
                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    Console.WriteLine("Signing out current user before new login...");
                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
                
                // Tìm user theo email
                var user = await GetUserByEmailAsync(email);
                Console.WriteLine($"User found: {user != null}");
                
                if (user == null)
                {
                    Console.WriteLine("LOGIN FAILED: User not found");
                    return (false, string.Empty);
                }

                Console.WriteLine($"User ID: {user.UserId}");
                Console.WriteLine($"User Email: {user.Email}");
                Console.WriteLine($"User Password in DB: {user.Password}");
                Console.WriteLine($"User RoleId: {user.RoleId}");
                Console.WriteLine($"User IsActive: {user.IsActive}");
                
                // Kiểm tra user có active không
                if (user.IsActive != true)
                {
                    Console.WriteLine("LOGIN FAILED: User is not active");
                    return (false, string.Empty);
                }
                
                // So sánh mật khẩu sử dụng VerifyPassword method
                if (!VerifyPassword(password, user.Password))
                {
                    Console.WriteLine("LOGIN FAILED: Wrong password");
                    return (false, string.Empty);
                }
                
                // Lấy thông tin role
                var role = await GetRoleByIdAsync(user.RoleId);
                Console.WriteLine($"Role found: {role?.RoleName ?? "null"}");
                
                if (role == null)
                {
                    Console.WriteLine("LOGIN FAILED: Role not found");
                    return (false, string.Empty);
                }

                // Tạo claims cho user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Role, role.RoleName)
                };

                Console.WriteLine($"Creating claims...");
                
                // Tạo ClaimsIdentity
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                Console.WriteLine($"Signing in user...");
                
                // Sign in với properties mới
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(2),
                    AllowRefresh = true,
                    IssuedUtc = DateTime.UtcNow
                };
                
                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    authProperties);

                Console.WriteLine($"LOGIN SUCCESS! Role: {role.RoleName}");
                return (true, role.RoleName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LOGIN ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return (false, string.Empty);
            }
        }

        public async Task<RegisterResultDto> InitiateRegistrationAsync(RegisterDto registerDto)
        {
            // Kiểm tra email đã tồn tại
            var existingUser = await GetUserByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                return new RegisterResultDto
                {
                    Success = false,
                    Message = "Email đã tồn tại trong hệ thống"
                };
            }

            // Tạo mã xác nhận
            var verificationCode = _verificationService.GenerateVerificationCode();
            
            // Lưu thông tin đăng ký tạm thời vào cache
            var registrationData = new
            {
                Email = registerDto.Email,
                Password = registerDto.Password,
                FullName = registerDto.FullName,
                VerificationCode = verificationCode
            };

            var cacheKey = $"registration_data_{registerDto.Email}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
            _cache.Set(cacheKey, registrationData, cacheOptions);

            // Lưu mã xác nhận
            await _verificationService.StoreVerificationCodeAsync(registerDto.Email, verificationCode);

            // Gửi email xác nhận
            var emailSent = await _emailService.SendVerificationEmailAsync(registerDto.Email, verificationCode);
            
            if (!emailSent)
            {
                return new RegisterResultDto
                {
                    Success = false,
                    Message = "Không thể gửi email xác nhận. Vui lòng thử lại."
                };
            }

            return new RegisterResultDto
            {
                Success = true,
                Message = "Mã xác nhận đã được gửi đến email của bạn"
            };
        }

        public async Task<RegisterResultDto> CompleteRegistrationAsync(VerificationDto verificationDto)
        {
            // Kiểm tra mã xác nhận
            var isValidCode = await _verificationService.ValidateVerificationCodeAsync(
                verificationDto.Email, verificationDto.VerificationCode);

            if (!isValidCode)
            {
                return new RegisterResultDto
                {
                    Success = false,
                    Message = "Mã xác nhận không đúng hoặc đã hết hạn"
                };
            }

            // Lấy thông tin đăng ký từ cache
            var cacheKey = $"registration_data_{verificationDto.Email}";
            if (!_cache.TryGetValue(cacheKey, out dynamic registrationData))
            {
                return new RegisterResultDto
                {
                    Success = false,
                    Message = "Thông tin đăng ký đã hết hạn. Vui lòng đăng ký lại."
                };
            }

            // Tạo user mới
            var userRoles = await _roleRepository.FindAsync(r => r.RoleName == "User");
            int roleId = userRoles.FirstOrDefault()?.RoleId ?? 2;

            var newUser = new User
            {
                Email = registrationData.Email,
                Password = registrationData.Password,
                FullName = registrationData.FullName,
                RoleId = roleId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _userRepository.AddAsync(newUser);
            await _unitOfWork.CompleteAsync();

            // Xóa cache sau khi đăng ký thành công
            _cache.Remove(cacheKey);

            return new RegisterResultDto
            {
                Success = true,
                Message = "Đăng ký tài khoản thành công",
                UserId = newUser.UserId
            };
        }

        public async Task<bool> ResendVerificationCodeAsync(string email)
        {
            // Kiểm tra cache xem có thông tin đăng ký không
            var cacheKey = $"registration_data_{email}";
            if (!_cache.TryGetValue(cacheKey, out dynamic registrationData))
            {
                return false;
            }

            // Tạo mã xác nhận mới
            var newVerificationCode = _verificationService.GenerateVerificationCode();
            
            // Cập nhật mã trong cache
            var updatedData = new
            {
                Email = registrationData.Email,
                Password = registrationData.Password,
                FullName = registrationData.FullName,
                VerificationCode = newVerificationCode
            };
            _cache.Set(cacheKey, updatedData, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            // Lưu mã xác nhận mới
            await _verificationService.StoreVerificationCodeAsync(email, newVerificationCode);

            // Gửi email mới
            return await _emailService.SendVerificationEmailAsync(email, newVerificationCode);
        }

        public async Task<RegisterResultDto> RegisterUserAsync(RegisterDto registerDto)
        {
            // Kiểm tra email đã tồn tại
            var existingUser = await GetUserByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                return new RegisterResultDto
                {
                    Success = false,
                    Message = "Email đã tồn tại trong hệ thống"
                };
            }

            // Lấy default Role cho User (RoleId = 2 cho User)
            var userRoles = await _roleRepository.FindAsync(r => r.RoleName == "User");
            int roleId = userRoles.FirstOrDefault()?.RoleId ?? 2; // Mặc định là 2 nếu không tìm thấy

            // Tạo user mới
            var newUser = new User
            {
                Email = registerDto.Email,
                Password = registerDto.Password, // Trong thực tế, mật khẩu nên được hash
                FullName = registerDto.FullName,
                RoleId = roleId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _userRepository.AddAsync(newUser);
            await _unitOfWork.CompleteAsync();

            return new RegisterResultDto
            {
                Success = true,
                Message = "Đăng ký tài khoản thành công",
                UserId = newUser.UserId
            };
        }

        public async Task SignOutAsync(HttpContext httpContext)
        {
            try
            {
                Console.WriteLine("=== SIGNING OUT USER ===");
                
                // Clear all authentication cookies
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                // Clear any additional cookies if they exist
                foreach (var cookie in httpContext.Request.Cookies.Keys)
                {
                    if (cookie.StartsWith(".AspNetCore") || cookie.Contains("Auth"))
                    {
                        httpContext.Response.Cookies.Delete(cookie);
                        Console.WriteLine($"Deleted cookie: {cookie}");
                    }
                }
                
                Console.WriteLine("SIGN OUT SUCCESSFUL");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SIGN OUT ERROR: {ex.Message}");
                throw;
            }
        }
    }
}
