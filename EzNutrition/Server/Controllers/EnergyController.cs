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

        private readonly DietaryReferenceIntakeRepository _energyRepository;
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

        [HttpPost("DRIs/{gender}/{age}")]
        public IActionResult GetDRIs(string gender, decimal age, [FromBody] IEnumerable<string> specialPhysiologicalPeriod)
        {
            if (string.IsNullOrEmpty(gender) || age < 0)
            {
                _logger.LogWarning("不正确的EER参数：{gender}/{age}", gender, age);
                return BadRequest("Invalid gender or age.");
            }
#warning 没有正确处理特殊生理时期信息
            var driResults = _energyRepository.GetDRIsByPersonalInfo(age, gender, specialPhysiologicalPeriod);

            if (driResults == null || !driResults.Any())
            {
                _logger.LogWarning("无记录的EER参数：{gender}/{age}", gender, age);
                return NotFound("No EER results found.");
            }

            return Ok(driResults);
        }



        public EnergyController(DietaryReferenceIntakeRepository energyRepository, ILogger<EnergyController> logger)
        {
            _energyRepository = energyRepository;
            _logger = logger;
        }
    }
}