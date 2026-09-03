using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[Action]")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [TypeFilter(typeof(AuthenticationExceptionFilter))]
    public class AuthController(
        ILogger<AuthController> logger,
        AuthManagerRepository authManagerRepository,
        AuthenticationSessionService authenticationSessions,
        AccountSecurityService accountSecurityService,
        IAccountRecoveryQueue accountRecoveryQueue,
        CertificateFileStore certificateFileStore) : ControllerBase
    {
        /// <summary>
        /// 桌面客户端登录，返回短期访问令牌及一次性刷新凭据。
        /// </summary>
        /// <param name="request">账号密码及保持登录选项。</param>
        /// <param name="cancellationToken">请求取消信号。</param>
        [HttpPost]
        [EnableRateLimiting("Login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequestDto request, CancellationToken cancellationToken)
        {
            return Ok(await authManagerRepository.Login(
                request.UserName, request.Password, isBrowser: false,
                request.RememberLogin, cancellationToken));
        }

        /// <summary>使用桌面刷新凭据轮换令牌；不要求访问令牌仍然有效。</summary>
        [HttpPost]
        [EnableRateLimiting("Refresh")]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshRequestDto request, CancellationToken cancellationToken) =>
            Ok(await authenticationSessions.RefreshAsync(
                request.RefreshToken, isBrowser: false, request.SessionId, cancellationToken));

        /// <summary>撤销桌面当前会话；重复请求保持幂等。</summary>
        [HttpPost]
        [EnableRateLimiting("Refresh")]
        public async Task<IActionResult> Logout(
            [FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
        {
            await authenticationSessions.RevokeAsync(
                request.RefreshToken, isBrowser: false, request.SessionId, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// 用户注册，同时提交基本信息和专业身份信息
        /// 返回一个 uploadTicket，供后续上传证件照片使用
        /// </summary>
        [HttpPost]
        [EnableRateLimiting("AccountRecovery")]
        public async Task<IActionResult> Register([FromBody] RegistrationDto registrationDto)
        {
            if (!ModelState.IsValid)
            {
                logger.LogWarning("Invalid registration attempt for user {Username}.", registrationDto.UserName);
                return BadRequest(ModelState);
            }

            try
            {
                logger.LogInformation("User {Username} attempting to register.", registrationDto.UserName);
                RegistrationResultDto result = await authManagerRepository.RegisterUserAsync(registrationDto);
                if (!result.Success)
                {
                    logger.LogWarning("Registration failed for user {Username}: {Message}", registrationDto.UserName, result.Message);
                    return BadRequest(result.Message);
                }
                else
                {
                    logger.LogInformation("User {Username} registered successfully.", registrationDto.UserName);
                    return Ok(result);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error occurred while registering user {Username}.", registrationDto.UserName);
                return StatusCode(StatusCodes.Status500InternalServerError, "注册服务暂时不可用，请稍后重试。");
            }
        }

        /// <summary>
        /// 确认邮箱地址
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        [HttpGet]
        [EnableRateLimiting("AccountRecovery")]
        public IActionResult ConfirmEmail([FromQuery][Required] string userId, [FromQuery][Required] string token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("确认链接不完整，请检查");
            }

            var destination = QueryHelpers.AddQueryString(
                "/account/confirm-email",
                new Dictionary<string, string?>
                {
                    ["userId"] = userId,
                    ["token"] = token
                });
            return Redirect(destination);
        }

        [HttpPost]
        [EnableRateLimiting("AccountRecovery")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto request)
        {
            var result = await accountSecurityService.ConfirmEmailAsync(request.UserId, request.Token);
            return ToActionResult(result);
        }

        [HttpPost]
        [EnableRateLimiting("AccountRecovery")]
        public IActionResult ResendEmailConfirmation(
            [FromBody] ResendEmailConfirmationDto request)
        {
            if (!accountRecoveryQueue.TryEnqueue(AccountRecoveryRequestKind.EmailConfirmation, request.Email))
            {
                return RecoveryQueueUnavailable();
            }

            return Accepted(new AccountOperationResultDto
            {
                Success = true,
                Message = AccountSecurityService.GenericConfirmationResponse
            });
        }

        [HttpPost]
        [EnableRateLimiting("AccountRecovery")]
        public IActionResult ForgotPassword(
            [FromBody] ForgotPasswordDto request)
        {
            if (!accountRecoveryQueue.TryEnqueue(AccountRecoveryRequestKind.PasswordReset, request.Email))
            {
                return RecoveryQueueUnavailable();
            }

            return Accepted(new AccountOperationResultDto
            {
                Success = true,
                Message = AccountSecurityService.GenericPasswordResetResponse
            });
        }

        [HttpPost]
        [EnableRateLimiting("AccountRecovery")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            var result = await accountSecurityService.ResetPasswordAsync(request);
            return ToActionResult(result);
        }

        [HttpPost]
        [EnableRateLimiting("AccountRecovery")]
        public async Task<IActionResult> ConfirmEmailChange(
            [FromBody] ConfirmEmailChangeDto request,
            CancellationToken cancellationToken)
        {
            var result = await accountSecurityService.ConfirmEmailChangeAsync(
                request,
                cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>
        /// 根据 uploadTicket 上传证件照片
        /// </summary>
        [HttpPost("{uploadTicket}")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(CertificateFileStore.MaxFileSize)]
        public async Task<IActionResult> UploadCertificate(
            [FromForm] IFormFile? certificateFile,
            [FromRoute] string uploadTicket,
            CancellationToken cancellationToken)
        {
            if (certificateFile == null || certificateFile.Length == 0)
            {
                logger.LogWarning("No file uploaded for upload ticket {UploadTicket}.", uploadTicket);
                return BadRequest("No file uploaded.");
            }

            if (!Guid.TryParse(uploadTicket, out var ticket) ||
                !await authManagerRepository.ValidateUploadTicket(ticket, cancellationToken))
            {
                logger.LogWarning("Invalid upload ticket {UploadTicket}.", uploadTicket);
                return BadRequest("Invalid upload ticket.");
            }

            try
            {
                await certificateFileStore.SaveAsync(ticket, certificateFile, cancellationToken);
                logger.LogInformation("File uploaded successfully for upload ticket {UploadTicket}.", uploadTicket);
                return Ok();
            }
            catch (InvalidDataException ex)
            {
                logger.LogWarning(ex, "Invalid certificate image for upload ticket {UploadTicket}.", uploadTicket);
                return BadRequest(ex.Message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Certificate upload for ticket {UploadTicket} was cancelled.", uploadTicket);
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error occurred while uploading file for upload ticket {UploadTicket}.", uploadTicket);
                return StatusCode(StatusCodes.Status500InternalServerError, "证件图片上传失败，请稍后重试。");
            }
        }

        private IActionResult ToActionResult(AccountSecurityResult result)
        {
            var response = new AccountOperationResultDto
            {
                Success = result.Succeeded,
                Message = result.Message
            };
            return result.Succeeded ? Ok(response) : BadRequest(response);
        }

        private IActionResult RecoveryQueueUnavailable()
        {
            Response.Headers.RetryAfter = "60";
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new AccountOperationResultDto
                {
                    Success = false,
                    Message = "邮件服务当前繁忙，请稍后重试。"
                });
        }
    }
}
