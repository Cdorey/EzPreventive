namespace EzNutrition.Shared.Data.DTO
{
    public class UserDto
    {
        /// <summary>
        /// 用户的唯一标识符
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public required string UserName { get; set; }

        /// <summary>
        /// 标准化后的用户名
        /// </summary>
        public required string NormalizedUserName { get; set; }

        /// <summary>
        /// 用户的邮箱（可能为空）
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// 邮箱是否确认
        /// </summary>
        public bool EmailConfirmed { get; set; }

        /// <summary>
        /// 用户的电话号码（可能为空）
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// 电话号码是否确认
        /// </summary>
        public bool PhoneNumberConfirmed { get; set; }

        /// <summary>
        /// 是否启用了双因素认证
        /// </summary>
        public bool TwoFactorEnabled { get; set; }

        /// <summary>
        /// 是否允许用户被锁定
        /// </summary>
        public bool LockoutEnabled { get; set; }

        /// <summary>
        /// 登录失败次数（用于锁定策略等）
        /// </summary>
        public int AccessFailedCount { get; set; }
    }
}