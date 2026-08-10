using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using EzNutrition.Server.Services.Settings;
using System.Text.Encodings.Web;

namespace EzNutrition.Server.Services
{
    /// <summary>
    /// 使用 MailKit 实现 IEmailSender<IdentityUser> 接口
    /// </summary>
    public class SmtpEmailSender(
        IOptions<EmailSettings> options,
        ILogger<SmtpEmailSender> logger) : IEmailSender<IdentityUser>, IAccountEmailSender
    {
        private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);
        private readonly EmailSettings smtpSettings = options.Value;

        Task IEmailSender<IdentityUser>.SendConfirmationLinkAsync(
            IdentityUser user,
            string email,
            string confirmationLink) =>
            SendConfirmationLinkAsync(user, email, confirmationLink);

        Task IEmailSender<IdentityUser>.SendPasswordResetLinkAsync(
            IdentityUser user,
            string email,
            string resetLink) =>
            SendPasswordResetLinkAsync(user, email, resetLink);

        public async Task SendConfirmationLinkAsync(
            IdentityUser user,
            string email,
            string confirmationLink,
            CancellationToken cancellationToken = default)
        {
            confirmationLink = HtmlEncoder.Default.Encode(confirmationLink);
            string subject = "欢迎加入 EzNutrition - 请确认您的电子邮箱";
            string body = $@"
<div style='font-family:Arial, sans-serif; font-size:14px; color:#333;'>
  <h2 style='color:#2d89ef;'>欢迎加入 EzNutrition！</h2>
  <p>亲爱的用户，</p>
  <p>感谢您注册 EzNutrition！为了确认这是您本人使用的电子邮箱地址，请阅读以下内容并点击确认按钮：</p>
  
  <h3>许可协议 (AGPL-3)</h3>
  <p>
    本程序以 <strong>GNU Affero General Public License v3.0 (AGPL-3)</strong> 许可发布。<br/>
    请注意，AGPL-3 许可要求：如果您对本程序进行修改并在网络上公开发布，则必须公开您的源代码；您必须保留本程序及其派生版本中原作者的署名，并附上完整的许可文本；任何基于本程序构建的衍生产品，也必须以相同的 AGPL-3 许可发布。<br/>
    详细信息请参见 <a href='https://www.gnu.org/licenses/agpl-3.0.html' target='_blank'>AGPL-3 许可协议</a>。
  </p>
  
  <h3>源代码地址</h3>
  <p>
    本程序的完整源代码发布在 GitHub 上，欢迎访问：<br/>
    <a href='https://github.com/Cdorey/EzPreventive.git' target='_blank'>https://github.com/Cdorey/EzPreventive.git</a>
  </p>
  
  <h3>免责声明</h3>
  <p>
    本程序的计算方法及其所有模型参数基于营养学教科书及营养学会公开发布的参考资料构建，包括但不限于《营养与食品卫生学》、《中国居民膳食指南》、《中国居民膳食营养素参考摄入量2023》和《食物成分表》等权威资料。<br/>
    EzNutrition 不对任何未经验证的额外内容承担责任。<br/>
    本工具仅限于具有营养专业背景的医护人员基于工作目的，或者营养相关专业的高校教师和学生基于教学目的使用，不应作为其他用途。
  </p>
  
  <h3>使用限制</h3>
  <p>
    本工具旨在为专业人员提供辅助计算能力，所有输出结果仅供专业人员参考，并不构成任何形式的医疗、营养或健康建议。<br/>
    使用本工具时，用户应遵守所在地的相关法律法规，并对所做决策自行负责。<br/>
    如因不当使用本工具而导致任何损失，EzNutrition及其开发者概不负责。<br/>
    除了AI辅助生成膳食建议的接口外，本工具在核定营养学参数时，几乎所有计算均通过Blazor WASM技术在您本地计算机上运行，相关计算逻辑可通过本程序的完整源代码进行查验。<br/>
    除了AI辅助生成膳食建议的接口外，EzNutrition的服务端仅负责用户身份认证和提供原始公开参数，不参与营养学参数的实际计算过程。
  </p>

  
  <p>如果您同意上述所有内容，请点击下面的按钮确认您的电子邮箱：</p>
  <p>
    <a href='{confirmationLink}' style='display:inline-block; padding:10px 20px; background-color:#2d89ef; color:#fff; text-decoration:none; border-radius:4px;'>确认我的电子邮箱</a>
  </p>
  <p>如果按钮无法点击，请复制下面的链接到浏览器地址栏中访问：</p>
  <p style='word-break:break-all;'>{confirmationLink}</p>
  
  <p>祝您生活愉快！</p>
  <p>此致,<br/>作者 CdoreyPoisson</p>
</div>";
            await SendEmailAsync(user.UserName ?? string.Empty, email, subject, body, cancellationToken);
        }

        public async Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
        {
            string subject = "Password Reset Code";
            string body = $"Your password reset code is: {resetCode}";
            await SendEmailAsync(user.UserName ?? string.Empty, email, subject, body);
        }

        public async Task SendPasswordResetLinkAsync(
            IdentityUser user,
            string email,
            string resetLink,
            CancellationToken cancellationToken = default)
        {
            resetLink = HtmlEncoder.Default.Encode(resetLink);
            string subject = "EzNutrition 密码重置";
            string body = $@"
<div style='font-family:Arial, sans-serif; font-size:14px; color:#333;'>
  <h2 style='color:#2d89ef;'>重置您的 EzNutrition 密码</h2>
  <p>我们收到了该账户的密码重置请求。请点击下面的按钮设置新密码：</p>
  <p><a href='{resetLink}' style='display:inline-block; padding:10px 20px; background-color:#2d89ef; color:#fff; text-decoration:none; border-radius:4px;'>设置新密码</a></p>
  <p>如果按钮无法点击，请复制下面的链接到浏览器地址栏：</p>
  <p style='word-break:break-all;'>{resetLink}</p>
  <p>如果这不是您发起的请求，可以忽略这封邮件。</p>
</div>";
            await SendEmailAsync(user.UserName ?? string.Empty, email, subject, body, cancellationToken);
        }

        public async Task SendEmailChangeLinkAsync(
            IdentityUser user,
            string newEmail,
            string confirmationLink,
            CancellationToken cancellationToken = default)
        {
            confirmationLink = HtmlEncoder.Default.Encode(confirmationLink);
            var encodedEmail = HtmlEncoder.Default.Encode(newEmail);
            const string subject = "确认您的 EzNutrition 新邮箱";
            var body = $@"
<div style='font-family:Arial, sans-serif; font-size:14px; color:#333;'>
  <h2 style='color:#2d89ef;'>确认新电子邮箱</h2>
  <p>您申请将 EzNutrition 账户邮箱修改为 <strong>{encodedEmail}</strong>。</p>
  <p>请点击下面的按钮完成变更；确认前，原邮箱仍然有效。</p>
  <p><a href='{confirmationLink}' style='display:inline-block; padding:10px 20px; background-color:#2d89ef; color:#fff; text-decoration:none; border-radius:4px;'>确认新邮箱</a></p>
  <p>如果按钮无法点击，请复制下面的链接到浏览器地址栏：</p>
  <p style='word-break:break-all;'>{confirmationLink}</p>
  <p>如果这不是您发起的请求，请不要点击链接，并尽快修改账户密码。</p>
</div>";
            await SendEmailAsync(user.UserName ?? string.Empty, newEmail, subject, body, cancellationToken);
        }

        public async Task SendEmailChangedNotificationAsync(
            IdentityUser user,
            string previousEmail,
            string newEmail,
            CancellationToken cancellationToken = default)
        {
            var encodedEmail = HtmlEncoder.Default.Encode(newEmail);
            const string subject = "EzNutrition 账户邮箱已修改";
            var body = $@"
<div style='font-family:Arial, sans-serif; font-size:14px; color:#333;'>
  <h2 style='color:#2d89ef;'>账户安全通知</h2>
  <p>您的 EzNutrition 账户邮箱已修改为 <strong>{encodedEmail}</strong>。</p>
  <p>如果这不是您本人操作，请尽快联系管理员并修改账户密码。</p>
</div>";
            await SendEmailAsync(user.UserName ?? string.Empty, previousEmail, subject, body, cancellationToken);
        }

        private async Task SendEmailAsync(
            string username,
            string email,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            using var deliveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deliveryCancellation.CancelAfter(DeliveryTimeout);
            var deliveryToken = deliveryCancellation.Token;

            // 构造邮件消息
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtpSettings.SenderName, smtpSettings.SenderEmail));
            message.To.Add(new MailboxAddress(username, email));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = body
            };
            message.Body = builder.ToMessageBody();

            // 使用 MailKit 的 SmtpClient 发送邮件
            using var client = new SmtpClient();
            await client.ConnectAsync(
                smtpSettings.SmtpServer,
                smtpSettings.SmtpPort,
                SecureSocketOptions.SslOnConnect,
                deliveryToken);
            await client.AuthenticateAsync(
                smtpSettings.UserName,
                smtpSettings.Password,
                deliveryToken);
            await client.SendAsync(message, deliveryToken);
            try
            {
                await client.DisconnectAsync(true, deliveryToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SMTP accepted an email, but the client could not disconnect cleanly.");
            }
        }
    }
}
