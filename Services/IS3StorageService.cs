namespace PetGroomingAppointmentSystem.Services
{
    public interface IS3StorageService
    {
        /// <summary>
        /// Upload a file to S3 and return the CloudFront URL
        /// </summary>
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder = "");

        /// <summary>
        /// Upload a base64 image to S3 and return the CloudFront URL
        /// </summary>
        Task<string> UploadBase64ImageAsync(string base64Data, string folder = "");

        /// <summary>
        /// Delete a file from S3
        /// </summary>
        Task<bool> DeleteFileAsync(string fileUrl);

        /// <summary>
        /// Get the CloudFront URL for a given S3 key
        /// </summary>
        string GetCloudFrontUrl(string s3Key);
    }
}