using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Services;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(Policy = PolicyList.Admin)]
    public class AdminController(JwtService jwtService, ApplicationDbContext dbContext, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<IdentityUser> signInManager, ILogger<AuthController> logger, AuthManagerRepository authManagerRepository) : ControllerBase
    {
        [HttpPost("Role/add")]
        public async Task<IActionResult> AddRole([FromForm] string role)
        {
            var x = await roleManager.CreateAsync(new IdentityRole { Name = role });
            return Ok(x);
        }

        [HttpPost("Role/add")]
        public async Task<IActionResult> AddToRole([FromForm] string username, [FromForm] string role)
        {
            try
            {
                await authManagerRepository.AddToRoleAsync(username, role);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }

        }

        [HttpGet("User")]
        [HttpGet("User/{role?}")]
        public async Task<IActionResult> GetUsers(string? role = default)
        {
            try
            {
                var x = await authManagerRepository.GetAllUsers(role);
                return Ok(x);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }

}