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
        public IActionResult Foods()
        {
            return Ok(foodNutritionValueRepository.GetFoods());
        }

        public IActionResult Nutrients()
        {
            return Ok(foodNutritionValueRepository.GetNutrients());
        }

        public IActionResult CompositionData([FromQuery] string friendlyCode)
        {
            var res = foodNutritionValueRepository.FoodNutritionValueByFriendlyCode(friendlyCode);
            if (res == null)
            {
                return BadRequest();
            }
            else
            {
                return Ok(res.FoodNutrientValues);
            }
        }
    }
}