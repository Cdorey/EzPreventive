namespace EzNutrition.Shared.Data.DTO
{
    public class RegistrationResultDto
    {
        public bool Success { get; set; }

        public required string Message { get; set; }

        /// <summary>
        /// 上传票据，用于后续上传证件照片
        /// </summary>
        public string? UploadTicket { get; set; }
    }
}