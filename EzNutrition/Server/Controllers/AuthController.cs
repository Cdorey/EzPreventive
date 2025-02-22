using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[Action]")]
    public class AuthController(ILogger<AuthController> logger, AuthManagerRepository authManagerRepository) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
        {
            try
            {
                logger.LogInformation("User {Username} attempting to log in.", username);
                var result = await authManagerRepository.Login(username, password);
                logger.LogInformation("User {Username} logged in successfully.", username);
                return Ok(result);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error occurred while logging in user {Username}.", username);
                return BadRequest(e.Message);
            }
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
                logger.LogWarning("Invalid registration attempt with data: {@RegistrationDto}", registrationDto);
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
                    return Ok(new { result.UploadTicket, result.Message });
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error occurred while registering user {Username}.", registrationDto.UserName);
                return BadRequest(e.Message);
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateProfessionalIdentity([FromBody] ProfessionalIdentityDto professionalIdentityDto)
        {
            if (!ModelState.IsValid)
            {
                logger.LogWarning("Invalid professional identity creation attempt with data: {@ProfessionalIdentityDto}", professionalIdentityDto);
                return BadRequest(ModelState);
            }

            try
            {
                logger.LogInformation("User {User} attempting to create professional identity.", User.Identity?.Name);
                await authManagerRepository.CreateProfessionalIdentityRequest(professionalIdentityDto, User);
                logger.LogInformation("Professional identity created successfully for user {User}.", User.Identity?.Name);
                return Ok();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error occurred while creating professional identity for user {User}.", User.Identity?.Name);
                return BadRequest(e.Message);
            }
        }

        /// <summary>
        /// 根据 uploadTicket 上传证件照片
        /// </summary>
        [HttpPost("{uploadTicket}")]
        public async Task<IActionResult> UploadCertificate([FromForm] IFormFile certificateFile, [FromRoute] string uploadTicket)
        {
            if (certificateFile == null || certificateFile.Length == 0)
            {
                logger.LogWarning("No file uploaded for upload ticket {UploadTicket}.", uploadTicket);
                return BadRequest("No file uploaded.");
            }

            if (!await authManagerRepository.ValidateUploadTicket(uploadTicket))
            {
                logger.LogWarning("Invalid upload ticket {UploadTicket}.", uploadTicket);
                return BadRequest("Invalid upload ticket.");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(certificateFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension) || certificateFile.Length > 10 * 1024 * 1024)
            {
                logger.LogWarning("Invalid file type or size for upload ticket {UploadTicket}.", uploadTicket);
                return BadRequest("Invalid file type or size.");
            }

            var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "TempUploads");
            if (!Directory.Exists(tempFolder))
            {
                Directory.CreateDirectory(tempFolder);
            }

            var fileName = $"{uploadTicket}{extension}";
            var filePath = Path.Combine(tempFolder, fileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await certificateFile.CopyToAsync(stream);
                }
                logger.LogInformation("File uploaded successfully for upload ticket {UploadTicket}.", uploadTicket);
                return Ok();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error occurred while uploading file for upload ticket {UploadTicket}.", uploadTicket);
                return BadRequest(e.Message);
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult Profile()
        {
            logger.LogInformation("User {User} accessed profile.", User.Identity?.Name);
            return Ok();
        }
    }
}