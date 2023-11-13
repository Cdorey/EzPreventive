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
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;

        private readonly AuthManagerRepository _repository;

        [HttpPost]
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
        {
            try
            {
                return Ok(await _repository.Login(username, password));
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
                await _repository.Register(username, password);
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

        public AuthController(ILogger<AuthController> logger, AuthManagerRepository authManagerRepository)
        {
            _logger = logger;
            _repository = authManagerRepository;
        }
    }
}