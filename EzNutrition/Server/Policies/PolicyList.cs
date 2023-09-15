using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace EzNutrition.Server.Policies
{
    public static class PolicyList
    {
        [PolicyDefined(PolicyType.Permission)]
        public const string Prescription = "PrescriptionPermission";

        public static void RegisterPolicies(AuthorizationOptions options)
        {
            var policyListType = typeof(PolicyList);
            var policies = from policy in typeof(PolicyList).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                           where policy.GetCustomAttribute<PolicyDefinedAttribute>() != null
                           select policy;

            foreach (var policy in policies)
            {
                options.AddPolicy(policy?.GetValue(null)?.ToString() ?? throw new NullReferenceException(), configurePolicy => { configurePolicy.RequireClaim(policy.GetCustomAttribute<PolicyDefinedAttribute>()?.PolicyType.ToString() ?? throw new NullReferenceException(), policy.Name); });
            }
        }
    }
}
