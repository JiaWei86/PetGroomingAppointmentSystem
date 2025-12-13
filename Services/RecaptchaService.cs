//using System.Text.Json;
//using Microsoft.Extensions.Configuration;

//namespace PetGroomingAppointmentSystem.Services
//{
//    public interface IRecaptchaService
//    {
//        Task<bool> VerifyTokenAsync(string token);
//    }

//    public class RecaptchaService : IRecaptchaService
//    {
//        private readonly HttpClient _httpClient;
//        private readonly string? _secretKey;

//        public RecaptchaService(HttpClient httpClient, IConfiguration configuration)
//        {
//            _httpClient = httpClient;
//            _secretKey = configuration["RecaptchaSettings:SecretKey"];
//        }

//        public async Task<bool> VerifyTokenAsync(string token)
//        {
//            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_secretKey))
//                return false;

//            try
//            {
//                var request = new HttpRequestMessage(HttpMethod.Post, "https://www.google.com/recaptcha/api/siteverify")
//                {
//                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
//                    {
//                        { "secret", _secretKey },
//                        { "response", token }
//                    })
//                };

//                var response = await _httpClient.SendAsync(request);
//                response.EnsureSuccessStatusCode();

//                var json = await response.Content.ReadAsStringAsync();
//                using var doc = JsonDocument.Parse(json);
//                var root = doc.RootElement;

//                bool success = root.GetProperty("success").GetBoolean();
//                double score = root.GetProperty("score").GetDouble();

//                return success && score >= 0.5;
//            }
//            catch
//            {
//                return false;
//            }
//        }
//    }
//}