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

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {

            try
            {
                var user = _context.Users.FirstOrDefault(x => x.UserName == username);
                if (user != default && (await _signInManager.PasswordSignInAsync(user, password, false, false)).Succeeded)
                {
                    return Ok(_jwtService.GenerateJwtToken(user));
                }
                else
                {
                    return BadRequest();
                }
            }
            catch
            {
                return BadRequest();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetToken()
        {
            //await _userManager.CreateAsync(new IdentityUser { UserName = "EpiMan" }, "I_am_epiman");
            //await _userManager.AddToRoleAsync(await _userManager.FindByNameAsync("EpiMan"), "EpiMan");
            var x = await _jwtService.GenerateJwtToken("Test_User");
            //await _roleManager.CreateAsync(new IdentityRole { Name = "Admin" });
            //await _roleManager.CreateAsync(new IdentityRole { Name = "Physician" });
            //await _roleManager.CreateAsync(new IdentityRole { Name = "EpiMan" });
            //await _roleManager.CreateAsync(new IdentityRole { Name = "Student" });
            return Ok(x);
        }

        [Authorize(Policy = PolicyList.Prescription)]
        [HttpGet("Test")]
        public IActionResult TestToken()
        {
            return Ok(User.FindFirstValue(ClaimTypes.Name));
        }

        public AuthController(JwtService jwtService, ApplicationDbContext dbContext, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<IdentityUser> signInManager)
        {
            _jwtService = jwtService;
            _context = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
        }
    }
}