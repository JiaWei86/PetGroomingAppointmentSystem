namespace PetGroomingAppointmentSystem.Services
{
    public interface IChatbotService
    {
        Task<string> GetResponseAsync(string userMessage);
    }
}
