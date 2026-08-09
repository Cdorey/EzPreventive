namespace EzNutrition.Application.Ports;

/// <summary>
/// 表示营养参考数据无法从当前适配器读取或解析。
/// </summary>
public sealed class NutritionDataAccessException : Exception
{
    /// <summary>初始化营养参考数据访问异常。</summary>
    public NutritionDataAccessException(string message)
        : base(message)
    {
    }

    /// <summary>使用内部异常初始化营养参考数据访问异常。</summary>
    public NutritionDataAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
