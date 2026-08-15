using EzNutrition.Application.Ports;
using EzNutrition.Client.Infrastructure;
using EzNutrition.Shared.Data.DTO.PromptDto;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using PromptDietaryRecallSurvey = EzNutrition.Shared.Data.DTO.PromptDto.DietaryRecallSurvey;

namespace EzNutrition.Client.Tests.Infrastructure;

/// <summary>
/// Locks the WASM AI adapter to the existing Prescription HTTP and SSE protocol.
/// </summary>
public sealed class HttpAiAdviceGatewayTests
{
    private static readonly Uri BaseAddress = new("https://eznutrition.test/");
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Get_environment_uses_authorized_client_and_existing_endpoint()
    {
        var expected = new EnvironmentDto(
            "Synthetic Provider",
            "Synthetic Platform",
            "Synthetic Configuration");
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(expected)));
        var factory = new RecordingHttpClientFactory(handler, BaseAddress);
        var gateway = new HttpAiAdviceGateway(factory);

        var actual = await gateway.GetEnvironmentAsync();

        Assert.Equal(expected, actual);
        Assert.Equal(new[] { "Authorize" }, factory.ClientNames);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri(BaseAddress, "Prescription/Environment"), request.RequestUri);
        Assert.Null(request.Body);
    }

    [Fact]
    public async Task Get_environment_maps_forbidden_to_host_independent_access_denied()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var gateway = CreateGateway(handler);

        var exception = await Assert.ThrowsAsync<AiAdviceAccessException>(
            () => gateway.GetEnvironmentAsync());

        Assert.Equal(AiAdviceAccessFailureKind.AccessDenied, exception.FailureKind);
        Assert.Contains("403", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_posts_prompt_enables_browser_streaming_and_maps_sse_until_done()
    {
        var reasoning = JsonSerializer.Serialize(new AiResultDto("reasoning-fragment", true));
        var recommendation = JsonSerializer.Serialize(new AiResultDto("recommendation-fragment", false));
        var empty = JsonSerializer.Serialize(new AiResultDto(string.Empty, false));
        var sse = string.Join(
            "\r\n",
            ": connected",
            string.Empty,
            "event: ignored",
            string.Empty,
            $"data: {reasoning}",
            string.Empty,
            $"DATA: {recommendation}",
            $"data: {empty}",
            "data: [DONE]",
            $"data: {recommendation}",
            string.Empty);
        var handler = new RecordingHandler((_, _) => Task.FromResult(SseResponse(sse)));
        var factory = new RecordingHttpClientFactory(handler, BaseAddress);
        var gateway = new HttpAiAdviceGateway(factory);
        var prompt = CreatePrompt();

        var updates = await CollectAsync(gateway.GenerateAsync(prompt));

        Assert.Equal(new[] { "AiAuthorize" }, factory.ClientNames);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri(BaseAddress, "Prescription/Generate"), request.RequestUri);
        Assert.Equal("application/json", request.ContentType?.MediaType);
        Assert.Equal("utf-8", request.ContentType?.CharSet);
        Assert.NotNull(request.Body);
        Assert.Contains("女", request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u5973", request.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"patientInfo\"", request.Body, StringComparison.Ordinal);

        var posted = JsonSerializer.Deserialize<PromptDto>(request.Body!, WebJsonOptions);
        Assert.NotNull(posted);
        Assert.Equal(prompt.PatientInfo.Gender, posted.PatientInfo.Gender);
        Assert.Equal(prompt.PatientInfo.Age, posted.PatientInfo.Age);
        Assert.Equal(prompt.PatientInfo.BMI, posted.PatientInfo.BMI);
        Assert.Equal(prompt.PatientInfo.PAL, posted.PatientInfo.PAL);
        Assert.Equal(prompt.PatientInfo.Height, posted.PatientInfo.Height);
        Assert.Equal(prompt.PatientInfo.Weight, posted.PatientInfo.Weight);
        Assert.Equal(
            prompt.PatientInfo.TotalBalanceEnergyViaCalculation,
            posted.PatientInfo.TotalBalanceEnergyViaCalculation);
        Assert.Equal(
            prompt.PatientInfo.SpecialPhysiologicalPeriod,
            posted.PatientInfo.SpecialPhysiologicalPeriod);
        Assert.Equal(prompt.ClinicalInfo?.Subjective, posted.ClinicalInfo?.Subjective);
        Assert.Equal(prompt.ClinicalInfo?.Objective, posted.ClinicalInfo?.Objective);
        Assert.Equal(prompt.ClinicalInfo?.Assessment, posted.ClinicalInfo?.Assessment);
        Assert.Equal(prompt.ClinicalInfo?.Plan, posted.ClinicalInfo?.Plan);
        Assert.Equal(
            prompt.DietaryRecallSurvey?.DeficientNutrients,
            posted.DietaryRecallSurvey?.DeficientNutrients);
        Assert.Equal(
            prompt.DietaryRecallSurvey?.ExcessiveNutrients,
            posted.DietaryRecallSurvey?.ExcessiveNutrients);

        Assert.True(
            request.Options.TryGetValue("WebAssemblyEnableStreamingResponse", out var streamingValue),
            "The WASM adapter did not set the browser response-streaming request option.");
        Assert.True(Assert.IsType<bool>(streamingValue));

        Assert.Collection(
            updates,
            update =>
            {
                Assert.Equal(AiAdviceGatewayUpdateKind.Accepted, update.Kind);
                Assert.Empty(update.Content);
            },
            update =>
            {
                Assert.Equal(AiAdviceGatewayUpdateKind.Reasoning, update.Kind);
                Assert.Equal("reasoning-fragment", update.Content);
            },
            update =>
            {
                Assert.Equal(AiAdviceGatewayUpdateKind.Recommendation, update.Kind);
                Assert.Equal("recommendation-fragment", update.Content);
            });
    }

    [Fact]
    public async Task Generate_rejects_eof_without_done_marker()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(SseResponse(": connected\n\n")));
        var gateway = CreateGateway(handler);
        await using var enumerator = gateway
            .GenerateAsync(CreatePrompt())
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(AiAdviceGatewayUpdateKind.Accepted, enumerator.Current.Kind);
        var exception = await Assert.ThrowsAsync<AiAdviceProtocolException>(async () =>
        {
            _ = await enumerator.MoveNextAsync();
        });

        Assert.Contains("completion marker", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generate_surfaces_provider_error_chunk()
    {
        var error = JsonSerializer.Serialize(new AiResultDto("synthetic provider failure", false, true));
        var handler = new RecordingHandler((_, _) => Task.FromResult(SseResponse(
            $"data: {error}\ndata: [DONE]\n")));
        var gateway = CreateGateway(handler);
        await using var enumerator = gateway
            .GenerateAsync(CreatePrompt())
            .GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(AiAdviceGatewayUpdateKind.Accepted, enumerator.Current.Kind);
        var exception = await Assert.ThrowsAsync<AiAdviceProviderException>(async () =>
        {
            _ = await enumerator.MoveNextAsync();
        });

        Assert.Equal("synthetic provider failure", exception.Message);
    }

    [Fact]
    public async Task Generate_rejects_non_success_status_before_accepting_request()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(
            HttpStatusCode.ServiceUnavailable)));
        var gateway = CreateGateway(handler);
        await using var enumerator = gateway
            .GenerateAsync(CreatePrompt())
            .GetAsyncEnumerator();

        var exception = await Assert.ThrowsAsync<AiAdviceAccessException>(async () =>
        {
            _ = await enumerator.MoveNextAsync();
        });

        Assert.Equal(AiAdviceAccessFailureKind.Unavailable, exception.FailureKind);
        Assert.Contains("503", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_wraps_non_caller_task_cancellation_as_unavailable()
    {
        using var callerCancellation = new CancellationTokenSource();
        var expected = new TaskCanceledException("synthetic handler timeout");
        var handler = new RecordingHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(expected));
        var gateway = CreateGateway(handler);
        await using var enumerator = gateway
            .GenerateAsync(CreatePrompt(), callerCancellation.Token)
            .GetAsyncEnumerator();

        var exception = await Assert.ThrowsAsync<AiAdviceAccessException>(async () =>
        {
            _ = await enumerator.MoveNextAsync();
        });

        Assert.False(callerCancellation.IsCancellationRequested);
        Assert.Equal(AiAdviceAccessFailureKind.Unavailable, exception.FailureKind);
        Assert.Same(expected, exception.InnerException);
    }

    [Fact]
    public async Task Generate_accepts_before_body_arrives_streams_incrementally_and_disposes_on_early_exit()
    {
        using var callerCancellation = new CancellationTokenSource();
        var responseStream = new ControlledSseStream();
        var handler = new RecordingHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(responseStream)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(response);
        });
        var gateway = CreateGateway(handler);
        var enumerator = gateway
            .GenerateAsync(CreatePrompt(), callerCancellation.Token)
            .GetAsyncEnumerator();
        Task<bool>? pendingMove = null;

        try
        {
            pendingMove = enumerator.MoveNextAsync().AsTask();
            Assert.True(await pendingMove.WaitAsync(TimeSpan.FromSeconds(5)));
            pendingMove = null;
            Assert.Equal(AiAdviceGatewayUpdateKind.Accepted, enumerator.Current.Kind);
            Assert.Equal(0, responseStream.ReadCount);

            pendingMove = enumerator.MoveNextAsync().AsTask();
            await responseStream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pendingMove.IsCompleted);

            var recommendation = JsonSerializer.Serialize(
                new AiResultDto("incremental recommendation", false));
            responseStream.Append($"data: {recommendation}\n\n");

            Assert.True(await pendingMove.WaitAsync(TimeSpan.FromSeconds(5)));
            pendingMove = null;
            Assert.Equal(AiAdviceGatewayUpdateKind.Recommendation, enumerator.Current.Kind);
            Assert.Equal("incremental recommendation", enumerator.Current.Content);
            Assert.False(responseStream.IsDisposed);
        }
        finally
        {
            if (pendingMove is not null)
            {
                callerCancellation.Cancel();
                responseStream.Complete();
                try
                {
                    _ = await pendingMove.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception)
                {
                    // Preserve the original assertion failure while allowing the iterator to unwind.
                }
            }

            await enumerator.DisposeAsync();
        }

        Assert.True(responseStream.IsDisposed);
    }

    [Fact]
    public async Task Generate_propagates_cancellation_to_response_stream_read()
    {
        using var cancellation = new CancellationTokenSource();
        var responseStream = new CancellationObservingStream();
        var handler = new RecordingHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(responseStream)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(response);
        });
        var gateway = CreateGateway(handler);
        var collection = CollectAsync(gateway.GenerateAsync(CreatePrompt(), cancellation.Token));

        await responseStream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collection);
        Assert.True(responseStream.CancellationObserved);
        Assert.True(Assert.Single(handler.Requests).CancellationToken.CanBeCanceled);
    }

    private static HttpAiAdviceGateway CreateGateway(RecordingHandler handler) =>
        new(new RecordingHttpClientFactory(handler, BaseAddress));

    private static PromptDto CreatePrompt() => new()
    {
        PatientInfo = new PatientInfo
        {
            Gender = "女",
            Age = 42,
            BMI = 22.4m,
            PAL = 1.6m,
            Height = 165.2m,
            Weight = 61.1m,
            TotalBalanceEnergyViaCalculation = 1850,
            SpecialPhysiologicalPeriod = "乳母"
        },
        DietaryRecallSurvey = new PromptDietaryRecallSurvey
        {
            DeficientNutrients = ["钙", "维生素D"],
            ExcessiveNutrients = ["钠"]
        },
        ClinicalInfo = new ClinicalInfo
        {
            Subjective = "synthetic subjective",
            Objective = "synthetic objective",
            Assessment = "synthetic assessment",
            Plan = "synthetic plan"
        }
    };

    private static async Task<List<AiAdviceGatewayUpdate>> CollectAsync(
        IAsyncEnumerable<AiAdviceGatewayUpdate> source)
    {
        var updates = new List<AiAdviceGatewayUpdate>();
        await foreach (var update in source)
        {
            updates.Add(update);
        }

        return updates;
    }

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(value, WebJsonOptions),
            Encoding.UTF8,
            "application/json")
    };

    private static HttpResponseMessage SseResponse(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "text/event-stream")
    };

    private sealed class RecordingHttpClientFactory(
        HttpMessageHandler handler,
        Uri baseAddress) : IHttpClientFactory
    {
        public List<string> ClientNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            ClientNames.Add(name);
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = baseAddress
            };
        }
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestSnapshot(
                request.Method,
                request.RequestUri,
                request.Content?.Headers.ContentType,
                body,
                request.Options.ToDictionary(option => option.Key, option => option.Value),
                cancellationToken));
            return await responder(request, cancellationToken);
        }
    }

    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri? RequestUri,
        MediaTypeHeaderValue? ContentType,
        string? Body,
        IReadOnlyDictionary<string, object?> Options,
        CancellationToken CancellationToken);

    private sealed class CancellationObservingStream : Stream
    {
        public TaskCompletionSource ReadStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CancellationObserved { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 0;
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class ControlledSseStream : Stream
    {
        private readonly Channel<byte[]> chunks = Channel.CreateUnbounded<byte[]>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

        private byte[]? currentChunk;
        private int currentOffset;
        private int readCount;

        public TaskCompletionSource ReadStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int ReadCount => Volatile.Read(ref readCount);

        public bool IsDisposed { get; private set; }

        public override bool CanRead => !IsDisposed;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void Append(string value)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (!chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(value)))
            {
                throw new InvalidOperationException("The controlled SSE stream has already completed.");
            }
        }

        public void Complete() => chunks.Writer.TryComplete();

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (buffer.IsEmpty)
            {
                return 0;
            }

            Interlocked.Increment(ref readCount);
            ReadStarted.TrySetResult();

            while (currentChunk is null || currentOffset >= currentChunk.Length)
            {
                currentChunk = null;
                currentOffset = 0;
                if (!await chunks.Reader.WaitToReadAsync(cancellationToken)
                    || !chunks.Reader.TryRead(out currentChunk))
                {
                    return 0;
                }
            }

            var count = Math.Min(buffer.Length, currentChunk.Length - currentOffset);
            currentChunk.AsSpan(currentOffset, count).CopyTo(buffer.Span);
            currentOffset += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                IsDisposed = true;
                chunks.Writer.TryComplete();
            }

            base.Dispose(disposing);
        }
    }
}
