using BookingManagement.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BookingManagement.Services.Services
{
    public class VerificationService : IVerificationService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<VerificationService> _logger;
        private const int CODE_EXPIRY_MINUTES = 10;

        public VerificationService(IMemoryCache cache, ILogger<VerificationService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public string GenerateVerificationCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString(); // 6 chữ số
        }

        public async Task<bool> StoreVerificationCodeAsync(string email, string code)
        {
            try
            {
                var cacheKey = $"verification_code_{email}";
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CODE_EXPIRY_MINUTES)
                };

                _cache.Set(cacheKey, code, cacheOptions);
                _logger.LogInformation($"Verification code stored for email: {email}");
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to store verification code for email: {email}");
                return false;
            }
        }

        public async Task<bool> ValidateVerificationCodeAsync(string email, string code)
        {
            try
            {
                var cacheKey = $"verification_code_{email}";
                
                if (_cache.TryGetValue(cacheKey, out string storedCode))
                {
                    var isValid = storedCode == code;
                    if (isValid)
                    {
                        // Remove code after successful validation
                        _cache.Remove(cacheKey);
                        _logger.LogInformation($"Verification code validated successfully for email: {email}");
                    }
                    else
                    {
                        _logger.LogWarning($"Invalid verification code provided for email: {email}");
                    }
                    return await Task.FromResult(isValid);
                }

                _logger.LogWarning($"No verification code found for email: {email}");
                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to validate verification code for email: {email}");
                return false;
            }
        }

        public async Task<bool> IsVerificationCodeValidAsync(string email, string code)
        {
            try
            {
                var cacheKey = $"verification_code_{email}";
                
                if (_cache.TryGetValue(cacheKey, out string storedCode))
                {
                    return await Task.FromResult(storedCode == code);
                }

                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to check verification code for email: {email}");
                return false;
            }
        }

        public async Task RemoveVerificationCodeAsync(string email)
        {
            try
            {
                var cacheKey = $"verification_code_{email}";
                _cache.Remove(cacheKey);
                _logger.LogInformation($"Verification code removed for email: {email}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to remove verification code for email: {email}");
            }
        }
    }
}
