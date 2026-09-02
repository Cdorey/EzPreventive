using EzNutrition.Server.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    [Authorize]
    public class FoodCompositionController(FoodNutritionValueRepository foodNutritionValueRepository) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Foods(CancellationToken cancellationToken)
        {
            return Ok(await foodNutritionValueRepository.GetFoodsAsync(cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> Nutrients(CancellationToken cancellationToken)
        {
            return Ok(await foodNutritionValueRepository.GetNutrientsAsync(cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> CompositionData(
            [FromQuery] string friendlyCode,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(friendlyCode))
            {
                return BadRequest("friendlyCode is required.");
            }

            var res = await foodNutritionValueRepository.FoodNutritionValueByFriendlyCodeAsync(
                friendlyCode.Trim(),
                cancellationToken);
            if (res == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(res.FoodNutrientValues);
            }
        }
    }
}
