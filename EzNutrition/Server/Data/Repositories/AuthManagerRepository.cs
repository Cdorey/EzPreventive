using EzNutrition.Server.Controllers;
using EzNutrition.Server.Policies;
using EzNutrition.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EzNutrition.Server.Data.Repositories
{
    public class AuthManagerRepository
    {
        private readonly JwtService _jwtService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// 创建基础的Role关系，以及管理员账号
        /// </summary>
        /// <returns></returns>
        public async Task Initialize()
        {
            //创建Roles
            if (await _roleManager.FindByNameAsync("Admin") == default)
            {
                var x = await _roleManager.CreateAsync(new IdentityRole { Name = "Admin" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Admin role");
            }

            if (await _roleManager.FindByNameAsync("Student") == default)
            {
                var x = await _roleManager.CreateAsync(new IdentityRole { Name = "Student" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Student role");
            }

            if (await _roleManager.FindByNameAsync("Teacher") == default)
            {
                var x = await _roleManager.CreateAsync(new IdentityRole { Name = "Teacher" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Teacher role");
            }

            if (await _roleManager.FindByNameAsync("Physician") == default)
            {
                var x = await _roleManager.CreateAsync(new IdentityRole { Name = "Physician" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Physician role");
            }

            if (await _roleManager.FindByNameAsync("Epiman") == default)
            {
                var x = await _roleManager.CreateAsync(new IdentityRole { Name = "Epiman" });
                if (!x.Succeeded)
                    throw new Exception("failed to create Epiman role");
            }

            var adminRole = await _roleManager.FindByNameAsync("Admin");
            if (adminRole != default)
            {
                var x = await _roleManager.AddClaimAsync(adminRole, new Claim(PolicyType.Permission.ToString(), nameof(PolicyList.Admin)));
                if (!x.Succeeded)
                    throw new Exception("failed to add Admin Permission");

                var admin = await _userManager.FindByNameAsync("Admin");
                if (admin == default)
                {
                    var password = Guid.NewGuid().ToString();
                    var addUser = await _userManager.CreateAsync(new IdentityUser { UserName = "Admin" }, password);
                    if (!addUser.Succeeded)
                        throw new Exception("failed to add Admin User");
                    else
                        _logger.LogInformation("Admin user created, temporary password is {password}, and change it immediately.", password);
                    admin = await _userManager.FindByNameAsync("Admin");
                }
                var addToRole = await _userManager.AddToRolesAsync(admin, new string[] { "Admin", "Epiman" });
                if (!addToRole.Succeeded)
                    throw new Exception("failed to add Admin Role for Admin User");
            }
            else
            {
                throw new Exception("failed to Find Admin role");
            }
        }

        public async Task<string> Login(string username, string password)
        {

            try
            {
                var user = _context.Users.FirstOrDefault(x => x.UserName == username);
                if (user != default && (await _signInManager.PasswordSignInAsync(user, password, false, false)).Succeeded)
                {
                    _logger.LogInformation("用户登陆成功：{user.Id}/{user.NormalizedUserName}", user.Id, user.NormalizedUserName);
                    return await _jwtService.GenerateJwtToken(user);
                }
                else
                {
                    _logger.LogInformation("用户登陆失败：{username}", username);
                    throw new Exception("用户名/密码不正确");
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation(e, "用户登陆失败：{username}", username);
                throw;
            }
        }

        public async Task Register(string username, string password)
        {
            _logger.LogInformation("用户注册申请：{username}", username);
            var user = _context.Users.FirstOrDefault(x => x.UserName == username);
            if (user != default)
            {
                _logger.LogInformation("用户注册失败：{username}已占用", username);
                throw new Exception($"{username} was registered");
            }
            else
            {
                var x = await _userManager.CreateAsync(new IdentityUser { UserName = username }, password);
                if (!x.Succeeded)
                {
                    _logger.LogInformation("用户注册失败：CreateAsync调用异常，{errors}", x.Errors.ToString());
                    throw new Exception("failed");

                }

                //无论请求什么角色，暂时只放EpiMan权限
                var addRole = await _userManager.AddToRoleAsync(await _userManager.FindByNameAsync(username), "EpiMan");
                if (!addRole.Succeeded)
                {
                    _logger.LogInformation("角色添加失败：AddToRoleAsync调用异常，{errors}", addRole.Errors.ToString());
                }
            }
        }

        public async Task AddToRoleAsync(string username, string role)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user != default)
            {
                var addToRole = await _userManager.AddToRoleAsync(user, role);
                if (!addToRole.Succeeded)
                    throw new Exception("failed to add user to role");
            }
            else
            {
                throw new Exception($"there is not a user named {username}.");
            }
        }

        public AuthManagerRepository(JwtService jwtService, ApplicationDbContext dbContext, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<IdentityUser> signInManager, ILogger<AuthController> logger)
        {
            _jwtService = jwtService;
            _context = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _logger = logger;
        }
    }
}
