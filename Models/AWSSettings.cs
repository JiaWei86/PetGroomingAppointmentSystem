namespace PetGroomingAppointmentSystem.Models
{
    public class AWSSettings
    {
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string CloudFrontDomain { get; set; } = string.Empty;
    }
}