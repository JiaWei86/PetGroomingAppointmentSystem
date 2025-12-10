using PetGroomingAppointmentSystem.Models;

namespace PetGroomingAppointmentSystem.Areas.Customer.ViewModels
{
    public class HomeViewModel
    {
        public List<Service> DogServices { get; set; }
        public List<Service> CatServices { get; set; }
        public List<RedeemGift> RedeemGifts { get; set; }
        public int CustomerLoyaltyPoints { get; set; } = 0;
    }
}
