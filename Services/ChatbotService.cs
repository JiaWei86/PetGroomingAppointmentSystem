using System.Net.Http.Json;
using System.Text.Json;
using PetGroomingAppointmentSystem.Models;

namespace PetGroomingAppointmentSystem.Services;

public class ChatbotService : IChatbotService
{
    private readonly ILogger<ChatbotService> _logger;
    private readonly List<FaqItem> _faqDatabase;

    public ChatbotService(IConfiguration configuration, ILogger<ChatbotService> logger)
    {
        _logger = logger;
        _faqDatabase = InitializeFaqDatabase();
    }

    public Task<string> GetResponseAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return Task.FromResult("Please type a message.");

        try
        {
            var faqResponse = SearchFaqDatabase(userMessage);
            return Task.FromResult(
                !string.IsNullOrEmpty(faqResponse)
                    ? faqResponse
                    : "I'm currently in FAQ-only mode. Please ask about our services, pricing, or pet care!"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError($"ChatbotService exception: {ex.Message}");
            return Task.FromResult("I’m having trouble processing your request. Please try again later.");
        }
    }
    private string SearchFaqDatabase(string userMessage)
    {
        var lowerMessage = userMessage.ToLower();
        var matched = _faqDatabase.FirstOrDefault(f => f.Keywords.Any(k => lowerMessage.Contains(k.ToLower())));
        return matched?.Answer ?? string.Empty;
    }

    private List<FaqItem> InitializeFaqDatabase()
    {
        return new List<FaqItem>
            {
                new FaqItem
                {
                    Answer = "You can book an appointment directly through our website by clicking the 'Book Now' button or visiting our Services Section. You can also call us or visit in person.",
                    Keywords = new[] { "book", "booking", "reserve", "make appointment", "set appointment" }
                },

                new FaqItem
                {
                    Answer = "You can cancel your appointment up to 24 hours before your scheduled time through your account or by contacting us. Cancellations made less than 24 hours before may incur a cancellation fee.",
                    Keywords = new[] { "cancel", "cancellation", "reschedule", "change appointment", "modify appointment" }
                },

                new FaqItem
                {
                    Answer = "We offer a variety of services. Both dogs and cats are welcome! Visit our services and pricing list for detailed information.",
                    Keywords = new[] { "services", "grooming services", "what services", "offer", "available" }
                },

                new FaqItem
                {
                    Answer = "Pricing varies based on the type of pet, size, and services selected. Please visit our Services & Price List section or contact us for exact pricing.",
                    Keywords = new[] { "price", "cost", "how much", "pricing", "rate" }
                },

                new FaqItem
                {
                    Answer = "Yes, professional bathing is safe for most pets when done by trained groomers. We use pet-safe, hypoallergenic products. However, if your pet has skin conditions or health concerns, please inform us beforehand so we can take special precautions.",
                    Keywords = new[] { "bathing", "bath", "wash", "safe", "allergies" }
                },

                new FaqItem
                {
                    Answer = "Grooming frequency depends on your pet's breed and coat type. Generally: Short-haired pets every 6-8 weeks, long-haired pets every 4-6 weeks, and cats every 8-12 weeks. Our groomers can recommend a schedule based on your pet's specific needs.",
                    Keywords = new[] { "how often", "frequency", "grooming schedule", "groom frequency" }
                },

                new FaqItem
                {
                    Answer = "We specialize in handling anxious and nervous pets with patience and gentle care techniques. For extremely aggressive pets, please contact us beforehand so we can discuss special accommodations or recommend alternatives.",
                    Keywords = new[] { "anxious", "aggressive", "nervous", "scared", "anxiety" }
                },

                new FaqItem
                {
                    Answer = "Absolutely! We exclusively use premium, pet-safe, hypoallergenic grooming products that are gentle on your pet's skin and coat. All our products are tested for safety and suitability for pets.",
                    Keywords = new[] { "products", "safe products", "ingredients", "chemical", "allergy" }
                },

                new FaqItem
                {
                    Answer = "We're open Tuesday to Friday from 9:00 AM to 5:00 PM. You can book appointments online anytime, or call us during business hours for immediate assistance.",
                    Keywords = new[] { "hours", "open", "closed", "time", "when" }
                },

                new FaqItem
                {
                    Answer = "You can reach us by: Whatsapp (during business hours) and Email (response within 24 hours). Our friendly team is here to help with any questions or concerns!",
                    Keywords = new[] { "contact", "support", "help", "phone", "email" }
                }
            };

    }
}