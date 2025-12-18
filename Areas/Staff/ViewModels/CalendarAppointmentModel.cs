using PetGroomingAppointmentSystem.Models.ViewModels;
using System.Collections.Generic;

namespace PetGroomingAppointmentSystem.Areas.Staff.ViewModels
{
    public class CalendarAppointmentModel
    {
        public string Id { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
        public string PetName { get; set; }
        public string GroomerName { get; set; }
        public string ServiceType { get; set; }
        public string Status { get; set; }
        public List<PetGroomingAppointmentSystem.Models.ViewModels.CalendarAppointmentModel> AppointmentsForCalendar { get; set; } // Add this property to match the usage in your view
    }
}
