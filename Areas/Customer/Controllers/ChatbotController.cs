using PetGroomingAppointmentSystem.Services;
using PetGroomingAppointmentSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace PetGroomingAppointmentSystem.Areas.Customer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;
        private readonly ILogger<ChatbotController> _logger;

        public ChatbotController(IChatbotService chatbotService, ILogger<ChatbotController> logger)
        {
            _chatbotService = chatbotService;
            _logger = logger;
        }

        [HttpPost("message")]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }

            try
            {
                var response = await _chatbotService.GetResponseAsync(request.Message);
                return Ok(new { response });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing chatbot message: {ex.Message}");
                return StatusCode(500, new { error = "An error occurred processing your request" });
            }
        }

        [HttpGet("faq-topics")]
        public async Task<IActionResult> GetFaqTopics()
        {
            try
            {
                var topics = await _chatbotService.GetFaqTopicsAsync();
                return Ok(new { topics });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching FAQ topics: {ex.Message}");
                return StatusCode(500, new { error = "An error occurred fetching FAQ topics" });
            }
        }
    }
}