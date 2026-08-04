using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[Action]")]
    public class AuthController(
        ILogger<AuthController> logger,
        AuthManagerRepository authManagerRepository,
        CertificateFileStore certificateFileStore) : ControllerBase
    {
        /// <summary>
        /// 登录，返回一个包含 token 的对象
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                return BadRequest("用户名和密码不能为空。");
            }

            try
            {
                logger.LogInformation("User {Username} attempting to log in.", username);
                var result = await authManagerRepository.Login(username, password);
                logger.LogInformation("User {Username} logged in successfully.", username);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("用户名/密码不正确");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error occurred while logging in user {Username}.", username);
                return StatusCode(StatusCodes.Status500InternalServerError, "登录服务暂时不可用，请稍后重试。");
            }
        }

        /// <summary>
        /// 检查邮箱是否可用
        /// GET api/Account/CheckEmail?email=xxx@example.com
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CheckEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
                return BadRequest("A valid email address is required.");

            var available = await authManagerRepository.CheckEmail(email.Trim());
            return Ok(available);
        }

        /// <summary>
        /// 用户注册，同时提交基本信息和专业身份信息
        /// 返回一个 uploadTicket，供后续上传证件照片使用
        /// </summary>
        [HttpPost]
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
        public async Task<IActionResult> ConfirmEmail([FromQuery][Required] string userId, [FromQuery][Required] string token)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("确认链接不完整，请检查");
            }

            try
            {
                var result = await authManagerRepository.ConfirmEmailAsync(userId, token);
                if (result is not null && result.Succeeded)
                {
                    return Ok("Email地址已确认！");
                }

                return BadRequest("Email地址确认失败，请重试或重新请求确认邮件。");
            }
            catch (FormatException)
            {
                return BadRequest("Email确认链接格式不正确。");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while confirming email for user {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Email确认服务暂时不可用。");
            }
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
    }
}
