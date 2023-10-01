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
                    _logger.LogWarning("用户登陆失败：{username}", username);
                    return BadRequest("用户名/密码不正确");
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "用户登陆失败：{username}", username);
                return BadRequest(e.Message);
            }
        }

        [HttpPost("{role}")]
        public async Task<IActionResult> Register([FromForm] string username, [FromForm] string password, [FromForm] string role)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        public async Task<IActionResult> GetToken()
        {
            throw new NotImplementedException();
            //await _userManager.CreateAsync(new IdentityUser { UserName = "EpiMan" }, "I_am_epiman");
            //await _userManager.AddToRoleAsync(await _userManager.FindByNameAsync("EpiMan"), "EpiMan");
            //var x = await _jwtService.GenerateJwtToken("Test_User");
            //await _roleManager.CreateAsync(new IdentityRole { Name = "Admin" });
            //await _roleManager.CreateAsync(new IdentityRole { Name = "Physician" });
            //await _roleManager.CreateAsync(new IdentityRole { Name = "EpiMan" });
            //await _roleManager.CreateAsync(new IdentityRole { Name = "Student" });
            //return Ok(x);
        }

        //[Authorize(Policy = PolicyList.Prescription)]
        [HttpGet("Test")]
        public IActionResult TestToken()
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