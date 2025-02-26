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
