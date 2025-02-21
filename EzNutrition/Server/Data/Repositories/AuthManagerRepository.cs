using EzNutrition.Server.Controllers;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EzNutrition.Server.Data.Repositories
{
    public class AuthManagerRepository(JwtService jwtService, ApplicationDbContext dbContext, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<IdentityUser> signInManager, ILogger<AuthController> logger)
    {

        /// <summary>
        /// 创建基础的Role关系，以及管理员账号
        /// </summary>
        /// <returns></returns>
        public async Task Initialize()
        {
            //创建Roles
            if (await roleManager.FindByNameAsync("Admin") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Admin" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Admin role");
            }

            if (await roleManager.FindByNameAsync("Student") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Student" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Student role");
            }

            if (await roleManager.FindByNameAsync("Teacher") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Teacher" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Teacher role");
            }

            if (await roleManager.FindByNameAsync("Physician") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Physician" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Physician role");
            }

            if (await roleManager.FindByNameAsync("Epiman") == default)
            {
                var x = await roleManager.CreateAsync(new IdentityRole { Name = "Epiman" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Epiman role");
            }

            var adminRole = await roleManager.FindByNameAsync("Admin");
            if (adminRole != default)
            {
                var x = await roleManager.AddClaimAsync(adminRole, new Claim(PolicyType.Permission.ToString(), nameof(PolicyList.Admin)));
                if (!x.Succeeded)
                    throw new Exception("failed to add Admin Permission");

                var admin = await userManager.FindByNameAsync("Admin");
                if (admin == default)
                {
                    var password = Guid.NewGuid().ToString();
                    var addUser = await userManager.CreateAsync(new IdentityUser { UserName = "Admin" }, password);
                    if (!addUser.Succeeded)
                        throw new Exception("failed to add Admin User");
                    else
                        logger.LogInformation("Admin user created, temporary password is {password}, and change it immediately.", password);
                    admin = await userManager.FindByNameAsync("Admin");
                }
                var addToRole = await userManager.AddToRolesAsync(admin!, ["Admin", "Epiman"]);
                if (!addToRole.Succeeded)
                    throw new Exception("failed to add Admin Role for Admin User");
            }
            else
            {
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
                    logger.LogInformation("用户登陆失败：{username}", username);
                    throw new Exception("用户名/密码不正确");
                }
            }
            catch (Exception e)
            {
                logger.LogInformation(e, "用户登陆失败：{username}", username);
                throw;
            }
        }

        /// <summary>
        /// 注册
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task Register(string username, string password)
        {
            logger.LogInformation("用户注册申请：{username}", username);
            var user = dbContext.Users.FirstOrDefault(x => x.UserName == username);
            if (user != default)
            {
                logger.LogInformation("用户注册失败：{username}已占用", username);
                throw new Exception($"{username} was registered");
            }
            else
            {
                var x = await userManager.CreateAsync(new IdentityUser { UserName = username }, password);
                if (!x.Succeeded)
                {
                    logger.LogInformation("用户注册失败：CreateAsync调用异常，{errors}", x.Errors.ToString());
                    throw new Exception("failed");

                }

                //无论请求什么角色，暂时只放EpiMan权限
                //var addRole = await _userManager.AddToRoleAsync(await _userManager.FindByNameAsync(username), "EpiMan");
                //if (!addRole.Succeeded)
                //{
                //    _logger.LogInformation("角色添加失败：AddToRoleAsync调用异常，{errors}", addRole.Errors.ToString());
                //}
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
                    throw new Exception("failed to add user to role");
            }
            else
            {
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
                return await userManager.GetUsersInRoleAsync(role);
            }
            else
            {
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
            await userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(days));
            await userManager.UpdateSecurityStampAsync(user);
        }

        /// <summary>
        /// 获取用户组列表
        /// </summary>
        /// <returns></returns>
        public IQueryable<IdentityRole> GetAllRoles()
        {
            return roleManager.Roles;
        }
    }
}
