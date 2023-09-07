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
        public IActionResult Login(string username, string passwordHash)
        {

            try
            {
                var user = _context.Users.FirstOrDefault(x => x.UserName == username && x.PasswordHash == passwordHash);
                if (user != default)
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
            throw new NotImplementedException();
            //await _roleManager.RemoveClaimAsync(_roleManager.Roles.First(x => x.Name == "Admin"), new Claim(PolicyType.Permission.ToString(), nameof(PolicyList.Prescription)));
            //await _userManager.AddToRoleAsync(_userManager.Users.First(x => x.UserName == "Test_User"), _roleManager.Roles.First(x => x.Name == "Admin").Name);
            //var x = await _jwtService.GenerateJwtToken(_userManager.Users.First(x => x.UserName == "Test_User"));
            //var x = await _userManager.DeleteAsync(_userManager.Users.First());
            //await _roleManager.CreateAsync(new IdentityRole { Name = "Admin" });
            //await _roleManager.CreateAsync(new IdentityRole { Name = "Physician" });
            //await _roleManager.CreateAsync(new IdentityRole { Name = "EpiMan" });
            //var x = await _roleManager.CreateAsync(new IdentityRole { Name = "Student" });
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