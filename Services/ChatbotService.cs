using System.Net.Http.Json;
using System.Text.Json;
using PetGroomingAppointmentSystem.Models;

namespace PetGroomingAppointmentSystem.Services;

public class ChatbotService : IChatbotService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatbotService> _logger;
    private readonly List<FaqItem> _faqDatabase;

    public ChatbotService(HttpClient httpClient, IConfiguration configuration, ILogger<ChatbotService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _faqDatabase = InitializeFaqDatabase();
    }

    public async Task<string> GetResponseAsync(string userMessage)
    {
        try
        {
            // First, try to match with FAQ database
            var faqResponse = SearchFaqDatabase(userMessage);
            if (!string.IsNullOrEmpty(faqResponse))
            {
                return faqResponse;
            }

            // If no FAQ match, use AI (OpenAI or Azure OpenAI)
            var aiResponse = await GetAiResponseAsync(userMessage);
            return aiResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in chatbot service: {ex.Message}");
            return "I apologize, but I'm having trouble processing your request. Please try again or contact our support team.";
        }
    }

    public async Task<List<string>> GetFaqTopicsAsync()
    {
        return _faqDatabase.Select(f => f.Topic).Distinct().ToList();
    }

    private string SearchFaqDatabase(string userMessage)
    {
        var lowerMessage = userMessage.ToLower();

        // Search for keyword matches
        var matchedFaq = _faqDatabase.FirstOrDefault(f =>
            f.Keywords.Any(k => lowerMessage.Contains(k.ToLower())));

        return matchedFaq?.Answer ?? string.Empty;
    }

    private async Task<string> GetAiResponseAsync(string userMessage)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        var useAzureOpenAi = _configuration.GetValue<bool>("OpenAI:UseAzure");

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenAI API key is not configured");
            return "I'm currently in FAQ-only mode. Please ask about our services, pricing, or pet care!";
        }

        if (useAzureOpenAi)
        {
            return await GetAzureOpenAiResponseAsync(userMessage, apiKey);
        }
        else
        {
            return await GetOpenAiResponseAsync(userMessage, apiKey);
        }
    }

    private async Task<string> GetOpenAiResponseAsync(string userMessage, string apiKey)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var request = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = userMessage }
            },
            max_tokens = 500,
            temperature = 0.7
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                "https://api.openai.com/v1/chat/completions",
                request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                var content = result.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();
                
                return content ?? "Unable to process your request.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"OpenAI API error: {response.StatusCode} - {errorContent}");
                return "I apologize, but I'm unable to process your request at the moment.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"OpenAI API exception: {ex.Message}");
            return "I apologize, but I'm unable to process your request at the moment.";
        }
    }

    private async Task<string> GetAzureOpenAiResponseAsync(string userMessage, string apiKey)
    {
        var endpoint = _configuration["OpenAI:AzureEndpoint"];
        var deploymentId = _configuration["OpenAI:DeploymentId"];

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deploymentId))
        {
            _logger.LogWarning("Azure OpenAI configuration is incomplete");
            return "Azure OpenAI is not properly configured.";
        }

        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("api-key", apiKey);

        var request = new
        {
            messages = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = userMessage }
            },
            max_tokens = 500,
            temperature = 0.7
        };

        try
        {
            var url = $"{endpoint}/openai/deployments/{deploymentId}/chat/completions?api-version=2024-02-15-preview";
            var response = await client.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                var content = result.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();
                
                return content ?? "Unable to process your request.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Azure OpenAI API error: {response.StatusCode} - {errorContent}");
                return "I apologize, but I'm unable to process your request at the moment.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Azure OpenAI API exception: {ex.Message}");
            return "I apologize, but I'm unable to process your request at the moment.";
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are a helpful AI assistant for Hajimi House, a pet grooming appointment system. 
You help customers with:
- Information about our grooming services (bathing, haircuts, nail clipping)
- How to book appointments
- Pricing and package information
- Pet care advice and grooming tips
- General questions about our business

Always be friendly, professional, and encouraging. If you don't have information about a specific topic, 
suggest the customer contact our support team or visit our website. Keep responses concise and helpful.";
    }

    private List<FaqItem> InitializeFaqDatabase()
    {
        return new List<FaqItem>
        {
            new FaqItem
            {
                Topic = "Booking & Scheduling",
                Question = "How do I book an appointment?",
                Answer = "You can book an appointment directly through our website by clicking the 'Book Now' button or visiting our Appointment page. Select your pet type, preferred date and time, and grooming service. You can also call us or visit in person.",
                Keywords = new[] { "book", "appointment", "schedule", "booking", "reserve" }
            },
            new FaqItem
            {
                Topic = "Booking & Scheduling",
                Question = "Can I reschedule or cancel my appointment?",
                Answer = "Yes! You can reschedule or cancel your appointment up to 24 hours before your scheduled time through your account or by contacting us. Cancellations made less than 24 hours before may incur a cancellation fee.",
                Keywords = new[] { "cancel", "reschedule", "change", "modify", "appointment" }
            },
            new FaqItem
            {
                Topic = "Services & Pricing",
                Question = "What grooming services do you offer?",
                Answer = "We offer a variety of services including: Bathing, Haircut/Trimming, Nail Clipping/Grinding, Ear Cleaning, and Full Grooming Packages. Both dogs and cats are welcome! Visit our pricing page for detailed rates.",
                Keywords = new[] { "service", "grooming", "what", "offer", "available" }
            },
            new FaqItem
            {
                Topic = "Services & Pricing",
                Question = "How much does grooming cost?",
                Answer = "Pricing varies based on the type of pet, size, and services selected. Basic services start from $30, while full grooming packages range from $80-$150. Please visit our Services & Price List page or contact us for exact pricing.",
                Keywords = new[] { "price", "cost", "how much", "expensive", "rate" }
            },
            new FaqItem
            {
                Topic = "Pet Care",
                Question = "Is bathing safe for all pets?",
                Answer = "Yes, professional bathing is safe for most pets when done by trained groomers. We use pet-safe, hypoallergenic products. However, if your pet has skin conditions or health concerns, please inform us beforehand so we can take special precautions.",
                Keywords = new[] { "bathing", "safe", "bath", "wash", "allergies" }
            },
            new FaqItem
            {
                Topic = "Pet Care",
                Question = "How often should I groom my pet?",
                Answer = "Grooming frequency depends on your pet's breed and coat type. Generally: Short-haired pets every 6-8 weeks, long-haired pets every 4-6 weeks, and cats every 8-12 weeks. Our groomers can recommend a schedule based on your pet's specific needs.",
                Keywords = new[] { "how often", "frequency", "groom", "grooming", "schedule" }
            },
            new FaqItem
            {
                Topic = "Pet Care",
                Question = "What is your policy on anxious or aggressive pets?",
                Answer = "We specialize in handling anxious and nervous pets with patience and gentle care techniques. For extremely aggressive pets, please contact us beforehand so we can discuss special accommodations or recommend alternatives.",
                Keywords = new[] { "anxious", "aggressive", "nervous", "scared", "anxiety" }
            },
            new FaqItem
            {
                Topic = "General",
                Question = "Do you use safe grooming products?",
                Answer = "Absolutely! We exclusively use premium, pet-safe, hypoallergenic grooming products that are gentle on your pet's skin and coat. All our products are tested for safety and suitability for pets.",
                Keywords = new[] { "products", "safe", "chemical", "ingredient", "allergy" }
            },
            new FaqItem
            {
                Topic = "General",
                Question = "What are your business hours?",
                Answer = "We're open Monday to Saturday from 9:00 AM to 6:00 PM, and Sunday from 10:00 AM to 4:00 PM. You can book appointments online anytime, or call us during business hours for immediate assistance.",
                Keywords = new[] { "hours", "open", "closed", "time", "when" }
            },
            new FaqItem
            {
                Topic = "General",
                Question = "How can I contact customer support?",
                Answer = "You can reach us by: Phone (during business hours), Email (response within 24 hours), or through our Contact page on the website. Our friendly team is here to help with any questions or concerns!",
                Keywords = new[] { "contact", "support", "help", "phone", "email" }
            }
        };
    }
}