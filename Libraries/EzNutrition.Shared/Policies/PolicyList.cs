using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace EzNutrition.Shared.Policies
{
    public static class PolicyList
    {
        [PolicyDefined(PolicyType.Permission)]
        public const string Prescription = "PrescriptionPermission";

        [PolicyDefined(PolicyType.Permission)]
        public const string AdjustModel = "AdjustModelPermission";

        /// <summary>允许审核并签发正式报告；由管理员向经核验的医师授予。</summary>
        [PolicyDefined(PolicyType.Permission)]
        public const string IssueReport = "IssueReportPermission";

        /// <summary>允许输出评估稿或打印已签发报告原件。</summary>
        [PolicyDefined(PolicyType.Permission)]
        public const string PrintReport = "PrintReportPermission";

        [PolicyDefined(PolicyType.Role)]
        public const string Admin = "AdminRole";

        public static void RegisterPolicies(AuthorizationOptions options)
        {
            var policyListType = typeof(PolicyList);
            var policies = from policy in typeof(PolicyList).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                           where policy.GetCustomAttribute<PolicyDefinedAttribute>() != null
                           select policy;

            foreach (var policy in policies)
            {
                options.AddPolicy(policy?.GetValue(null)?.ToString() ?? throw new NullReferenceException(), configurePolicy =>
                {
                    var policyType = policy.GetCustomAttribute<PolicyDefinedAttribute>()?.PolicyType;
                    switch (policyType)
                    {
                        case PolicyType.Role:
                            configurePolicy.RequireRole(policy.Name);
                            break;
                        case PolicyType.Permission:
                            configurePolicy.RequireClaim(nameof(PolicyType.Permission), policy.Name);
                            break;
                        default:
                            break;
                    }
                });
            }
        }
    }
}
