using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Extension;
using EzNutrition.Shared.Data.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[Action]")]
    [Authorize]
    public class UserController(ILogger<UserController> logger, AuthManagerRepository authManagerRepository, UserManager<IdentityUser> userManager, ApplicationDbContext applicationDbContext) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userName = User.Identity?.Name;
            if (userName is null)
            {
                return NotFound("用户不存在");
            }

            logger.LogInformation("User {User} accessed profile.", userName);
            var user = await userManager.FindByNameAsync(userName);
            if (user == null)
            {
                logger.LogWarning("User {User} not found.", userName);
                return NotFound("用户不存在");
            }

            // 获取用户的角色信息
            var roles = await userManager.GetRolesAsync(user);
            // 获取用户的 Claims 信息
            var claims = await userManager.GetClaimsAsync(user);

            var dto = new UserInfoDto
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Roles = [.. roles],
                Claims = [.. claims.Select(c => new ClaimDto { Type = c.Type, Value = c.Value })]
            };
            return Ok(dto);

        }

        /// <summary>
        /// 提交专业身份审核请求
        /// </summary>
        /// <param name="professionalIdentityDto"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateProfessionalIdentity(
            [FromBody] ProfessionalIdentityDto professionalIdentityDto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                logger.LogWarning("Invalid professional identity creation attempt with data: {@ProfessionalIdentityDto}", professionalIdentityDto);
                return BadRequest(ModelState);
            }

            try
            {
                logger.LogInformation("User {User} attempting to create professional identity.", User.Identity?.Name);
                var result = await authManagerRepository.CreateProfessionalIdentityRequest(
                    professionalIdentityDto,
                    User,
                    cancellationToken);
                logger.LogInformation("Professional identity created successfully for user {User}.", User.Identity?.Name);
                return Ok(new RegistrationResultDto
                {
                    Success = true,
                    UploadTicket = result,
                    Message = "Professional identity request received successfully."
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error occurred while creating professional identity for user {User}.", User.Identity?.Name);
                return StatusCode(StatusCodes.Status500InternalServerError, "专业身份认证服务暂时不可用，请稍后重试。");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ProfessionalIdentity(CancellationToken cancellationToken)
        {
            var userName = User.Identity?.Name;
            if (userName is null)
            {
                return NotFound("用户不存在");
            }
            logger.LogInformation("User {User} accessed professional identity.", userName);
            var user = await userManager.FindByNameAsync(userName);
            if (user == null)
            {
                logger.LogWarning("User {User} not found.", userName);
                return NotFound("用户不存在");
            }
            var professionalIdentityEntities = await applicationDbContext.ProfessionalCertificationRequests
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .ToListAsync(cancellationToken);
            var professionalIdentity = professionalIdentityEntities
                .Select(request => request.ToDto())
                .ToList();
            if (professionalIdentity.Count != 0)
            {
                return Ok(professionalIdentity);
            }
            else
            {
                return NotFound("专业认证请求列表为空");
            }
        }
    }
}
