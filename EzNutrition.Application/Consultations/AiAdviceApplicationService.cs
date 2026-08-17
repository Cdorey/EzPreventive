using EzNutrition.Application.Ports;
using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.DTO.PromptDto;
using System.Runtime.CompilerServices;
using DomainDietaryRecallSurvey = EzNutrition.Domain.Dietary.DietaryRecallSurvey;
using PromptDietaryRecallSurvey = EzNutrition.Shared.Data.DTO.PromptDto.DietaryRecallSurvey;

namespace EzNutrition.Application.Consultations;

/// <summary>
/// Coordinates prompt preparation and AI advice state without depending on a UI or host transport.
/// </summary>
public sealed class AiAdviceApplicationService(IAiAdviceGateway gateway)
{
    /// <summary>Gets information about the AI capability supplied by the current host.</summary>
    public Task<EnvironmentDto?> GetEnvironmentAsync(CancellationToken cancellationToken = default) =>
        gateway.GetEnvironmentAsync(cancellationToken);

    /// <summary>
    /// Builds the data disclosure that can later be reviewed and submitted for generation.
    /// </summary>
    /// <returns><see langword="true"/> when all required consultation data was available.</returns>
    public bool PreparePrompt(ConsultationWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        lock (workspace.AiAdviceSyncRoot)
        {
            EnsureNotGenerating(workspace);
            if (workspace.CurrentEnergyCalculator is null
                || workspace.SubjectiveObjectiveAssessmentPlanInformation is null)
            {
                return false;
            }

            var dietaryRecallSurvey = workspace.DietaryRecallSurvey is { NutrientAssessments.Count: > 0 } survey
                ? CreateDietaryRecallProjection(survey)
                : null;

            var prompt = new AiAdviceRequestDto
            {
                PatientInfo = new PatientInfo
                {
                    Gender = workspace.Client.Gender,
                    Age = workspace.Client.Age?.ToReferenceYears()
                        ?? throw new InvalidOperationException("Age is required before preparing advice."),
                    BMI = workspace.CurrentEnergyCalculator.BMI,
                    PAL = workspace.CurrentEnergyCalculator.PAL,
                    Height = workspace.Client.Height,
                    Weight = workspace.Client.Weight,
                    TotalBalanceEnergyViaCalculation = workspace.CurrentEnergyCalculator.Energy,
                    SpecialPhysiologicalPeriod = workspace.Client.SpecialPhysiologicalPeriod,
                },
                ClinicalInfo = new ClinicalInfo
                {
                    Subjective = workspace.SubjectiveObjectiveAssessmentPlanInformation.Subjective,
                    Objective = workspace.SubjectiveObjectiveAssessmentPlanInformation.Objective,
                    Assessment = workspace.SubjectiveObjectiveAssessmentPlanInformation.Assessment,
                    Plan = workspace.SubjectiveObjectiveAssessmentPlanInformation.Plan,
                },
                DietaryRecallSurvey = dietaryRecallSurvey
            };

            workspace.AdvicePrompt = prompt;
            ResetAdvice(workspace);
            return true;
        }
    }

    private static PromptDietaryRecallSurvey CreateDietaryRecallProjection(
        DomainDietaryRecallSurvey survey) => new()
        {
            Foods = survey.EntryCalculations
                .Select(entry => new DietaryRecallFoodItem(
                    entry.FoodName,
                    MapMeal(entry.MealOccasion),
                    entry.EdibleWeight,
                    "g"))
                .ToArray(),
            Nutrients = survey.NutrientAssessments
                .Select(CreateNutrientProjection)
                .ToArray()
        };

    private static DietaryNutrientIntake CreateNutrientProjection(
        DietaryNutrientAssessment assessment)
    {
        var mealEnergyShares = assessment.MealEnergies
            .Where(meal => meal.Energy > 0m)
            .Select(meal => new DietaryMealEnergyShare(
                MapMeal(meal.MealOccasion),
                meal.Energy,
                meal.PercentageOfTotalEnergy))
            .ToArray();
        var topFoodSources = assessment.FoodContributions
            .OrderByDescending(source => source.Value)
            .Take(3)
            .Select(source => new DietaryFoodSource(
                source.FoodName,
                source.Value,
                source.Unit))
            .ToArray();

        return new DietaryNutrientIntake(
            assessment.FriendlyName,
            assessment.Value,
            assessment.Unit,
            MapReferenceComparison(assessment.ReferenceStatus),
            new[] { assessment.LowerReference, assessment.UpperReference }
                .OfType<DietaryNutrientReference>()
                .Concat(assessment.ContextReferences)
                .Distinct()
                .Select(reference => new DietaryReferenceTarget(
                    reference.Type.ToString().Replace('_', '-'),
                    reference.Value,
                    reference.Unit))
                .ToArray(),
            mealEnergyShares.Length == 0 ? null : mealEnergyShares,
            topFoodSources.Length == 0 ? null : topFoodSources);
    }

    private static DietaryReferenceComparison MapReferenceComparison(
        DietaryReferenceStatus status) => status switch
        {
            DietaryReferenceStatus.NotEstablished => DietaryReferenceComparison.NotEstablished,
            DietaryReferenceStatus.WithinRange => DietaryReferenceComparison.WithinReference,
            DietaryReferenceStatus.BelowRange => DietaryReferenceComparison.BelowReference,
            DietaryReferenceStatus.AboveRange => DietaryReferenceComparison.AboveReference,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    private static DietaryMealOccasion MapMeal(MealOccasion meal) => meal switch
    {
        MealOccasion.Breakfast => DietaryMealOccasion.Breakfast,
        MealOccasion.MorningSnack => DietaryMealOccasion.MorningSnack,
        MealOccasion.Lunch => DietaryMealOccasion.Lunch,
        MealOccasion.AfternoonSnack => DietaryMealOccasion.AfternoonSnack,
        MealOccasion.Dinner => DietaryMealOccasion.Dinner,
        MealOccasion.LateNightSnack => DietaryMealOccasion.LateNightSnack,
        _ => throw new ArgumentOutOfRangeException(nameof(meal), meal, null)
    };

    /// <summary>Discards the prepared disclosure and resets generated advice.</summary>
    public void DiscardPreparedAdvice(ConsultationWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        lock (workspace.AiAdviceSyncRoot)
        {
            EnsureNotGenerating(workspace);
            workspace.AdvicePrompt = null;
            ResetAdvice(workspace);
        }
    }

    /// <summary>Clears generated output while preserving the prepared disclosure.</summary>
    public void ResetForPreview(ConsultationWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        lock (workspace.AiAdviceSyncRoot)
        {
            EnsureNotGenerating(workspace);
            ResetAdvice(workspace);
        }
    }

    /// <summary>
    /// Synchronously finalizes and invalidates an active attempt before its host call is cancelled.
    /// Late updates from that attempt can no longer mutate the workspace.
    /// </summary>
    public bool InterruptGeneration(ConsultationWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        lock (workspace.AiAdviceSyncRoot)
        {
            if (workspace.AiGeneratedAdvice is not { Sending: true } advice
                || advice.GenerationStatus != AiAdviceGenerationStatus.Generating)
            {
                return false;
            }

            FinishAttempt(advice, AiAdviceGenerationStatus.Incomplete, isReady: false);
            return true;
        }
    }

    /// <summary>
    /// Generates and applies advice to the supplied workspace while exposing only semantic
    /// progress updates to the presentation layer.
    /// </summary>
    public async IAsyncEnumerable<AiAdviceGatewayUpdate> GenerateAsync(
        ConsultationWorkspace workspace,
        EnvironmentDto? environment,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        AiAdviceRequestDto prompt;
        AiGeneratedAdvice advice;
        var attemptId = Guid.NewGuid();
        lock (workspace.AiAdviceSyncRoot)
        {
            prompt = workspace.AdvicePrompt
                ?? throw new InvalidOperationException("AI advice data must be prepared before generation.");
            advice = workspace.AiGeneratedAdvice ??= new AiGeneratedAdvice();
            if (advice.Sending)
            {
                throw new InvalidOperationException("AI advice generation is already in progress for this workspace.");
            }

            advice.GenerationAttemptId = attemptId;
            advice.Sending = true;
            advice.IsReady = false;
            advice.ReasoningContent = string.Empty;
            advice.Content = string.Empty;
            advice.GenerationStatus = AiAdviceGenerationStatus.Generating;
            advice.RequestedAt = DateTimeOffset.UtcNow;
            advice.CompletedAt = null;
            advice.Environment = environment;
        }

        try
        {
            await using (var updates = gateway
                .GenerateAsync(prompt, cancellationToken)
                .GetAsyncEnumerator(cancellationToken))
            {
                while (true)
                {
                    if (!IsCurrentAttempt(workspace, advice, attemptId))
                    {
                        yield break;
                    }

                    bool hasUpdate;
                    try
                    {
                        hasUpdate = await updates.MoveNextAsync();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        TryFinishAttempt(
                            workspace,
                            advice,
                            attemptId,
                            AiAdviceGenerationStatus.Incomplete,
                            isReady: false);
                        throw;
                    }
                    catch
                    {
                        TryFinishAttempt(
                            workspace,
                            advice,
                            attemptId,
                            AiAdviceGenerationStatus.Failed,
                            isReady: false);
                        throw;
                    }

                    if (!hasUpdate)
                    {
                        break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    var update = updates.Current;
                    if (!TryApplyUpdate(workspace, advice, attemptId, update))
                    {
                        yield break;
                    }

                    yield return update;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!TryHasRecommendation(workspace, advice, attemptId, out var hasRecommendation))
            {
                yield break;
            }

            if (!hasRecommendation)
            {
                TryFinishAttempt(
                    workspace,
                    advice,
                    attemptId,
                    AiAdviceGenerationStatus.Failed,
                    isReady: false);
                throw new AiAdviceProtocolException(
                    "The AI stream completed without recommendation content.");
            }

            _ = TryFinishAttempt(
                workspace,
                advice,
                attemptId,
                AiAdviceGenerationStatus.Completed,
                isReady: true);
        }
        finally
        {
            _ = TryFinishAttempt(
                workspace,
                advice,
                attemptId,
                AiAdviceGenerationStatus.Incomplete,
                isReady: false);
        }
    }

    private static void EnsureNotGenerating(ConsultationWorkspace workspace)
    {
        if (workspace.AiGeneratedAdvice?.Sending == true)
        {
            throw new InvalidOperationException("AI advice generation is still in progress.");
        }
    }

    private static void ResetAdvice(ConsultationWorkspace workspace)
    {
        var environment = workspace.AiGeneratedAdvice?.Environment;
        workspace.AiGeneratedAdvice = new AiGeneratedAdvice
        {
            Environment = environment
        };
    }

    private static bool IsCurrentAttempt(
        ConsultationWorkspace workspace,
        AiGeneratedAdvice advice,
        Guid attemptId)
    {
        lock (workspace.AiAdviceSyncRoot)
        {
            return IsCurrentAttemptCore(workspace, advice, attemptId);
        }
    }

    private static bool TryApplyUpdate(
        ConsultationWorkspace workspace,
        AiGeneratedAdvice advice,
        Guid attemptId,
        AiAdviceGatewayUpdate update)
    {
        lock (workspace.AiAdviceSyncRoot)
        {
            if (!IsCurrentAttemptCore(workspace, advice, attemptId))
            {
                return false;
            }

            switch (update.Kind)
            {
                case AiAdviceGatewayUpdateKind.Accepted:
                    break;
                case AiAdviceGatewayUpdateKind.Reasoning:
                    advice.ReasoningContent += update.Content;
                    break;
                case AiAdviceGatewayUpdateKind.Recommendation:
                    advice.Content += update.Content;
                    break;
                default:
                    FinishAttempt(advice, AiAdviceGenerationStatus.Failed, isReady: false);
                    throw new AiAdviceProtocolException("The AI host returned an unknown generation update.");
            }

            return true;
        }
    }

    private static bool TryHasRecommendation(
        ConsultationWorkspace workspace,
        AiGeneratedAdvice advice,
        Guid attemptId,
        out bool hasRecommendation)
    {
        lock (workspace.AiAdviceSyncRoot)
        {
            if (!IsCurrentAttemptCore(workspace, advice, attemptId))
            {
                hasRecommendation = false;
                return false;
            }

            hasRecommendation = !string.IsNullOrWhiteSpace(advice.Content);
            return true;
        }
    }

    private static bool TryFinishAttempt(
        ConsultationWorkspace workspace,
        AiGeneratedAdvice advice,
        Guid attemptId,
        AiAdviceGenerationStatus status,
        bool isReady)
    {
        lock (workspace.AiAdviceSyncRoot)
        {
            if (!IsCurrentAttemptCore(workspace, advice, attemptId))
            {
                return false;
            }

            FinishAttempt(advice, status, isReady);
            return true;
        }
    }

    private static bool IsCurrentAttemptCore(
        ConsultationWorkspace workspace,
        AiGeneratedAdvice advice,
        Guid attemptId) =>
        ReferenceEquals(workspace.AiGeneratedAdvice, advice)
        && advice.Sending
        && advice.GenerationStatus == AiAdviceGenerationStatus.Generating
        && advice.GenerationAttemptId == attemptId;

    private static void FinishAttempt(
        AiGeneratedAdvice advice,
        AiAdviceGenerationStatus status,
        bool isReady)
    {
        advice.GenerationAttemptId = null;
        advice.IsReady = isReady;
        advice.Sending = false;
        advice.GenerationStatus = status;
        advice.CompletedAt = DateTimeOffset.UtcNow;
    }
}
