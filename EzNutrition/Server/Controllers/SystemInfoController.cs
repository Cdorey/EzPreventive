using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class SystemInfoController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        [HttpGet]
        public IActionResult CaseNumber()
        {
            var caseNumber = _configuration.GetSection("CaseNumber").Value ?? "备案号缺失";
            return Ok(caseNumber);
        }

        public SystemInfoController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
    }

}