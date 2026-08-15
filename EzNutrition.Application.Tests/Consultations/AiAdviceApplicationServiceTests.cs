using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.Entities;
using System.Runtime.CompilerServices;
using AdviceEnvironment = EzNutrition.Shared.Data.DTO.PromptDto.EnvironmentDto;
using AdviceRequest = EzNutrition.Shared.Data.DTO.PromptDto.AiAdviceRequestDto;

namespace EzNutrition.Application.Tests.Consultations;

public sealed class AiAdviceApplicationServiceTests
{
    [Fact]
    public void PreparePrompt_maps_consultation_data_and_dietary_flags()
    {
        var gateway = CreateGateway();
        var service = new AiAdviceApplicationService(gateway);
        var workspace = CreateReadyWorkspace();

        var prepared = service.PreparePrompt(workspace);

        Assert.True(prepared);
        var prompt = Assert.IsType<AdviceRequest>(workspace.AdvicePrompt);
        Assert.Equal("female", prompt.PatientInfo.Gender);
        Assert.Equal(35, prompt.PatientInfo.Age);
        Assert.Equal(22.04m, prompt.PatientInfo.BMI);
        Assert.Equal(1.5m, prompt.PatientInfo.PAL);
        Assert.Equal(165m, prompt.PatientInfo.Height);
        Assert.Equal(60m, prompt.PatientInfo.Weight);
        Assert.Equal(2100, prompt.PatientInfo.TotalBalanceEnergyViaCalculation);
        Assert.Equal("pregnancy", prompt.PatientInfo.SpecialPhysiologicalPeriod);

        var clinical = Assert.IsType<EzNutrition.Shared.Data.DTO.PromptDto.ClinicalInfo>(prompt.ClinicalInfo);
        Assert.Equal("subjective", clinical.Subjective);
        Assert.Equal("objective", clinical.Objective);
        Assert.Equal("assessment", clinical.Assessment);
        Assert.Equal("plan", clinical.Plan);

        var dietary = Assert.IsType<EzNutrition.Shared.Data.DTO.PromptDto.DietaryRecallSurvey>(
            prompt.DietaryRecallSurvey);
        Assert.Equal(["iron"], dietary.DeficientNutrients);
        Assert.Equal(["sodium"], dietary.ExcessiveNutrients);
        Assert.Equal(AiAdviceGenerationStatus.Prepared, workspace.AiGeneratedAdvice?.GenerationStatus);
    }

    [Fact]
    public async Task GenerateAsync_accumulates_reasoning_and_recommendation_then_completes()
    {
        var gateway = CreateGateway(
            new(AiAdviceGatewayUpdateKind.Accepted),
            new(AiAdviceGatewayUpdateKind.Reasoning, "think "),
            new(AiAdviceGatewayUpdateKind.Reasoning, "carefully"),
            new(AiAdviceGatewayUpdateKind.Recommendation, "eat "),
            new(AiAdviceGatewayUpdateKind.Recommendation, "well"));
        var service = new AiAdviceApplicationService(gateway);
        var workspace = PrepareWorkspace(service);
        var environment = new AdviceEnvironment("provider", "platform", "details");

        var updates = await DrainAsync(service.GenerateAsync(workspace, environment));

        Assert.Equal(5, updates.Count);
        Assert.Same(workspace.AdvicePrompt, gateway.LastPrompt);
        var advice = Assert.IsType<AiGeneratedAdvice>(workspace.AiGeneratedAdvice);
        Assert.Equal("think carefully", advice.ReasoningContent);
        Assert.Equal("eat well", advice.Content);
        Assert.True(advice.IsReady);
        Assert.False(advice.Sending);
        Assert.Equal(AiAdviceGenerationStatus.Completed, advice.GenerationStatus);
        Assert.NotNull(advice.RequestedAt);
        Assert.NotNull(advice.CompletedAt);
        Assert.Same(environment, advice.Environment);
    }

    [Fact]
    public async Task GenerateAsync_marks_failed_when_stream_has_no_recommendation_body()
    {
        var gateway = CreateGateway(
            new(AiAdviceGatewayUpdateKind.Accepted),
            new(AiAdviceGatewayUpdateKind.Reasoning, "reasoning only"));
        var service = new AiAdviceApplicationService(gateway);
        var workspace = PrepareWorkspace(service);

        var exception = await Assert.ThrowsAsync<AiAdviceProtocolException>(
            () => DrainAsync(service.GenerateAsync(workspace, environment: null)));

        Assert.Contains("without recommendation content", exception.Message, StringComparison.Ordinal);
        var advice = Assert.IsType<AiGeneratedAdvice>(workspace.AiGeneratedAdvice);
        Assert.Equal("reasoning only", advice.ReasoningContent);
        Assert.Empty(advice.Content);
        Assert.False(advice.IsReady);
        Assert.False(advice.Sending);
        Assert.Equal(AiAdviceGenerationStatus.Failed, advice.GenerationStatus);
        Assert.NotNull(advice.CompletedAt);
    }

    [Fact]
    public async Task GenerateAsync_propagates_provider_failure_and_marks_workspace_failed()
    {
        var expected = new AiAdviceProviderException("provider rejected request");
        var gateway = CreateFailingGateway(
            expected,
            new AiAdviceGatewayUpdate(
                AiAdviceGatewayUpdateKind.Reasoning,
                "partial reasoning"));
        var service = new AiAdviceApplicationService(gateway);
        var workspace = PrepareWorkspace(service);

        var actual = await Assert.ThrowsAsync<AiAdviceProviderException>(
            () => DrainAsync(service.GenerateAsync(workspace, environment: null)));

        Assert.Same(expected, actual);
        var advice = Assert.IsType<AiGeneratedAdvice>(workspace.AiGeneratedAdvice);
        Assert.Equal("partial reasoning", advice.ReasoningContent);
        Assert.False(advice.IsReady);
        Assert.False(advice.Sending);
        Assert.Equal(AiAdviceGenerationStatus.Failed, advice.GenerationStatus);
    }

    [Fact]
    public async Task GenerateAsync_propagates_access_failure_and_marks_workspace_failed()
    {
        var expected = new AiAdviceAccessException(
            "host unavailable",
            AiAdviceAccessFailureKind.Rejected);
        var gateway = CreateFailingGateway(
            expected,
            new AiAdviceGatewayUpdate(
                AiAdviceGatewayUpdateKind.Recommendation,
                "partial recommendation"));
        var service = new AiAdviceApplicationService(gateway);
        var workspace = PrepareWorkspace(service);

        var actual = await Assert.ThrowsAsync<AiAdviceAccessException>(
            () => DrainAsync(service.GenerateAsync(workspace, environment: null)));

        Assert.Same(expected, actual);
        Assert.Equal(AiAdviceAccessFailureKind.Rejected, actual.FailureKind);
        var advice = Assert.IsType<AiGeneratedAdvice>(workspace.AiGeneratedAdvice);
        Assert.Equal("partial recommendation", advice.Content);
        Assert.False(advice.IsReady);
        Assert.False(advice.Sending);
        Assert.Equal(AiAdviceGenerationStatus.Failed, advice.GenerationStatus);
    }

    [Fact]
    public async Task GenerateAsync_cancellation_marks_incomplete_and_keeps_partial_content()
    {
        var waitingForCancellation = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubAiAdviceGateway(
            (_, cancellationToken) => PartialUpdatesThenWaitAsync(
                waitingForCancellation,
                cancellationToken));
        var service = new AiAdviceApplicationService(gateway);
        var workspace = PrepareWorkspace(service);
        using var cancellation = new CancellationTokenSource();

        var generation = DrainAsync(
            service.GenerateAsync(workspace, environment: null, cancellation.Token));
        await waitingForCancellation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await generation;
        });

        var advice = Assert.IsType<AiGeneratedAdvice>(workspace.AiGeneratedAdvice);
        Assert.Equal("partial reasoning", advice.ReasoningContent);
        Assert.Equal("partial recommendation", advice.Content);
        Assert.False(advice.IsReady);
        Assert.False(advice.Sending);
        Assert.Equal(AiAdviceGenerationStatus.Incomplete, advice.GenerationStatus);
        Assert.NotNull(advice.CompletedAt);
    }

    [Fact]
    public async Task GenerateAsync_early_disposal_marks_incomplete_and_keeps_partial_content()
    {
        var gateway = CreateGateway(
            new(AiAdviceGatewayUpdateKind.Reasoning, "partial reasoning"),
            new(AiAdviceGatewayUpdateKind.Recommendation, "partial recommendation"),
            new(AiAdviceGatewayUpdateKind.Recommendation, "not consumed"));
        var service = new AiAdviceApplicationService(gateway);
        var workspace = PrepareWorkspace(service);

        await using (var updates = service
            .GenerateAsync(workspace, environment: null)
            .GetAsyncEnumerator())
        {
            Assert.True(await updates.MoveNextAsync());
            Assert.True(await updates.MoveNextAsync());
            Assert.True(workspace.AiGeneratedAdvice?.Sending);
        }

        var advice = Assert.IsType<AiGeneratedAdvice>(workspace.AiGeneratedAdvice);
        Assert.Equal("partial reasoning", advice.ReasoningContent);
        Assert.Equal("partial recommendation", advice.Content);
        Assert.False(advice.IsReady);
        Assert.False(advice.Sending);
        Assert.Equal(AiAdviceGenerationStatus.Incomplete, advice.GenerationStatus);
        Assert.NotNull(advice.CompletedAt);
    }

    [Fact]
    public async Task PreparePrompt_rejects_active_generation_without_replacing_advice()
    {
        var gateway = CreateGateway(
            new(AiAdviceGatewayUpdateKind.Reasoning, "partial reasoning"),
            new(AiAdviceGatewayUpdateKind.Recommendation, "recommendation"));
        var service = new AiAdviceApplicationService(gateway);
        var workspace = PrepareWorkspace(service);
        var originalAdvice = Assert.IsType<AiGeneratedAdvice>(workspace.AiGeneratedAdvice);
        var originalPrompt = Assert.IsType<AdviceRequest>(workspace.AdvicePrompt);

        await using var updates = service
            .GenerateAsync(workspace, environment: null)
            .GetAsyncEnumerator();
        Assert.True(await updates.MoveNextAsync());

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = service.PreparePrompt(workspace);
        });

        Assert.Contains("still in progress", exception.Message, StringComparison.Ordinal);
        Assert.Same(originalAdvice, workspace.AiGeneratedAdvice);
        Assert.Same(originalPrompt, workspace.AdvicePrompt);
        Assert.True(originalAdvice.Sending);
        Assert.Equal(AiAdviceGenerationStatus.Generating, originalAdvice.GenerationStatus);
        Assert.Equal("partial reasoning", originalAdvice.ReasoningContent);
    }

    [Fact]
    public async Task GenerateAsync_second_enumerator_rejects_same_workspace_on_first_move_next()
    {
        var gateway = CreateGateway(
            new(AiAdviceGatewayUpdateKind.Reasoning, "first attempt"),
            new(AiAdviceGatewayUpdateKind.Recommendation, "recommendation"));
        var service = new AiAdviceApplicationService(gateway);
        var workspace = PrepareWorkspace(service);

        await using var first = service
            .GenerateAsync(workspace, environment: null)
            .GetAsyncEnumerator();
        Assert.True(await first.MoveNextAsync());

        await using var second = service
            .GenerateAsync(workspace, environment: null)
            .GetAsyncEnumerator();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await second.MoveNextAsync();
        });

        Assert.Contains("already in progress", exception.Message, StringComparison.Ordinal);
        var advice = Assert.IsType<AiGeneratedAdvice>(workspace.AiGeneratedAdvice);
        Assert.True(advice.Sending);
        Assert.Equal(AiAdviceGenerationStatus.Generating, advice.GenerationStatus);
        Assert.Equal("first attempt", advice.ReasoningContent);
    }

    [Fact]
    public async Task InterruptGeneration_finishes_immediately_and_ignores_late_update()
    {
        var lateMoveStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLateUpdate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new StubAiAdviceGateway(
            (_, cancellationToken) => PartialThenLateUpdateAsync(
                lateMoveStarted,
                releaseLateUpdate,
                cancellationToken));
        var service = new AiAdviceApplicationService(gateway);
        var workspace = PrepareWorkspace(service);

        await using var updates = service
            .GenerateAsync(workspace, environment: null)
            .GetAsyncEnumerator();
        Assert.True(await updates.MoveNextAsync());
        var lateMove = updates.MoveNextAsync().AsTask();
        await lateMoveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var interrupted = service.InterruptGeneration(workspace);
        var advice = Assert.IsType<AiGeneratedAdvice>(workspace.AiGeneratedAdvice);
        var statusImmediately = advice.GenerationStatus;
        var sendingImmediately = advice.Sending;
        var isReadyImmediately = advice.IsReady;
        var completedAtImmediately = advice.CompletedAt;
        var reasoningImmediately = advice.ReasoningContent;
        var contentImmediately = advice.Content;

        releaseLateUpdate.TrySetResult(true);
        var receivedLateUpdate = await lateMove.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(interrupted);
        Assert.Equal(AiAdviceGenerationStatus.Incomplete, statusImmediately);
        Assert.False(sendingImmediately);
        Assert.False(isReadyImmediately);
        Assert.NotNull(completedAtImmediately);
        Assert.Equal("partial reasoning", reasoningImmediately);
        Assert.Empty(contentImmediately);
        Assert.False(receivedLateUpdate);
        Assert.Equal("partial reasoning", advice.ReasoningContent);
        Assert.Empty(advice.Content);
        Assert.False(advice.Sending);
        Assert.Equal(AiAdviceGenerationStatus.Incomplete, advice.GenerationStatus);
    }

    private static ConsultationWorkspace PrepareWorkspace(AiAdviceApplicationService service)
    {
        var workspace = CreateReadyWorkspace();
        Assert.True(service.PreparePrompt(workspace));
        return workspace;
    }

    private static ConsultationWorkspace CreateReadyWorkspace()
    {
        var client = new ClientInfo
        {
            Gender = "female",
            Age = 35,
            Height = 165m,
            Weight = 60m,
            SpecialPhysiologicalPeriod = "pregnancy"
        };
        var energy = new EnergyCalculator(client)
        {
            PAL = 1.5m,
            AvailableEERs =
            [
                new EER
                {
                    BEE = 21m
                }
            ]
        };
        Assert.True(energy.Calculate());
        Assert.True(energy.CorrectEnergy(2100));

        var survey = new DietaryRecallSurvey(client, [], [], new DRIs(client));
        survey.NutrientAssessments.AddRange(
        [
            new DietaryNutrientAssessment
            {
                FriendlyName = "iron",
                Value = 5m,
                LowerReference = new DietaryNutrientReference(
                    DietaryReferenceIntakeType.RNI,
                    10m,
                    "mg/d")
            },
            new DietaryNutrientAssessment
            {
                FriendlyName = "sodium",
                Value = 3000m,
                UpperReference = new DietaryNutrientReference(
                    DietaryReferenceIntakeType.UL,
                    2000m,
                    "mg/d")
            },
            new DietaryNutrientAssessment
            {
                FriendlyName = "protein",
                Value = 65m
            }
        ]);

        return new ConsultationWorkspace(client)
        {
            CurrentEnergyCalculator = energy,
            DietaryRecallSurvey = survey,
            SubjectiveObjectiveAssessmentPlanInformation =
                new SubjectiveObjectiveAssessmentPlanInformation
                {
                    Subjective = "subjective",
                    Objective = "objective",
                    Assessment = "assessment",
                    Plan = "plan"
                }
        };
    }

    private static StubAiAdviceGateway CreateGateway(
        params AiAdviceGatewayUpdate[] updates) =>
        new((_, cancellationToken) => YieldUpdatesAsync(updates, cancellationToken));

    private static StubAiAdviceGateway CreateFailingGateway(
        Exception exception,
        params AiAdviceGatewayUpdate[] updates) =>
        new((_, cancellationToken) =>
            YieldUpdatesThenThrowAsync(updates, exception, cancellationToken));

    private static async Task<List<AiAdviceGatewayUpdate>> DrainAsync(
        IAsyncEnumerable<AiAdviceGatewayUpdate> updates)
    {
        var received = new List<AiAdviceGatewayUpdate>();
        await foreach (var update in updates)
        {
            received.Add(update);
        }

        return received;
    }

    private static async IAsyncEnumerable<AiAdviceGatewayUpdate> YieldUpdatesAsync(
        IEnumerable<AiAdviceGatewayUpdate> updates,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return update;
        }
    }

    private static async IAsyncEnumerable<AiAdviceGatewayUpdate> YieldUpdatesThenThrowAsync(
        IEnumerable<AiAdviceGatewayUpdate> updates,
        Exception? exception,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in YieldUpdatesAsync(updates, cancellationToken))
        {
            yield return update;
        }

        await Task.Yield();
        if (exception is not null)
        {
            throw exception;
        }
    }

    private static async IAsyncEnumerable<AiAdviceGatewayUpdate> PartialUpdatesThenWaitAsync(
        TaskCompletionSource<bool> waitingForCancellation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new AiAdviceGatewayUpdate(
            AiAdviceGatewayUpdateKind.Reasoning,
            "partial reasoning");
        yield return new AiAdviceGatewayUpdate(
            AiAdviceGatewayUpdateKind.Recommendation,
            "partial recommendation");
        waitingForCancellation.TrySetResult(true);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private static async IAsyncEnumerable<AiAdviceGatewayUpdate> PartialThenLateUpdateAsync(
        TaskCompletionSource<bool> lateMoveStarted,
        TaskCompletionSource<bool> releaseLateUpdate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new AiAdviceGatewayUpdate(
            AiAdviceGatewayUpdateKind.Reasoning,
            "partial reasoning");
        lateMoveStarted.TrySetResult(true);
        await releaseLateUpdate.Task.WaitAsync(cancellationToken);
        yield return new AiAdviceGatewayUpdate(
            AiAdviceGatewayUpdateKind.Recommendation,
            "late recommendation");
    }

    private sealed class StubAiAdviceGateway(
        Func<AdviceRequest, CancellationToken, IAsyncEnumerable<AiAdviceGatewayUpdate>> generate)
        : IAiAdviceGateway
    {
        public AdviceRequest? LastPrompt { get; private set; }

        public Task<AdviceEnvironment?> GetEnvironmentAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AdviceEnvironment?>(null);

        public IAsyncEnumerable<AiAdviceGatewayUpdate> GenerateAsync(
            AdviceRequest prompt,
            CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            return generate(prompt, cancellationToken);
        }
    }
}
