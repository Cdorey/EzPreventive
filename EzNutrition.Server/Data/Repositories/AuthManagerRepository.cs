using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace EzNutrition.Server.Data.Repositories
{
    public class AuthManagerRepository(JwtService jwtService,
                                       ApplicationDbContext dbContext,
                                       UserManager<IdentityUser> userManager,
                                       RoleManager<IdentityRole> roleManager,
                                       SignInManager<IdentityUser> signInManager,
                                       ILogger<AuthManagerRepository> logger,
                                       AccountSecurityService accountSecurityService,
                                       LoginTimingEqualizer loginTimingEqualizer,
                                       IOptions<AuthBootstrapSettings> bootstrapOptions)
    {
        /// <summary>
        /// 创建基础的Role关系，以及管理员账号
        /// </summary>
        /// <returns></returns>
        public async Task Initialize()
        {
            logger.LogInformation("初始化角色和管理员账号");

            string[] requiredRoles = ["Admin", "Student", "Teacher", "Physician", "Nutritionist", "RD", "Epiman"];
            foreach (var roleName in requiredRoles)
            {
                if (await roleManager.FindByNameAsync(roleName) is not null)
                {
                    continue;
                }

                var createRoleResult = await roleManager.CreateAsync(new IdentityRole { Name = roleName });
                if (!createRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", createRoleResult.Errors.Select(error => error.Description));
                    logger.LogError("创建角色 {RoleName} 失败：{Errors}", roleName, errors);
                    throw new InvalidOperationException($"Failed to create required role '{roleName}'.");
                }
            }

            var admin = await userManager.FindByNameAsync("Admin");
            if (admin is null)
            {
                var password = bootstrapOptions.Value.AdminPassword;
                if (string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException(
                        "AuthBootstrap:AdminPassword must be supplied through a protected configuration source when creating the Admin account.");
                }

                var addUser = await userManager.CreateAsync(new IdentityUser { UserName = "Admin" }, password);
                if (!addUser.Succeeded)
                {
                    logger.LogError("创建Admin用户失败：{Errors}", addUser.Errors);
                    throw new InvalidOperationException("Failed to create the Admin user.");
                }

                logger.LogInformation("Admin用户创建成功；请立即使用受控配置中的初始密码登录并修改密码。");
                admin = await userManager.FindByNameAsync("Admin")
                    ?? throw new InvalidOperationException("The Admin user could not be loaded after creation.");
            }

            foreach (var roleName in new[] { "Admin", "Epiman" })
            {
                if (await userManager.IsInRoleAsync(admin, roleName))
                {
                    continue;
                }

                var addToRole = await userManager.AddToRoleAsync(admin, roleName);
                if (!addToRole.Succeeded)
                {
                    logger.LogError("为Admin用户添加角色 {RoleName} 失败：{Errors}", roleName, addToRole.Errors);
                    throw new InvalidOperationException($"Failed to add the Admin user to role '{roleName}'.");
                }
            }
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public async Task<string> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                throw new UnauthorizedAccessException("用户名/密码不正确");
            }

            var user = await userManager.FindByNameAsync(username.Trim());
            if (user is null)
            {
                loginTimingEqualizer.Verify(password);
            }
            else if ((await signInManager.CheckPasswordSignInAsync(user, password, true)).Succeeded &&
                (string.IsNullOrWhiteSpace(user.Email) || user.EmailConfirmed))
            {
                logger.LogInformation("用户登陆成功：{UserId}/{NormalizedUserName}", user.Id, user.NormalizedUserName);
                return await jwtService.GenerateJwtToken(user);
            }

            logger.LogWarning("用户登陆失败：{Username}", username);
            throw new UnauthorizedAccessException("用户名/密码不正确");
        }

        /// <summary>
        /// 锁定用户
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="days"></param>
        /// <returns></returns>
        public async Task LockUser(string userName, int days)
        {
            var user = await userManager.FindByNameAsync(userName);
            if (user != default)
            {
                await userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(days));
                await userManager.UpdateSecurityStampAsync(user);
                logger.LogInformation("用户 {userName} 被锁定 {days} 天", userName, days);
            }
            else
            {
                logger.LogWarning("未找到用户：{userName}", userName);
                throw new Exception($"there is not a user named {userName}.");
            }
        }

        /// <summary>
        /// 获取用户组列表
        /// </summary>
        /// <returns></returns>
        public IQueryable<IdentityRole> GetAllRoles()
        {
            logger.LogInformation("获取所有角色列表");
            return roleManager.Roles;
        }

        /// <summary>
        /// 注册用户
        /// </summary>
        /// <param name="registrationDto"></param>
        /// <returns></returns>
        public async Task<RegistrationResultDto> RegisterUserAsync(RegistrationDto registrationDto)
        {
            logger.LogInformation("用户注册申请：{UserName}", registrationDto.UserName);
            var user = new IdentityUser
            {
                UserName = registrationDto.UserName,
                Email = registrationDto.Email,
                PhoneNumber = registrationDto.PhoneNumber
            };

            var result = await userManager.CreateAsync(user, registrationDto.Password);
            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "用户注册未完成，Identity 错误代码：{ErrorCodes}",
                    string.Join(", ", result.Errors.Select(error => error.Code)));
                return new RegistrationResultDto
                {
                    Success = false,
                    Message = "注册信息无法使用，请更换用户名或邮箱后重试。"
                };
            }

            try
            {
                string? certificateTicket = null;
                if (registrationDto.ProfessionalIdentity is not null)
                {
                    certificateTicket = await CreateProfessionalIdentityRequest(registrationDto.ProfessionalIdentity, user);
                }

                await accountSecurityService.SendEmailConfirmationAsync(user);
                logger.LogInformation(
                    "用户注册成功：{UserName}，上传票据：{UploadTicket}",
                    registrationDto.UserName,
                    certificateTicket);

                return new RegistrationResultDto
                {
                    Success = true,
                    Message = "Registration successful",
                    UploadTicket = certificateTicket
                };
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "用户 {UserName} 注册后的初始化步骤失败，正在回滚新建账号。",
                    registrationDto.UserName);
                await RollbackFailedRegistrationAsync(user);
                throw;
            }
        }

        private async Task RollbackFailedRegistrationAsync(IdentityUser user)
        {
            try
            {
                foreach (var entry in dbContext.ChangeTracker
                    .Entries<ProfessionalCertificationRequest>()
                    .Where(entry => entry.Entity.UserId == user.Id))
                {
                    entry.State = EntityState.Detached;
                }

                await dbContext.ProfessionalCertificationRequests
                    .Where(request => request.UserId == user.Id)
                    .ExecuteDeleteAsync();
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "回滚用户 {UserId} 的专业认证请求失败。", user.Id);
            }

            try
            {
                var deleteResult = await userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    logger.LogCritical(
                        "回滚用户 {UserId} 失败：{Errors}",
                        user.Id,
                        string.Join(", ", deleteResult.Errors.Select(error => error.Description)));
                }
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "回滚用户 {UserId} 时发生异常。", user.Id);
            }
        }

        /// <summary>
        /// 创建专业身份认证请求
        /// </summary>
        /// <param name="professionalIdentityDto"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<string> CreateProfessionalIdentityRequest(
            ProfessionalIdentityDto professionalIdentityDto,
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default)
        {
            if (user.Identity?.IsAuthenticated is true && user.Identity.Name is not null)
            {
                var userIdentiy = await userManager.FindByNameAsync(user.Identity.Name);
                return userIdentiy == null
                    ? throw new Exception("用户未找到")
                    : await CreateProfessionalIdentityRequest(professionalIdentityDto, userIdentiy, cancellationToken);
            }
            else
            {
                throw new Exception("用户未登录");
            }
        }

        /// <summary>
        /// 创建专业身份认证请求
        /// </summary>
        /// <param name="professionalIdentityDto"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task<string> CreateProfessionalIdentityRequest(
            ProfessionalIdentityDto professionalIdentityDto,
            IdentityUser user,
            CancellationToken cancellationToken = default)
        {
            var certificateTicket = Guid.NewGuid();
            var professionalIdentity = new ProfessionalCertificationRequest
            {
                //创建这个专业身份认证请求记录
                UserId = user.Id,
                RequestTime = DateTime.UtcNow,
                IdentityType = professionalIdentityDto.IdentityType,
                InstitutionName = professionalIdentityDto.InstitutionName,
                Status = RequestStatus.Pending,
                CertificateTicket = certificateTicket
            };
            dbContext.ProfessionalCertificationRequests.Add(professionalIdentity);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("创建专业身份认证请求：{UserId}，票据：{CertificateTicket}", user.Id, certificateTicket);
            return certificateTicket.ToString();
        }

        /// <summary>
        /// 验证上传票据
        /// </summary>
        /// <param name="uploadTicket"></param>
        /// <returns></returns>
        public async Task<bool> ValidateUploadTicket(
            Guid uploadTicket,
            CancellationToken cancellationToken = default)
        {
            var isValid = await dbContext.ProfessionalCertificationRequests
                .AsNoTracking()
                .AnyAsync(
                    request => request.CertificateTicket == uploadTicket && request.Status == RequestStatus.Pending,
                    cancellationToken);
            logger.LogInformation("验证上传票据：{UploadTicket}，结果：{IsValid}", uploadTicket, isValid);
            return isValid;
        }

    }
}
