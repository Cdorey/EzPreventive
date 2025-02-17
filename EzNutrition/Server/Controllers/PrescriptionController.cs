using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class PrescriptionController(DiseaseRepository diseaseRepository, AdviceRepository adviceRepository) : ControllerBase
    {
        [HttpGet("diseases")]
        public IActionResult GetDiseaseList()
        {
            var diseases = diseaseRepository.GetDiseases();
            return Ok(diseases);
        }

        [HttpPost("advices")]
        public IActionResult GetAdvicesForDiseases([FromBody] List<int> diseaseIDs)
        {
            if (diseaseIDs == null || diseaseIDs.Count == 0)
            {
                return BadRequest("Disease IDs must be provided.");
            }

            var advices = adviceRepository.GetAdviceByDiseaseID(diseaseIDs);
            return Ok(advices);
        }
    }
}