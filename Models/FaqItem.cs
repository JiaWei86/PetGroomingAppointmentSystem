namespace PetGroomingAppointmentSystem.Models;

public class FaqItem
{
    public string Topic { get; set; }
    public string Question { get; set; }
    public string Answer { get; set; }
    public string[] Keywords { get; set; }
}