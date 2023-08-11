using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PrescriptionController : ControllerBase
    {
        private readonly DiseaseRepository _diseaseRepository;
        private readonly AdviceRepository _adviceRepository;

        [HttpGet("diseases")]
        public IActionResult GetDiseaseList()
        {
            var diseases = _diseaseRepository.GetDiseases();
            return Ok(diseases);
        }

        [HttpPost("advices")]
        public IActionResult GetAdvicesForDiseases([FromBody] List<int> diseaseIDs)
        {
            if (diseaseIDs == null || diseaseIDs.Count == 0)
            {
                return BadRequest("Disease IDs must be provided.");
            }

            var advices = _adviceRepository.GetAdviceByDiseaseID(diseaseIDs);
            return Ok(advices);
        }
        public PrescriptionController(EzNutritionDbContext ezNutritionDbContext)
        {
            _diseaseRepository = new(ezNutritionDbContext);
            _adviceRepository = new(ezNutritionDbContext);
        }
    }

}