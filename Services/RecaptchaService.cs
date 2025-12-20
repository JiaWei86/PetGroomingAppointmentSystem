using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace PetGroomingAppointmentSystem.Services
{
    public interface IRecaptchaService
    {
        Task<bool> VerifyTokenAsync(string token);
    }

    public class RecaptchaService : IRecaptchaService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _secretKey;

        public RecaptchaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _secretKey = configuration["RecaptchaSettings:SecretKey"];
        }

        public async Task<bool> VerifyTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_secretKey))
            {
                Console.WriteLine("[reCAPTCHA] Token or SecretKey is empty");
                return false;
            }

            try
            {
                Console.WriteLine("[reCAPTCHA] Verifying token with Google...");

                var request = new HttpRequestMessage(HttpMethod.Post, "https://www.google.com/recaptcha/api/siteverify")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "secret", _secretKey },
                        { "response", token }
                    })
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool success = root.GetProperty("success").GetBoolean();
                Console.WriteLine($"[reCAPTCHA] Verification result: {(success ? "✓ VALID" : "❌ INVALID")}");

                if (success && root.TryGetProperty("score", out var scoreElement))
                {
                    // v3 返回 score
                    double score = scoreElement.GetDouble();
                    Console.WriteLine($"[reCAPTCHA] Score: {score}");
                    return success && score >= 0.5;
                }

                // v2 没有 score，直接返回 success
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[reCAPTCHA] Verification error: {ex.Message}");
                return false;
            }
        }
    }
}