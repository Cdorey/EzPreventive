namespace EzNutrition.Server.Policies
{
    [AttributeUsage(AttributeTargets.Field)]
    public class PolicyDefinedAttribute : Attribute
    {
        public PolicyType PolicyType { get; set; }
        public PolicyDefinedAttribute(PolicyType policyType)
        {
            PolicyType = policyType;
        }
    }
}
