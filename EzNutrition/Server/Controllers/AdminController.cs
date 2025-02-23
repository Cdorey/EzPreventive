using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Extension;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Data.Entities;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    [Authorize(Policy = PolicyList.Admin)]
    public class AdminController(RoleManager<IdentityRole> roleManager, ILogger<AdminController> logger, UserManager<IdentityUser> userManager, ApplicationDbContext applicationDbContext) : ControllerBase
    {
        /// <summary>
        /// 添加角色
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> AddRole([FromForm][Required] string role)
        {
            try
            {
                var result = await roleManager.CreateAsync(new IdentityRole { Name = role });
                return result.Succeeded ? Ok(new { message = "Role created successfully" }) : BadRequest(result.Errors);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AddRole");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        [HttpGet("{role?}")]
        public async Task<IActionResult> GetUsers(string? role = default)
        {
            if (role != default)
            {
                logger.LogInformation("获取角色为 {role} 的用户列表", role);
                return Ok(await userManager.GetUsersInRoleAsync(role));
            }
            else
            {
                logger.LogInformation("获取所有用户列表");
                return Ok(userManager.Users);
            }
        }

        /// <summary>
        /// 发布通知
        /// </summary>
        /// <param name="noticeDescription"></param>
        /// <param name="noticeTitle"></param>
        /// <param name="isCoverLetter"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Notification([FromForm][Required] string noticeDescription, [FromForm] string? noticeTitle, [FromForm] bool isCoverLetter = false)
        {
            var x = new Notice
            {
                Title = noticeTitle ?? string.Empty,
                Description = noticeDescription,
                CreateTime = DateTime.Now,
                IsCoverLetter = isCoverLetter,
                PublisherId = User.FindFirst(ClaimTypes.Upn)?.Value ?? string.Empty,
            };
            applicationDbContext.Add(x);
            await applicationDbContext.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// 根据 userId 获取用户基本信息、所属角色以及 Claims
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <returns>返回包含用户信息的 DTO</returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserInfo([FromRoute] string userId)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                logger.LogWarning("User with ID {UserId} not found.", userId);
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
                Claims = [.. claims.Select(c => new UserClaimDto { Type = c.Type, Value = c.Value })]
            };
            return Ok(dto);
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("用户不存在");

            var result = await userManager.DeleteAsync(user);
            return result.Succeeded ? Ok(new { message = "用户删除成功" }) : BadRequest(result.Errors);
        }

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> UpdateUser([FromBody] UserInfoDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // 根据 DTO 中的 userId 获取用户
            var user = await userManager.FindByIdAsync(dto.UserId);
            if (user == null)
            {
                logger.LogWarning("用户 {UserId} 不存在", dto.UserId);
                return NotFound("用户不存在");
            }

            // 更新用户基本属性
            user.UserName = dto.UserName;
            user.Email = dto.Email;
            user.PhoneNumber = dto.PhoneNumber;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                logger.LogError("更新用户基本信息失败：{Errors}", updateResult.Errors);
                return BadRequest(updateResult.Errors);
            }

            // 更新角色：同步 DTO 中的角色和当前用户角色
            var currentRoles = await userManager.GetRolesAsync(user);
            // 需要添加的角色：DTO 中存在，但当前用户没有
            var rolesToAdd = dto.Roles.Except(currentRoles).ToList();
            // 需要删除的角色：当前用户存在，但 DTO 中没有
            var rolesToRemove = currentRoles.Except(dto.Roles).ToList();

            if (rolesToRemove.Count != 0)
            {
                var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    logger.LogError("删除角色失败：{Errors}", removeResult.Errors);
                    return BadRequest(removeResult.Errors);
                }
            }
            if (rolesToAdd.Count != 0)
            {
                var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                {
                    logger.LogError("添加角色失败：{Errors}", addResult.Errors);
                    return BadRequest(addResult.Errors);
                }
            }

            // 更新 Claims：同步 DTO 中的 Claims 和当前用户的 Claims
            var currentClaims = await userManager.GetClaimsAsync(user);
            // 将 DTO 中的 Claim 转换为 Claim 对象
            var dtoClaims = dto.Claims.Select(c => new Claim(c.Type, c.Value)).ToList();

            // 需要删除的 Claims：当前用户存在，但在 DTO 中不存在
            var claimsToRemove = currentClaims.Where(cc => !dtoClaims.Any(dc => dc.Type == cc.Type && dc.Value == cc.Value)).ToList();
            // 需要添加的 Claims：DTO 中存在，但当前用户不存在
            var claimsToAdd = dtoClaims.Where(dc => !currentClaims.Any(cc => cc.Type == dc.Type && cc.Value == dc.Value)).ToList();

            foreach (var claim in claimsToRemove)
            {
                var removeClaimResult = await userManager.RemoveClaimAsync(user, claim);
                if (!removeClaimResult.Succeeded)
                {
                    logger.LogError("删除 Claim {ClaimType} 失败：{Errors}", claim.Type, removeClaimResult.Errors);
                    return BadRequest(removeClaimResult.Errors);
                }
            }

            foreach (var claim in claimsToAdd)
            {
                var addClaimResult = await userManager.AddClaimAsync(user, claim);
                if (!addClaimResult.Succeeded)
                {
                    logger.LogError("添加 Claim {ClaimType} 失败：{Errors}", claim.Type, addClaimResult.Errors);
                    return BadRequest(addClaimResult.Errors);
                }
            }

            return Ok(new { message = "用户更新成功" });
        }

        [HttpGet]
        public async Task<IActionResult> ProfessionalCertificationRequests()
        {
            var x = await applicationDbContext.ProfessionalCertificationRequests.AsNoTracking().Select(x => x.ToDto()).ToListAsync();
            return Ok(x);
        }
    }
}