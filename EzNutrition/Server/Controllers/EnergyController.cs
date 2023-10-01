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
        private readonly ILogger<EnergyController> _logger;

        [HttpGet("EERs/{gender}/{age}")]
        public IActionResult GetEERs(string gender, decimal age)
        {
            if (string.IsNullOrEmpty(gender) || age < 0)
            {
                _logger.LogWarning("不正确的EER参数：{gender}/{age}", gender, age); 
                return BadRequest("Invalid gender or age.");
            }

            var eerResults = _energyRepository.GetEERsByPersonalInfo(age, gender);

            if (eerResults == null || !eerResults.Any())
            {
                _logger.LogWarning("无记录的EER参数：{gender}/{age}", gender, age);
                return NotFound("No EER results found.");
            }

            return Ok(eerResults);
        }

        public EnergyController(EnergyRepository energyRepository, ILogger<EnergyController> logger)
        {
            _energyRepository = energyRepository;
            _logger = logger;
        }
    }
}