namespace EzNutrition.Shared.Policies
{
    [AttributeUsage(AttributeTargets.Field)]
    public class PolicyDefinedAttribute(PolicyType policyType) : Attribute
    {
        public PolicyType PolicyType { get; } = policyType;
    }
}
