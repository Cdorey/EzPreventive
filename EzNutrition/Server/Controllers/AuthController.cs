using EzNutrition.Server.Data;
using EzNutrition.Server.Policies;
using EzNutrition.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtService _jwtService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<AuthController> _logger;

        [HttpPost]
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
        {

            try
            {
                var user = _context.Users.FirstOrDefault(x => x.UserName == username);
                if (user != default && (await _signInManager.PasswordSignInAsync(user, password, false, false)).Succeeded)
                {
                    _logger.LogInformation("用户登陆成功：{user.Id}/{user.NormalizedUserName}", user.Id, user.NormalizedUserName);
                    return Ok(await _jwtService.GenerateJwtToken(user));
                }
                else
                {
                    _logger.LogInformation("用户登陆失败：{username}", username);
                    return BadRequest("用户名/密码不正确");
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation(e, "用户登陆失败：{username}", username);
                return BadRequest(e.Message);
            }
        }

        [HttpPost("{role}/regist")]
        public async Task<IActionResult> Register([FromForm] string username, [FromForm] string password, string role)
        {
            _logger.LogInformation("用户注册申请：{username}", username);
            var user = _context.Users.FirstOrDefault(x => x.UserName == username);
            if (user != default)
            {
                _logger.LogInformation("用户注册失败：{username}已占用", username);
                return BadRequest($"{username} was registered");
            }
            else
            {
                var x = await _userManager.CreateAsync(new IdentityUser { UserName = username }, password);
                if (!x.Succeeded)
                {
                    _logger.LogInformation("用户注册失败：CreateAsync调用异常，{errors}", x.Errors.ToString());
                    return BadRequest("failed");

                }

                //无论请求什么角色，暂时只放EpiMan权限
                var addRole = await _userManager.AddToRoleAsync(await _userManager.FindByNameAsync(username), "EpiMan");
                if (!addRole.Succeeded)
                {
                    _logger.LogInformation("角色添加失败：AddToRoleAsync调用异常，{errors}", addRole.Errors.ToString());
                }
                return Ok();
            }
        }

        [HttpPost("{role}/add")]
        [Authorize(Policy = PolicyList.Admin)]
        public async Task<IActionResult> AddRole(string role)
        {
            var x = await _roleManager.CreateAsync(new IdentityRole { Name = role });
            return Ok(x);
        }

        //[Authorize(Policy = PolicyList.Prescription)]
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            return Ok(User.FindFirstValue(ClaimTypes.Name));
        }

        public AuthController(JwtService jwtService, ApplicationDbContext dbContext, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<IdentityUser> signInManager, ILogger<AuthController> logger)
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