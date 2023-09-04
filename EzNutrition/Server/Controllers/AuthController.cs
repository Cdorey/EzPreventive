using EzNutrition.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtService _jwtService;

        [HttpGet("Token")]
        public IActionResult GetToken()
        {
            return Ok(_jwtService.GenerateJwtToken("test"));
        }

        [Authorize]
        [HttpGet("Test")]
        public IActionResult TestToken()
        {
            return Ok(User.FindFirstValue(ClaimTypes.Name));
        }

        public AuthController(JwtService jwtService)
        {
            _jwtService = jwtService;
        }
    }
}