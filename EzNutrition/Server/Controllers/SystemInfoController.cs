using EzNutrition.Server.Data;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> CoverLetter(CancellationToken cancellationToken)
        {
            var letter = await db.Notices
                .AsNoTracking()
                .Where(notice => notice.IsCoverLetter)
                .OrderByDescending(notice => notice.CreateTime)
                .FirstOrDefaultAsync(cancellationToken);

            return letter is not null ? Ok(letter) : NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> Notice(CancellationToken cancellationToken)
        {
            var notice = await db.Notices
                .AsNoTracking()
                .Where(item => !item.IsCoverLetter)
                .OrderByDescending(item => item.CreateTime)
                .FirstOrDefaultAsync(cancellationToken);

            return notice is not null ? Ok(notice) : NotFound();
        }
    }

}
