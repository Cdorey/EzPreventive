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

        /// <summary>
        /// 获取最新的登录页面公告。
        /// </summary>
        /// <param name="cancellationToken">用于取消数据库查询的令牌。</param>
        /// <returns>找到时返回公告，否则返回 404。</returns>
        [HttpGet]
        public Task<IActionResult> CoverLetter(CancellationToken cancellationToken) =>
            GetLatestNoticeAsync(NoticeKind.PreLoginAnnouncement, cancellationToken);

        /// <summary>
        /// 获取最新的登录后公告。
        /// </summary>
        /// <param name="cancellationToken">用于取消数据库查询的令牌。</param>
        /// <returns>找到时返回公告，否则返回 404。</returns>
        [HttpGet]
        public Task<IActionResult> Notice(CancellationToken cancellationToken) =>
            GetLatestNoticeAsync(NoticeKind.PostLoginAnnouncement, cancellationToken);

        /// <summary>
        /// 获取最新的用户许可协议。
        /// </summary>
        /// <param name="cancellationToken">用于取消数据库查询的令牌。</param>
        /// <returns>找到时返回用户许可协议，否则返回 404。</returns>
        [HttpGet]
        public Task<IActionResult> UserAgreement(CancellationToken cancellationToken) =>
            GetLatestNoticeAsync(NoticeKind.UserAgreement, cancellationToken);

        /// <summary>
        /// 获取最新的隐私条款。
        /// </summary>
        /// <param name="cancellationToken">用于取消数据库查询的令牌。</param>
        /// <returns>找到时返回隐私条款，否则返回 404。</returns>
        [HttpGet]
        public Task<IActionResult> PrivacyPolicy(CancellationToken cancellationToken) =>
            GetLatestNoticeAsync(NoticeKind.PrivacyPolicy, cancellationToken);

        /// <summary>
        /// 获取指定类别中创建时间最新的内容。
        /// </summary>
        private async Task<IActionResult> GetLatestNoticeAsync(
            NoticeKind kind,
            CancellationToken cancellationToken)
        {
            var notice = await db.Notices
                .AsNoTracking()
                .Where(item => item.Kind == kind)
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
