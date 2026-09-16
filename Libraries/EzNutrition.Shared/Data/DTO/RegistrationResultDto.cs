namespace EzNutrition.Shared.Data.DTO
{
    public class RegistrationResultDto
    {
        public bool Success { get; set; }

        public required string Message { get; set; }

        /// <summary>
        /// 可安全暴露给客户端的注册失败类型。客户端只应针对已知类型显示具体提示。
        /// </summary>
        public RegistrationFailureCode? FailureCode { get; set; }

        /// <summary>
        /// 上传票据，用于后续上传证件照片
        /// </summary>
        public string? UploadTicket { get; set; }
    }

    public enum RegistrationFailureCode
    {
        DuplicateEmail,
        EmailRecipientUnavailable
    }
}
