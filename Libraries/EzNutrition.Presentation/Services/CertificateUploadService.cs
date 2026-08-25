using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Headers;

namespace EzNutrition.Presentation.Services
{
    public sealed class CertificateUploadService(IHttpClientFactory httpClientFactory)
    {
        public const long MaxFileSize = 45L * 1024 * 1024;

        private static readonly IReadOnlyDictionary<string, string> AllowedFileTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png"
            };

        public async Task UploadAsync(
            string uploadTicket,
            IBrowserFile file,
            CancellationToken cancellationToken = default)
        {
            if (!Guid.TryParse(uploadTicket, out var ticket))
            {
                throw new InvalidOperationException("服务器返回了无效的上传票据。");
            }

            var extension = Path.GetExtension(file.Name);
            if (!AllowedFileTypes.TryGetValue(extension, out var mediaType))
            {
                throw new InvalidOperationException("证明图片仅支持 JPG、JPEG 或 PNG 格式。");
            }

            if (file.Size <= 0 || file.Size > MaxFileSize)
            {
                throw new InvalidOperationException("证明图片不能为空，且大小不能超过 45 MB。");
            }

            using var content = new MultipartFormDataContent();
            await using var fileStream = file.OpenReadStream(MaxFileSize, cancellationToken);
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            content.Add(fileContent, "certificateFile", file.Name);

            var httpClient = httpClientFactory.CreateClient("Anonymous");
            using var response = await httpClient.PostAsync(
                $"Auth/UploadCertificate/{ticket:D}",
                content,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    "证明图片上传失败。",
                    null,
                    response.StatusCode);
            }
        }
    }
}
