using EzNutrition.Server.Data;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class SystemInfoController(IConfiguration configuration, ApplicationDbContext db) : ControllerBase
    {
        [HttpGet]
        public IActionResult CaseNumber()
        {
            var caseNumber = configuration.GetSection("CaseNumber").Value ?? "备案号缺失";
            return Ok(caseNumber);
        }

        [HttpGet]
        public IActionResult CoverLetter()
        {
            IOrderedQueryable<Notice> x = from notice in db.Notices
                                          where notice.IsCoverLetter
                                          orderby notice.CreateTime descending
                                          select notice;
            Notice? letter = x.FirstOrDefault();

            return letter is not null ? Ok(letter) : BadRequest();
        }

        [HttpGet]
        public IActionResult Notice()
        {
            IOrderedQueryable<Notice> x = from notice in db.Notices
                                          where !notice.IsCoverLetter
                                          orderby notice.CreateTime descending
                                          select notice;
            Notice? letter = x.FirstOrDefault();

            return letter is not null ? Ok(letter) : BadRequest();
        }
    }

}