using EzNutrition.Server.Data.Repositories;
using EzNutrition.Shared.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

[ApiController]
[Route("[controller]/[action]")]
[Authorize(Policy = PolicyList.Admin)]
public class AdminController(RoleManager<IdentityRole> roleManager, ILogger<AdminController> logger, AuthManagerRepository authManagerRepository) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AddRole([FromForm][Required] string role)
    {
        try
        {
            var result = await roleManager.CreateAsync(new IdentityRole { Name = role });
            return result.Succeeded ? Ok(new { message = "Role created successfully" }) : BadRequest(result.Errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AddRole");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddToRole([FromForm][Required] string username, [FromForm][Required] string role)
    {
        try
        {
            await authManagerRepository.AddToRoleAsync(username, role);
            return Ok(new { message = "User added to role successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AddToRole");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{role?}")]
    public async Task<IActionResult> GetUsers(string? role = default)
    {
        try
        {
            var users = await authManagerRepository.GetAllUsers(role);
            return Ok(users);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetUsers");
            return BadRequest(ex.Message);
        }
    }
}