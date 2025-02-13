using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController(ILogger<AuthController> logger, AuthManagerRepository authManagerRepository) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
        {
            try
            {
                return Ok(await authManagerRepository.Login(username, password));
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost("Regist/{role}")]
        public async Task<IActionResult> Register([FromForm] string username, [FromForm] string password, string role)
        {
            try
            {
                await authManagerRepository.Register(username, password);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet("Profile")]
        public IActionResult GetProfile()
        {
            return Ok(User.FindFirstValue(ClaimTypes.Name));
        }
    }
}