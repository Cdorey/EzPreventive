using EzNutrition.Server.Data;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class SystemInfoController(IConfiguration configuration, ApplicationDbContext db) : ControllerBase
    {
        private static readonly string? ServerVersion = ResolveServerVersion();

        /// <summary>
        /// 获取当前服务端的公开部署信息。
        /// </summary>
        [HttpGet]
        [ProducesResponseType<PublicSystemInfoDto>(StatusCodes.Status200OK)]
        public ActionResult<PublicSystemInfoDto> PublicInfo() =>
            Ok(new PublicSystemInfoDto(GetCaseNumber(), ServerVersion));

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

        private string? GetCaseNumber()
        {
            var caseNumber = configuration["CaseNumber"];
            return string.IsNullOrWhiteSpace(caseNumber) ? null : caseNumber.Trim();
        }

        private static string? ResolveServerVersion()
        {
            var version = typeof(SystemInfoController).Assembly.GetName().Version;
            return version?.ToString(4);
        }
    }

}
