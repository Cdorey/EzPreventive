using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
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

        [HttpPost("Role/add")]
        [Authorize(Policy = PolicyList.Admin)]
        public async Task<IActionResult> AddRole([FromForm] string role)
        {
            var x = await _roleManager.CreateAsync(new IdentityRole { Name = role });
            return Ok(x);
        }

        [HttpPost("Role/add")]
        [Authorize(Policy = PolicyList.Admin)]
        public async Task<IActionResult> AddToRole([FromForm] string username, [FromForm] string role)
        {
            try
            {
                await _repository.AddToRoleAsync(username, role);
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

        public AuthController(JwtService jwtService, ApplicationDbContext dbContext, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<IdentityUser> signInManager, ILogger<AuthController> logger, AuthManagerRepository authManagerRepository)
        {
            _jwtService = jwtService;
            _context = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _logger = logger;
            _repository = authManagerRepository;
        }
    }
}