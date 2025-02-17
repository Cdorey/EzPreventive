using EzNutrition.Server.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class EnergyController(DietaryReferenceIntakeRepository energyRepository, ILogger<EnergyController> logger) : ControllerBase
    {
        [HttpPost("EERs/{gender}/{age}")]
        public IActionResult GetEERs(string gender, decimal age, [FromBody] IEnumerable<string> specialPhysiologicalPeriod)
        {
            if (string.IsNullOrEmpty(gender) || age < 0)
            {
                logger.LogWarning("不正确的EER参数：{gender}/{age}", gender, age);
                return BadRequest("Invalid gender or age.");
            }

            var eerResults = energyRepository.GetEERsByPersonalInfo(age, gender, specialPhysiologicalPeriod);

            if (eerResults == null || !eerResults.Any())
            {
                logger.LogWarning("无记录的EER参数：{gender}/{age}", gender, age);
                return NotFound("No EER results found.");
            }

            return Ok(eerResults);
        }

        [HttpPost("DRIs/{gender}/{age}")]
        public IActionResult GetDRIs(string gender, decimal age, [FromBody] IEnumerable<string> specialPhysiologicalPeriod)
        {
            if (string.IsNullOrEmpty(gender) || age < 0)
            {
                logger.LogWarning("不正确的DRIs参数：{gender}/{age}", gender, age);
                return BadRequest("Invalid gender or age.");
            }
            var driResults = energyRepository.GetDRIsByPersonalInfo(age, gender, specialPhysiologicalPeriod);

            if (driResults == null || !driResults.Any())
            {
                logger.LogWarning("无记录的DRIs参数：{gender}/{age}", gender, age);
                return NotFound("No DRIs results found.");
            }

            return Ok(driResults);
        }
    }
}