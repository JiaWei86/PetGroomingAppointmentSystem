using Microsoft.AspNetCore.SignalR;
using PetGroomingAppointmentSystem.Services;

namespace PetGroomingAppointmentSystem.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IChatbotService _chatbotService;

        public ChatHub(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        // Called by browser
        public async Task SendMessage(string message)
        {
            // Send user message back immediately
            await Clients.Caller.SendAsync("ReceiveMessage", "User", message);

            // Get FAQ bot reply
            var reply = await _chatbotService.GetResponseAsync(message);

            // Send bot reply
            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
        }
    }
}
