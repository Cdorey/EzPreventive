using AntDesign;
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
        /// <param name="newRole"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> AddRole([FromForm][Required] string newRole)
        {
            try
            {
                var result = await roleManager.CreateAsync(new IdentityRole { Name = newRole });
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
        public async Task<IActionResult> Users(string? role = default)
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
        /// 获取角色列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Roles()
        {
            return Ok(await roleManager.Roles.Select(x => x.Name).ToListAsync());
        }

        /// <summary>
        /// 获取指定角色的 Claim 列表
        /// </summary>
        /// <param name="roleName">角色名称</param>
        /// <returns>包含角色名称和对应 Claim 列表的 DTO</returns>
        [HttpGet("{roleName}")]
        public async Task<IActionResult> RoleClaims(string roleName)
        {
            // 通过角色名称查找角色对象
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return NotFound("Role not found");
            }

            // 通过 RoleManager 获取该角色的 Claims
            // 注意：此方法依赖于你的角色存储实现，如果使用默认 IdentityRole，
            // 可能需要扩展以支持角色 Claims。这里假设你的 RoleManager 支持 GetClaimsAsync。
            var claims = await roleManager.GetClaimsAsync(role);

            // 将 Identity中的 Claim 转换为 UserClaimDto 列表
            var roleClaimsDto = new RoleClaimsDto
            {
                RoleName = roleName,
                Claims = claims.Select(c => new ClaimDto { Type = c.Type, Value = c.Value }).ToList()
            };

            return Ok(roleClaimsDto);
        }

        /// <summary>
        /// 更新指定角色的 Claim 列表
        /// </summary>
        /// <param name="roleName">角色名称</param>
        /// <param name="newClaims">新的 Claim 列表</param>
        /// <returns>更新结果</returns>
        [HttpPut("{roleName}")]
        public async Task<IActionResult> UpdateRoleClaims([FromRoute] string roleName, [FromBody] List<ClaimDto> newClaims)
        {
            // 查找角色
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return NotFound("Role not found");
            }

            // 获取现有的 Claim 列表
            var existingClaims = await roleManager.GetClaimsAsync(role);

            // 移除所有现有 Claim
            foreach (var claim in existingClaims)
            {
                var removeResult = await roleManager.RemoveClaimAsync(role, claim);
                if (!removeResult.Succeeded)
                {
                    return BadRequest("Failed to remove existing claims");
                }
            }

            // 添加新传入的 Claim
            foreach (var claimDto in newClaims)
            {
                var claim = new Claim(claimDto.Type, claimDto.Value);
                var addResult = await roleManager.AddClaimAsync(role, claim);
                if (!addResult.Succeeded)
                {
                    return BadRequest($"Failed to add claim: {claimDto.Type}");
                }
            }

            return Ok(new { Message = "Role claims updated successfully" });
        }

        /// <summary>
        /// 发布通知
        /// </summary>
        /// <param name="noticeDescription"></param>
        /// <param name="noticeTitle"></param>
        /// <param name="isCoverLetter"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Notification([FromBody] NotificationDto notification)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var x = new Notice
            {
                Title = notification.NoticeTitle ?? string.Empty,
                Description = notification.NoticeDescription,
                CreateTime = DateTime.Now,
                IsCoverLetter = notification.IsCoverLetter,
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
                Claims = [.. claims.Select(c => new ClaimDto { Type = c.Type, Value = c.Value })]
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
            await applicationDbContext.ProfessionalCertificationRequests.Where(x => x.UserId == userId).ExecuteDeleteAsync();
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

        /// <summary>
        /// 根据 Ticket 请求上传的图片，Ticket 为文件名前缀（扩展名不可知）
        /// </summary>
        /// <param name="ticket">上传图片的 Ticket</param>
        /// <returns>图片文件流</returns>
        [HttpGet("{ticket}")]
        public IActionResult CertificateImage([FromRoute] string ticket)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ticket))
                {
                    return BadRequest("Ticket 不能为空");
                }

                var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "TempUploads");
                if (!Directory.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                }

                // 在 TempUploads 文件夹中查找以 ticket 为前缀的文件
                var files = Directory.GetFiles(tempFolder, ticket + ".*");
                if (files == null || files.Length == 0)
                {
                    logger.LogWarning("未找到 Ticket {Ticket} 对应的文件", ticket);
                    return NotFound("未找到对应文件");
                }

                // 默认取第一个匹配的文件
                var filePath = files[0];
                var ext = Path.GetExtension(filePath).ToLowerInvariant();

                // 根据扩展名设置 MIME 类型
                string contentType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => "application/octet-stream"
                };

                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return File(fileStream, contentType);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "根据 Ticket {Ticket} 获取文件失败", ticket);
                return StatusCode(500, "服务器内部错误");
            }
        }

        /// <summary>
        /// 更新用户专业认证请求
        /// </summary>
        /// <param name="dto">专业认证请求 DTO</param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> UpdateRequest([FromBody] ProfessionalCertificationRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // 根据 DTO 中的 Id 查找数据库中的对象
                var request = await applicationDbContext.ProfessionalCertificationRequests.FindAsync(dto.Id);
                if (request == null)
                {
                    logger.LogWarning("更新失败：请求 {RequestId} 不存在", dto.Id);
                    return NotFound("请求不存在");
                }
                var ticket = request.CertificateTicket;

                // 更新各属性
                request.Status = dto.Status;
                request.ProcessedTime = DateTime.Now;
                request.ProcessDetails = dto.ProcessDetails;
                request.Remarks = dto.Remarks;
                request.CertificateTicket = dto.Status == RequestStatus.Pending ? dto.CertificateTicket : null;
                // 保存更改
                await applicationDbContext.SaveChangesAsync();

                if (dto.Status != RequestStatus.Pending && ticket is not null)
                {
                    try
                    {
                        var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "TempUploads");
                        if (!Directory.Exists(tempFolder))
                        {
                            Directory.CreateDirectory(tempFolder);
                        }

                        // 在 TempUploads 文件夹中查找以 ticket 为前缀的文件
                        var files = Directory.GetFiles(tempFolder, ticket.ToString() + ".*");
                        files?.ForEach(System.IO.File.Delete);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "根据 Ticket {Ticket} 获取文件失败", ticket);
                    }

                }
                return Ok(new { message = "更新成功" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新请求 {RequestId} 出现异常", dto.Id);
                return StatusCode(500, "更新请求时发生错误");
            }
        }
    }
}