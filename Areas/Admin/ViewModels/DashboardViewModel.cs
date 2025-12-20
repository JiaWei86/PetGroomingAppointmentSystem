using System.Collections.Generic;
using PetGroomingAppointmentSystem.Models;

namespace PetGroomingAppointmentSystem.Models.ViewModels
{
    /// <summary>
    /// ViewModel to hold all data required for the Admin Dashboard.
    /// </summary>
    public class DashboardViewModel
    {
        public StatCardModel TotalAppointments { get; set; }
        public StatCardModel ActiveGroomers { get; set; }
        public StatCardModel PendingAppointments { get; set; }
        public List<RedeemGift> OutOfStockGifts { get; set; }
        public LoyaltyPointsModel LoyaltyPoints { get; set; }

        /// <summary>
        /// Data for the main trend chart.
        /// </summary>
        public ChartDataModel ChartData { get; set; }

        /// <summary>
        /// List of appointments for the calendar view.
        /// </summary>
        public List<CalendarAppointmentModel> AppointmentsForCalendar { get; set; }

        public DashboardViewModel()
        {
            // Initialize with default values to prevent null reference exceptions in the view
            TotalAppointments = new StatCardModel();
            ActiveGroomers = new StatCardModel();
            PendingAppointments = new StatCardModel();
            OutOfStockGifts = new List<RedeemGift>();
            LoyaltyPoints = new LoyaltyPointsModel();
            ChartData = new ChartDataModel();
            AppointmentsForCalendar = new List<CalendarAppointmentModel>();
        }
    }

    public class StatCardModel
    {
        public int Count { get; set; }
        public decimal ChangePercentage { get; set; }
    }

    public class LoyaltyPointsModel
    {
        public int AwardedThisWeek { get; set; }
        public int ActiveMembers { get; set; }
        public int RedeemedThisWeek { get; set; }
    }

    public class ChartDataModel
    {
        public ChartSeriesModel Week { get; set; }
        public ChartSeriesModel Month { get; set; }
        public ChartSeriesModel Day { get; set; }
    }

    public class ChartSeriesModel
    {
        public List<string> Labels { get; set; }
        public List<int> Data { get; set; }
    }

    public class CalendarAppointmentModel
    {
        public string Id { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
        public string PetName { get; set; }
        public string GroomerName { get; set; }
        public string ServiceType { get; set; }
        public string Status { get; set; }
    }
}