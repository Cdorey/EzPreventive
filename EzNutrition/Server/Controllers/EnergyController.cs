using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class EnergyController : ControllerBase
    {

        private readonly EnergyRepository _energyRepository;

        [HttpGet("EERs/{gender}/{age}")]
        public IActionResult GetEERs(string gender, decimal age)
        {
            if (string.IsNullOrEmpty(gender) || age < 0)
            {
                return BadRequest("Invalid gender or age.");
            }

            var eerResults = _energyRepository.GetEERsByPersonalInfo(age, gender);

            if (eerResults == null || !eerResults.Any())
            {
                return NotFound("No EER results found.");
            }

            return Ok(eerResults);
        }

        public EnergyController(EzNutritionDbContext ezNutritionDbContext)
        {
            _energyRepository = new(ezNutritionDbContext);
        }
    }
}