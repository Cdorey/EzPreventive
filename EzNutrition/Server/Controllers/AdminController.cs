using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Server.Policies;
using EzNutrition.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(Policy = PolicyList.Admin)]
    public class AdminController : ControllerBase
    {
        private readonly JwtService _jwtService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<AuthController> _logger;

        private readonly AuthManagerRepository _repository;

        [HttpPost("Role/add")]
        public async Task<IActionResult> AddRole([FromForm] string role)
        {
            var x = await _roleManager.CreateAsync(new IdentityRole { Name = role });
            return Ok(x);
        }

        [HttpPost("Role/add")]
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
        public AdminController(JwtService jwtService, ApplicationDbContext dbContext, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<IdentityUser> signInManager, ILogger<AuthController> logger, AuthManagerRepository authManagerRepository)
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