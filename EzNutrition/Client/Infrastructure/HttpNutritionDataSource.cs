using EzNutrition.Application.Ports;
using EzNutrition.Shared.Data.Entities;
using System.Net.Http.Json;
using System.Text.Json;

namespace EzNutrition.Client.Infrastructure;

/// <summary>
/// 通过 EzNutrition 后端 API 读取营养参考数据。
/// </summary>
public sealed class HttpNutritionDataSource(IHttpClientFactory httpClientFactory) :
    IEnergyReferenceDataSource,
    IDietaryReferenceIntakeDataSource,
    IFoodCompositionDataSource
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient("Authorize");

    /// <inheritdoc />
    public Task<IReadOnlyList<EER>> GetEnergyReferencesAsync(
        NutritionSubjectQuery subject,
        CancellationToken cancellationToken = default) => PostListAsync<EER>(
            $"Energy/EERs/{Uri.EscapeDataString(subject.Gender)}/{subject.Age}",
            [subject.SpecialPhysiologicalPeriod],
            "能量参考数据加载失败。",
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<DietaryReferenceIntakeValue>> GetDietaryReferenceIntakesAsync(
        NutritionSubjectQuery subject,
        CancellationToken cancellationToken = default) => PostListAsync<DietaryReferenceIntakeValue>(
            $"Energy/DRIs/{Uri.EscapeDataString(subject.Gender)}/{subject.Age}",
            [subject.SpecialPhysiologicalPeriod],
            "膳食参考摄入量加载失败。",
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Food>> GetFoodsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<Food>(
            "FoodComposition/Foods",
            "食物目录加载失败。",
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Nutrient>> GetNutrientsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<Nutrient>(
            "FoodComposition/Nutrients",
            "营养素目录加载失败。",
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<FoodNutrientValue>> GetFoodCompositionAsync(
        string friendlyCode,
        CancellationToken cancellationToken = default) => GetListAsync<FoodNutrientValue>(
            $"FoodComposition/CompositionData?friendlyCode={Uri.EscapeDataString(friendlyCode)}",
            "食物营养成分加载失败，请稍后重试。",
            cancellationToken);

    private async Task<IReadOnlyList<T>> GetListAsync<T>(
        string requestUri,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await ReadListAsync<T>(response, errorMessage, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsTransportOrPayloadFailure(ex))
        {
            throw new NutritionDataAccessException(errorMessage, ex);
        }
    }

    private async Task<IReadOnlyList<T>> PostListAsync<T>(
        string requestUri,
        IReadOnlyList<string> physiologicalPeriods,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                requestUri,
                physiologicalPeriods,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await ReadListAsync<T>(response, errorMessage, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsTransportOrPayloadFailure(ex))
        {
            throw new NutritionDataAccessException(errorMessage, ex);
        }
    }

    private static async Task<IReadOnlyList<T>> ReadListAsync<T>(
        HttpResponseMessage response,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var values = await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken);
        return values ?? throw new NutritionDataAccessException(errorMessage);
    }

    private static bool IsTransportOrPayloadFailure(Exception exception) => exception is
        HttpRequestException or
        JsonException or
        NotSupportedException or
        InvalidDataException or
        TaskCanceledException;
}
