using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using EzNutrition.Server.Services.Settings;
using System.Threading.Tasks;

namespace EzNutrition.Server.Services
{
    /// <summary>
    /// 使用 MailKit 实现 IEmailSender<IdentityUser> 接口
    /// </summary>
    public class SmtpEmailSender(IOptions<EmailSettings> options) : IEmailSender<IdentityUser>
    {
        private readonly EmailSettings smtpSettings = options.Value;

        public async Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
        {
            string subject = "欢迎加入 EzNutrition - 请确认您的电子邮箱";
            string body = $@"
<div style='font-family:Arial, sans-serif; font-size:14px; color:#333;'>
  <h2 style='color:#2d89ef;'>欢迎加入 EzNutrition！</h2>
  <p>亲爱的用户，</p>
  <p>感谢您注册 EzNutrition！为了激活您的账户并确认您的电子邮箱地址，请点击下面的按钮。点击激活即表示您同意以下所有内容：</p>
  
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

  
  <p>如果您同意上述所有内容，请点击下面的按钮激活您的账户：</p>
  <p>
    <a href='{confirmationLink}' style='display:inline-block; padding:10px 20px; background-color:#2d89ef; color:#fff; text-decoration:none; border-radius:4px;'>确认并激活我的账户</a>
  </p>
  <p>如果按钮无法点击，请复制下面的链接到浏览器地址栏中访问：</p>
  <p style='word-break:break-all;'>{confirmationLink}</p>
  
  <p>祝您生活愉快！</p>
  <p>此致,<br/>作者 CdoreyPoisson</p>
</div>";
            await SendEmailAsync(user.UserName ?? string.Empty, email, subject, body);
        }

        public async Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
        {
            string subject = "Password Reset Code";
            string body = $"Your password reset code is: {resetCode}";
            await SendEmailAsync(user.UserName ?? string.Empty, email, subject, body);
        }

        public async Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink)
        {
            string subject = "Reset your password";
            string body = $"Please reset your password by clicking this link: <a href='{resetLink}'>Reset Password</a>.";
            await SendEmailAsync(user.UserName ?? string.Empty, email, subject, body);
        }

        private async Task SendEmailAsync(string username, string email, string subject, string body)
        {
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
            await client.ConnectAsync(smtpSettings.SmtpServer, smtpSettings.SmtpPort, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(smtpSettings.UserName, smtpSettings.Password);
            var x = await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
