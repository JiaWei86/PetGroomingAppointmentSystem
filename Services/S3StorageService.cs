using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;
using PetGroomingAppointmentSystem.Models;

namespace PetGroomingAppointmentSystem.Services
{
    public class S3StorageService : IS3StorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly AWSSettings _awsSettings;
        private readonly ILogger<S3StorageService> _logger;

        public S3StorageService(IOptions<AWSSettings> awsSettings, ILogger<S3StorageService> logger)
        {
            _awsSettings = awsSettings.Value;
            _logger = logger;

            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_awsSettings.Region)
            };

            _s3Client = new AmazonS3Client(
                _awsSettings.AccessKey,
                _awsSettings.SecretKey,
                config
            );
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder = "")
        {
            try
            {
                // Generate unique file name
                var extension = Path.GetExtension(fileName);
                var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
                
                // Build S3 key (path)
                var s3Key = string.IsNullOrEmpty(folder) 
                    ? uniqueFileName 
                    : $"{folder.TrimEnd('/')}/{uniqueFileName}";

                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = fileStream,
                    Key = s3Key,
                    BucketName = _awsSettings.BucketName,
                    ContentType = contentType,
                    CannedACL = S3CannedACL.Private // CloudFront will serve files
                };

                var transferUtility = new TransferUtility(_s3Client);
                await transferUtility.UploadAsync(uploadRequest);

                _logger.LogInformation("File uploaded to S3: {S3Key}", s3Key);

                // Return CloudFront URL
                return GetCloudFrontUrl(s3Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to S3: {FileName}", fileName);
                throw;
            }
        }

        public async Task<string> UploadBase64ImageAsync(string base64Data, string folder = "")
        {
            try
            {
                // Remove data URI prefix if present
                var base64String = base64Data;
                var contentType = "image/jpeg";

                if (base64Data.Contains(","))
                {
                    var parts = base64Data.Split(',');
                    base64String = parts[1];
                    
                    // Extract content type from data URI
                    if (parts[0].Contains("png"))
                        contentType = "image/png";
                    else if (parts[0].Contains("gif"))
                        contentType = "image/gif";
                    else if (parts[0].Contains("webp"))
                        contentType = "image/webp";
                }

                var imageBytes = Convert.FromBase64String(base64String);
                var extension = contentType.Split('/')[1];
                var fileName = $"image.{extension}";

                using var stream = new MemoryStream(imageBytes);
                return await UploadFileAsync(stream, fileName, contentType, folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading base64 image to S3");
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string fileUrl)
        {
            try
            {
                // Extract S3 key from CloudFront URL
                var s3Key = ExtractS3KeyFromUrl(fileUrl);
                
                if (string.IsNullOrEmpty(s3Key))
                {
                    _logger.LogWarning("Could not extract S3 key from URL: {Url}", fileUrl);
                    return false;
                }

                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _awsSettings.BucketName,
                    Key = s3Key
                };

                await _s3Client.DeleteObjectAsync(deleteRequest);
                _logger.LogInformation("File deleted from S3: {S3Key}", s3Key);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file from S3: {Url}", fileUrl);
                return false;
            }
        }

        public string GetCloudFrontUrl(string s3Key)
        {
            var domain = _awsSettings.CloudFrontDomain.TrimEnd('/');
            return $"{domain}/{s3Key}";
        }

        private string ExtractS3KeyFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            // Handle CloudFront URLs
            if (url.Contains(_awsSettings.CloudFrontDomain))
            {
                var uri = new Uri(url);
                return uri.AbsolutePath.TrimStart('/');
            }

            // Handle direct S3 URLs
            if (url.Contains(".s3.") || url.Contains("s3://"))
            {
                var uri = new Uri(url);
                return uri.AbsolutePath.TrimStart('/');
            }

            return string.Empty;
        }
    }
}