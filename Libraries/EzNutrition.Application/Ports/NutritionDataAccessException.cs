namespace EzNutrition.Application.Ports;

/// <summary>指定营养参考数据访问失败的类别。</summary>
public enum NutritionDataAccessFailureKind
{
    /// <summary>数据源暂时不可用，或响应无法读取。</summary>
    Unavailable = 0,

    /// <summary>数据源中没有与查询条件匹配的记录。</summary>
    NotFound = 1
}

/// <summary>
/// 表示营养参考数据无法从当前数据源取得。
/// </summary>
public sealed class NutritionDataAccessException : Exception
{
    /// <summary>获取数据访问失败的类别。</summary>
    public NutritionDataAccessFailureKind FailureKind { get; }

    /// <summary>初始化营养参考数据访问异常。</summary>
    public NutritionDataAccessException(string message)
        : this(message, NutritionDataAccessFailureKind.Unavailable)
    {
    }

    /// <summary>使用指定失败类别初始化营养参考数据访问异常。</summary>
    public NutritionDataAccessException(string message, NutritionDataAccessFailureKind failureKind)
        : base(message)
    {
        FailureKind = failureKind;
    }

    /// <summary>使用内部异常初始化营养参考数据访问异常。</summary>
    public NutritionDataAccessException(string message, Exception innerException)
        : this(message, innerException, NutritionDataAccessFailureKind.Unavailable)
    {
    }

    /// <summary>使用内部异常及指定失败类别初始化营养参考数据访问异常。</summary>
    public NutritionDataAccessException(
        string message,
        Exception innerException,
        NutritionDataAccessFailureKind failureKind)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }
}
