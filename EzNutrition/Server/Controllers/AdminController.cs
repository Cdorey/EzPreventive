using AntDesign;
using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Extension;
using EzNutrition.Server.Services;
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
    public class AdminController(
        RoleManager<IdentityRole> roleManager,
        ILogger<AdminController> logger,
        UserManager<IdentityUser> userManager,
        ApplicationDbContext applicationDbContext,
        CertificateFileStore certificateFileStore) : ControllerBase
    {
        /// <summary>
        /// 添加角色
        /// </summary>
        /// <param name="newRole"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> AddRole([FromForm][Required] string newRole)
        {
            if (string.IsNullOrWhiteSpace(newRole))
            {
                return BadRequest("Role name is required.");
            }

            try
            {
                var normalizedRoleName = newRole.Trim();
                if (await roleManager.RoleExistsAsync(normalizedRoleName))
                {
                    return Conflict("Role already exists.");
                }

                var result = await roleManager.CreateAsync(new IdentityRole { Name = normalizedRoleName });
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
        public async Task<IActionResult> Users(string? role = default, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                logger.LogInformation("获取角色为 {role} 的用户列表", role);
                var usersInRole = await userManager.GetUsersInRoleAsync(role.Trim());
                return Ok(usersInRole.Select(user => user.ToDto()).ToList());
            }
            else
            {
                logger.LogInformation("获取所有用户列表");
                var users = await userManager.Users
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
                return Ok(users.Select(user => user.ToDto()).ToList());
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
        public async Task<IActionResult> UpdateRoleClaims(
            [FromRoute] string roleName,
            [FromBody] List<ClaimDto> newClaims,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roleName) || newClaims is null ||
                newClaims.Any(claim => string.IsNullOrWhiteSpace(claim.Type) || string.IsNullOrWhiteSpace(claim.Value)))
            {
                return BadRequest("Role name and non-empty claim type/value pairs are required.");
            }

            if (newClaims.Any(claim => JwtService.IsReservedClaimType(claim.Type)))
            {
                return BadRequest("System identity claims cannot be assigned to a role.");
            }

            newClaims = newClaims
                .DistinctBy(claim => (claim.Type, claim.Value))
                .ToList();

            // 查找角色
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return NotFound("Role not found");
            }

            // 获取现有的 Claim 列表
            var existingClaims = await roleManager.GetClaimsAsync(role);
            await using var transaction = await applicationDbContext.Database.BeginTransactionAsync(cancellationToken);

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

            var usersInRole = await userManager.GetUsersInRoleAsync(roleName);
            foreach (var user in usersInRole)
            {
                var stampResult = await userManager.UpdateSecurityStampAsync(user);
                if (!stampResult.Succeeded)
                {
                    logger.LogError(
                        "更新角色 {RoleName} 后使用户 {UserId} 的登录凭据失效失败：{Errors}",
                        roleName,
                        user.Id,
                        stampResult.Errors);
                    return BadRequest(stampResult.Errors);
                }
            }

            await transaction.CommitAsync(cancellationToken);
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
        public async Task<IActionResult> Notification(
            [FromBody] NotificationDto notification,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var publisherId = User.FindFirstValue(ClaimTypes.Upn);
            if (string.IsNullOrWhiteSpace(publisherId))
            {
                return Unauthorized();
            }

            var x = new Notice
            {
                Title = notification.NoticeTitle ?? string.Empty,
                Description = notification.NoticeDescription,
                CreateTime = DateTime.Now,
                IsCoverLetter = notification.IsCoverLetter,
                PublisherId = publisherId,
            };
            applicationDbContext.Add(x);
            await applicationDbContext.SaveChangesAsync(cancellationToken);
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
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
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
        public async Task<IActionResult> DeleteUser(string userId, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("用户不存在");

            var certificateTickets = await applicationDbContext.ProfessionalCertificationRequests
                .AsNoTracking()
                .Where(request => request.UserId == userId && request.CertificateTicket != null)
                .Select(request => request.CertificateTicket!.Value)
                .ToListAsync(cancellationToken);

            await using var transaction = await applicationDbContext.Database.BeginTransactionAsync(cancellationToken);
            await applicationDbContext.ProfessionalCertificationRequests
                .Where(request => request.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            await transaction.CommitAsync(cancellationToken);

            foreach (var ticket in certificateTickets)
            {
                try
                {
                    certificateFileStore.Delete(ticket);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "删除用户 {UserId} 后清理证件文件 {Ticket} 失败", userId, ticket);
                }
            }

            return Ok(new { message = "用户删除成功" });
        }

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> UpdateUser(
            [FromBody] UserInfoDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.UserName) ||
                string.IsNullOrWhiteSpace(dto.Email) || dto.Roles is null || dto.Claims is null ||
                dto.Claims.Any(claim => string.IsNullOrWhiteSpace(claim.Type) || string.IsNullOrWhiteSpace(claim.Value)))
            {
                return BadRequest("用户标识、用户名、邮箱、角色和有效的 Claim 均不能为空。");
            }

            if (dto.Claims.Any(claim => JwtService.IsReservedClaimType(claim.Type)))
            {
                return BadRequest("系统身份 Claim 不能由管理员直接分配。");
            }

            var requestedRoles = dto.Roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var role in requestedRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    return BadRequest($"角色不存在：{role}");
                }
            }

            var requestedClaims = dto.Claims
                .DistinctBy(claim => (claim.Type, claim.Value))
                .ToList();

            // 根据 DTO 中的 userId 获取用户
            var user = await userManager.FindByIdAsync(dto.UserId);
            if (user == null)
            {
                logger.LogWarning("用户 {UserId} 不存在", dto.UserId);
                return NotFound("用户不存在");
            }

            await using var transaction = await applicationDbContext.Database.BeginTransactionAsync(cancellationToken);

            // 更新用户基本属性
            var emailChanged = !string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase);
            var phoneNumberChanged = !string.Equals(
                user.PhoneNumber ?? string.Empty,
                dto.PhoneNumber ?? string.Empty,
                StringComparison.Ordinal);
            user.UserName = dto.UserName;
            user.Email = dto.Email;
            user.PhoneNumber = dto.PhoneNumber;
            if (emailChanged)
            {
                user.EmailConfirmed = false;
            }
            if (phoneNumberChanged)
            {
                user.PhoneNumberConfirmed = false;
            }

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                logger.LogError("更新用户基本信息失败：{Errors}", updateResult.Errors);
                return BadRequest(updateResult.Errors);
            }

            // 更新角色：同步 DTO 中的角色和当前用户角色
            var currentRoles = await userManager.GetRolesAsync(user);
            // 需要添加的角色：DTO 中存在，但当前用户没有
            var rolesToAdd = requestedRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
            // 需要删除的角色：当前用户存在，但 DTO 中没有
            var rolesToRemove = currentRoles.Except(requestedRoles, StringComparer.OrdinalIgnoreCase).ToList();

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
            var dtoClaims = requestedClaims.Select(c => new Claim(c.Type, c.Value)).ToList();

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

            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                logger.LogError("使用户 {UserId} 的旧登录凭据失效失败：{Errors}", user.Id, stampResult.Errors);
                return BadRequest(stampResult.Errors);
            }

            await transaction.CommitAsync(cancellationToken);
            return Ok(new { message = "用户更新成功" });
        }

        [HttpGet]
        public async Task<IActionResult> ProfessionalCertificationRequests(CancellationToken cancellationToken)
        {
            var requests = await applicationDbContext.ProfessionalCertificationRequests
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return Ok(requests.Select(request => request.ToDto()).ToList());
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
                if (!Guid.TryParse(ticket, out var parsedTicket))
                {
                    return BadRequest("Ticket 格式不正确");
                }

                var certificateFile = certificateFileStore.OpenRead(parsedTicket);
                if (certificateFile is null)
                {
                    logger.LogWarning("未找到 Ticket {Ticket} 对应的文件", ticket);
                    return NotFound("未找到对应文件");
                }

                return File(certificateFile.Content, certificateFile.ContentType);
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
        public async Task<IActionResult> UpdateRequest(
            [FromBody] ProfessionalCertificationRequestDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!Enum.IsDefined(dto.Status))
            {
                return BadRequest("认证请求状态无效。");
            }

            try
            {
                // 根据 DTO 中的 Id 查找数据库中的对象
                var request = await applicationDbContext.ProfessionalCertificationRequests.FindAsync(
                    [dto.Id],
                    cancellationToken);
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
                await applicationDbContext.SaveChangesAsync(cancellationToken);

                if (dto.Status != RequestStatus.Pending && ticket is not null)
                {
                    try
                    {
                        certificateFileStore.Delete(ticket.Value);
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
