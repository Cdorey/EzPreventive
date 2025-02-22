using EzNutrition.Server.Controllers;
using EzNutrition.Server.Data.Entities;
using EzNutrition.Server.Services;
using EzNutrition.Server.Services.Settings;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

namespace EzNutrition.Server.Data.Repositories
{
    public class AuthManagerRepository(JwtService jwtService,
                                       ApplicationDbContext dbContext,
                                       UserManager<IdentityUser> userManager,
                                       RoleManager<IdentityRole> roleManager,
                                       SignInManager<IdentityUser> signInManager,
                                       ILogger<AuthController> logger,
                                       IOptions<EmailSettings> options,
                                       IEmailSender<IdentityUser> emailSender)
    {
        /// <summary>
        /// 创建基础的Role关系，以及管理员账号
        /// </summary>
        /// <returns></returns>
        public async Task Initialize()
        {
            logger.LogInformation("初始化角色和管理员账号");

            //创建Roles
            if (await roleManager.FindByNameAsync("Admin") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Admin" });
                if (!x.Succeeded)
                {
                    logger.LogError("创建Admin角色失败");
                    throw new Exception("failed to create Admin role");
                }
            }

            if (await roleManager.FindByNameAsync("Student") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Student" });
                if (!x.Succeeded)
                {
                    logger.LogError("创建Student角色失败");
                    throw new Exception("failed to create Student role");
                }
            }

            if (await roleManager.FindByNameAsync("Teacher") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Teacher" });
                if (!x.Succeeded)
                {
                    logger.LogError("创建Teacher角色失败");
                    throw new Exception("failed to create Teacher role");
                }
            }

            if (await roleManager.FindByNameAsync("Physician") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Physician" });
                if (!x.Succeeded)
                {
                    logger.LogError("创建Physician角色失败");
                    throw new Exception("failed to create Physician role");
                }
            }

            if (await roleManager.FindByNameAsync("Epiman") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Epiman" });
                if (!x.Succeeded)
                {
                    logger.LogError("创建Epiman角色失败");
                    throw new Exception("failed to create Epiman role");
                }
            }

            var adminRole = await roleManager.FindByNameAsync("Admin");
            if (adminRole != default)
            {
                var admin = await userManager.FindByNameAsync("Admin");
                if (admin == default)
                {
                    var password = Guid.NewGuid().ToString();
                    var addUser = await userManager.CreateAsync(new IdentityUser { UserName = "Admin" }, password);
                    if (!addUser.Succeeded)
                    {
                        logger.LogError("创建Admin用户失败");
                        throw new Exception("failed to add Admin User");
                    }
                    else
                    {
                        logger.LogInformation("Admin用户创建成功，临时密码为 {password}，请立即更改", password);
                    }
                    admin = await userManager.FindByNameAsync("Admin");
                }
                var addToRole = await userManager.AddToRolesAsync(admin!, ["Admin", "Epiman"]);
                if (!addToRole.Succeeded)
                {
                    logger.LogError("为Admin用户添加角色失败");
                    throw new Exception("failed to add Admin Role for Admin User");
                }
            }
            else
            {
                logger.LogError("未找到Admin角色");
                throw new Exception("failed to Find Admin role");
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
            try
            {
                var user = dbContext.Users.FirstOrDefault(x => x.UserName == username);
                if (user != default && (await signInManager.PasswordSignInAsync(user, password, false, false)).Succeeded)
                {
                    logger.LogInformation("用户登陆成功：{user.Id}/{user.NormalizedUserName}", user.Id, user.NormalizedUserName);
                    return await jwtService.GenerateJwtToken(user);
                }
                else
                {
                    logger.LogWarning("用户登陆失败：{username}", username);
                    throw new Exception("用户名/密码不正确");
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "用户登陆失败：{username}", username);
                throw;
            }
        }

        /// <summary>
        /// 将一个用户加入用户组
        /// </summary>
        /// <param name="username"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task AddToRoleAsync(string username, string role)
        {
            var user = await userManager.FindByNameAsync(username);
            if (user != default)
            {
                var addToRole = await userManager.AddToRoleAsync(user, role);
                if (!addToRole.Succeeded)
                {
                    logger.LogError("为用户 {username} 添加角色 {role} 失败", username, role);
                    throw new Exception("failed to add user to role");
                }
                else
                {
                    logger.LogInformation("为用户 {username} 添加角色 {role} 成功", username, role);
                }
            }
            else
            {
                logger.LogWarning("未找到用户：{username}", username);
                throw new Exception($"there is not a user named {username}.");
            }
        }

        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IdentityUser>> GetAllUsers(string? role = default)
        {
            if (role != default)
            {
                logger.LogInformation("获取角色为 {role} 的用户列表", role);
                return await userManager.GetUsersInRoleAsync(role);
            }
            else
            {
                logger.LogInformation("获取所有用户列表");
                return userManager.Users;
            }
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
                logger.LogError("用户注册失败：{errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                return new RegistrationResultDto
                {
                    Success = false,
                    Message = string.Join(", ", result.Errors.Select(e => e.Description))
                };
            }

            if (registrationDto.ProfessionalIdentity != null)
            {
                var certificateTicket = await CreateProfessionalIdentityRequest(registrationDto.ProfessionalIdentity, user);

                logger.LogInformation("用户注册成功：{UserName}，上传票据：{UploadTicket}", registrationDto.UserName, certificateTicket);
                await SendEmailConfirmationAsync(user);
                return new RegistrationResultDto
                {
                    Success = true,
                    Message = "Registration successful",
                    UploadTicket = certificateTicket.ToString()
                };
            }
            else
            {
                logger.LogInformation("用户注册成功：{UserName}", registrationDto.UserName);
                await SendEmailConfirmationAsync(user);
                return new RegistrationResultDto
                {
                    Success = true,
                    Message = "Registration successful"
                };
            }
        }

        /// <summary>
        /// 私有方法：生成邮箱确认 token 并发送确认邮件
        /// </summary>
        private async Task SendEmailConfirmationAsync(IdentityUser user)
        {
            // 生成邮箱确认 Token
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

            // 对 token 进行 URL 安全编码
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // 构建确认链接。注意 ClientUrl 可以在配置文件中设置
            var confirmationLink = $"{options.Value.ClientUrl}/Auth/ConfirmEmail?userId={user.Id}&token={encodedToken}";

            // 发送确认邮件
            await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink);
        }

        /// <summary>
        /// 创建专业身份认证请求
        /// </summary>
        /// <param name="professionalIdentityDto"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<string> CreateProfessionalIdentityRequest(ProfessionalIdentityDto professionalIdentityDto, ClaimsPrincipal user)
        {
            var userIdentiy = await userManager.GetUserAsync(user);
            return userIdentiy == null
                ? throw new Exception("用户未找到")
                : await CreateProfessionalIdentityRequest(professionalIdentityDto, userIdentiy);
        }

        /// <summary>
        /// 创建专业身份认证请求
        /// </summary>
        /// <param name="professionalIdentityDto"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task<string> CreateProfessionalIdentityRequest(ProfessionalIdentityDto professionalIdentityDto, IdentityUser user)
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
            await dbContext.SaveChangesAsync();
            logger.LogInformation("创建专业身份认证请求：{UserId}，票据：{CertificateTicket}", user.Id, certificateTicket);
            return certificateTicket.ToString();
        }

        /// <summary>
        /// 验证上传票据
        /// </summary>
        /// <param name="uploadTicket"></param>
        /// <returns></returns>
        public async Task<bool> ValidateUploadTicket(string uploadTicket)
        {
            var isValid = await dbContext.ProfessionalCertificationRequests.AnyAsync(x => x.CertificateTicket.ToString() == uploadTicket);
            logger.LogInformation("验证上传票据：{UploadTicket}，结果：{IsValid}", uploadTicket, isValid);
            return isValid;
        }

        /// <summary>
        /// 验证邮箱是否可用
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<bool> CheckEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var user = await userManager.FindByEmailAsync(email);
            return user == null;
        }

        /// <summary>
        /// 邮箱确认
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<IdentityResult?> ConfirmEmailAsync(string userId, string token)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return null;
            }

            // 解码 token（如果在生成时做了编码处理）
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

            return await userManager.ConfirmEmailAsync(user, decodedToken);
        }
    }
}