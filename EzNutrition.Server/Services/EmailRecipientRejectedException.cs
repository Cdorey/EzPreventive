namespace EzNutrition.Server.Services;

/// <summary>
/// 表示邮件服务明确拒绝了收件邮箱。异常详情仅用于服务端诊断。
/// </summary>
public sealed class EmailRecipientRejectedException(Exception innerException)
    : Exception("The email service rejected the recipient mailbox.", innerException);
