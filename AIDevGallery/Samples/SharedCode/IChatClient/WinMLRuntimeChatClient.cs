// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

#pragma warning disable SYSLIB1099 // Experimental WinML interop uses runtime-based COM marshalling with [GeneratedComInterface] types
#pragma warning disable SA1518 // File may not end with a newline character

using AIDevGallery.Interop.WinMLRuntime;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

/// <summary>
/// IChatClient implementation backed by the WinML Runtime API (IWinMLTextGenerator).
/// This is a side-by-side alternative to OnnxRuntimeGenAIChatClient.
/// </summary>
internal sealed class WinMLRuntimeChatClient : IChatClient
{
    private const uint DefaultMaxNewTokens = 1024;
    private const uint DefaultContextLength = 2048;

    private readonly WinMLRuntimeWrapper _runtime;
    private readonly IWinMLModel _model;
    private readonly IWinMLPipeline _pipeline;
    private readonly WinMLTokenizerWrapper _tokenizer;
    private readonly WinMLTextGeneratorWrapper _generator;
    private readonly LlmPromptTemplate? _promptTemplate;

    private bool _disposed;

    private WinMLRuntimeChatClient(
        WinMLRuntimeWrapper runtime,
        IWinMLModel model,
        IWinMLPipeline pipeline,
        WinMLTokenizerWrapper tokenizer,
        WinMLTextGeneratorWrapper generator,
        LlmPromptTemplate? promptTemplate)
    {
        _runtime = runtime;
        _model = model;
        _pipeline = pipeline;
        _tokenizer = tokenizer;
        _generator = generator;
        _promptTemplate = promptTemplate;
    }

    public ChatClientMetadata Metadata => new("WinMLRuntime");

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType?.IsInstanceOfType(this) == true ? this : null;
    }

    public static async Task<WinMLRuntimeChatClient> CreateAsync(
        string modelDir,
        LlmPromptTemplate? promptTemplate = null,
        WinMLDeviceType deviceType = WinMLDeviceType.Default,
        WinMLExecutionPolicy executionPolicy = WinMLExecutionPolicy.Default,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                WinMLRuntimeWrapper? runtime = null;
                IWinMLModel? model = null;
                IWinMLPipeline? pipeline = null;
                WinMLTokenizerWrapper? tokenizer = null;
                WinMLTextGeneratorWrapper? generator = null;
                bool ownershipTransferred = false;

                try
                {
                    runtime = WinMLRuntimeWrapper.Create(executionPolicy);

                    var config = WinMLGenAIConfig.LoadFromDirectory(modelDir);
                    var modelPath = config.GetRequiredDecoderPath();
                    System.Diagnostics.Trace.WriteLine($"[WinMLRuntime] GenAI config: {config.ConfigPath}");
                    System.Diagnostics.Trace.WriteLine($"[WinMLRuntime] Decoder path: {modelPath}");
                    System.Diagnostics.Trace.WriteLine($"[WinMLRuntime] Model dir:  {modelDir}");
                    System.Diagnostics.Trace.WriteLine($"[WinMLRuntime] DeviceType: {deviceType}, ExecutionPolicy: {executionPolicy}");

                    // Log files in model directory to help diagnose missing external weights
                    foreach (var f in Directory.GetFiles(modelDir))
                    {
                        var info = new FileInfo(f);
                        System.Diagnostics.Trace.WriteLine($"[WinMLRuntime]   {info.Name} ({info.Length:N0} bytes)");
                    }

                    model = WinMLRuntimeModelLoader.LoadModelWithExternalData(runtime, modelPath);

                    System.Diagnostics.Trace.WriteLine($"[WinMLRuntime] Creating pipeline...");
                    try
                    {
                        pipeline = CreatePipelineFromConfig(runtime, model, config, deviceType, executionPolicy);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"[WinMLRuntime] CreatePipeline failed: {ex.GetType().Name}: {ex.Message}");
                        System.Diagnostics.Trace.WriteLine($"[WinMLRuntime] HRESULT: 0x{ex.HResult:X8}");
                        System.Diagnostics.Trace.WriteLine($"[WinMLRuntime] Model was loaded from: {modelPath}");
                        System.Diagnostics.Trace.WriteLine($"[WinMLRuntime] Ensure Runtime dependencies and model resource files are available in: {modelDir}");
                        throw;
                    }

                    SetMaxSequenceLength(pipeline, config.ContextLength > 0 ? config.ContextLength : DefaultContextLength);

                    var tokenizerPath = Path.Combine(modelDir, "tokenizer.json");
                    tokenizer = WinMLTokenizerWrapper.Create(
                        File.Exists(tokenizerPath) ? tokenizerPath : modelDir);

                    var samplingDesc = CreateSamplingDesc(config);
                    IntPtr samplerPtr = IntPtr.Zero;
                    IWinMLSampler sampler;
                    try
                    {
                        Marshal.ThrowExceptionForHR(WinMLNativeMethods.WinMLCreateSampler(
                            in samplingDesc, out samplerPtr));
                        sampler = (IWinMLSampler)Marshal.GetObjectForIUnknown(samplerPtr);
                    }
                    finally
                    {
                        if (samplerPtr != IntPtr.Zero)
                        {
                            Marshal.Release(samplerPtr);
                        }
                    }

                    var genDesc = new WinMLGenerationDesc
                    {
                        MaxLength = GetMaxLength(config),
                        Sampling = samplingDesc,
                        EnableSampling = 1,
                        VocabSize = config.VocabSize
                    };
                    try
                    {
                        generator = WinMLTextGeneratorWrapper.Create(
                            pipeline,
                            genDesc,
                            config.EosTokenIds,
                            tokenizer.GetRawTokenizer(),
                            sampler);

                        cancellationToken.ThrowIfCancellationRequested();

                        var client = new WinMLRuntimeChatClient(
                            runtime, model, pipeline, tokenizer, generator, promptTemplate);
                        ownershipTransferred = true;
                        return client;
                    }
                    finally
                    {
                        Marshal.FinalReleaseComObject(sampler);
                    }
                }
                finally
                {
                    if (!ownershipTransferred)
                    {
                        generator?.Dispose();
                        tokenizer?.Dispose();
                        ReleaseComObject(pipeline);
                        ReleaseComObject(model);
                        runtime?.Dispose();
                    }
                }
            },
            cancellationToken);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var responseText = new StringBuilder();
        await foreach (var update in GetStreamingResponseAsync(chatMessages, options, cancellationToken))
        {
            if (update.Text != null)
            {
                responseText.Append(update.Text);
            }
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText.ToString()));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var prompt = FormatPrompt(chatMessages);
        var stopSequences = _promptTemplate?.Stop ?? Array.Empty<string>();

        // Run generation on background thread, yield results
        var tokenChannel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        var generationTask = Task.Run(
            () =>
            {
                try
                {
                    _generator.Reset();
                    _tokenizer.ResetDecodeState();

                    var tokenIds = _tokenizer.Encode(prompt);
                    _generator.AppendPromptTokens(tokenIds);

                    var recentText = new StringBuilder();

                    while (!_generator.IsDone())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var tokenId = _generator.GenerateNextToken();
                        if (_generator.IsDone())
                        {
                            break;
                        }

                        var text = _tokenizer.DecodeIncremental(tokenId);
                        if (!string.IsNullOrEmpty(text))
                        {
                            recentText.Append(text);

                            // Check if any stop sequence has been generated
                            bool hitStop = false;
                            foreach (var stop in stopSequences)
                            {
                                if (recentText.ToString().Contains(stop, StringComparison.Ordinal))
                                {
                                    hitStop = true;
                                    break;
                                }
                            }

                            if (hitStop)
                            {
                                break;
                            }

                            tokenChannel.Writer.TryWrite(text);

                            // Keep the tail of recentText for stop-sequence matching across chunk boundaries
                            if (stopSequences.Length > 0)
                            {
                                int maxStopLen = 0;
                                foreach (var s in stopSequences)
                                {
                                    if (s.Length > maxStopLen)
                                    {
                                        maxStopLen = s.Length;
                                    }
                                }

                                if (recentText.Length > maxStopLen * 2)
                                {
                                    recentText.Remove(0, recentText.Length - maxStopLen);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    tokenChannel.Writer.Complete();
                }
            },
            cancellationToken);

        await foreach (var text in tokenChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, text);
        }

        await generationTask;
    }

    private string FormatPrompt(IEnumerable<ChatMessage> messages)
    {
        if (_promptTemplate == null)
        {
            return string.Join(". ", messages.Select(m => m.Text));
        }

        var sb = new StringBuilder();
        var messageList = messages.ToList();

        for (int i = 0; i < messageList.Count; i++)
        {
            var message = messageList[i];
            if (message.Role == ChatRole.System && i == 0)
            {
                if (!string.IsNullOrWhiteSpace(_promptTemplate.System))
                {
                    sb.Append(_promptTemplate.System.Replace("{{CONTENT}}", message.Text));
                }
            }
            else if (message.Role == ChatRole.User)
            {
                if (!string.IsNullOrWhiteSpace(_promptTemplate.User))
                {
                    sb.Append(_promptTemplate.User.Replace("{{CONTENT}}", message.Text));
                }
                else
                {
                    sb.Append(message.Text);
                }
            }
            else if (message.Role == ChatRole.Assistant)
            {
                if (!string.IsNullOrWhiteSpace(_promptTemplate.Assistant))
                {
                    sb.Append(_promptTemplate.Assistant.Replace("{{CONTENT}}", message.Text));
                }
                else
                {
                    sb.Append(message.Text);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_promptTemplate.Assistant))
        {
            // Add the start of assistant response (before "{{CONTENT}}")
            var assistantStart = _promptTemplate.Assistant.Split("{{CONTENT}}")[0];
            sb.Append(assistantStart);
        }

        return sb.ToString();
    }

    private static IWinMLPipeline CreatePipelineFromConfig(
        WinMLRuntimeWrapper runtime,
        IWinMLModel decoderModel,
        WinMLGenAIConfig config,
        WinMLDeviceType deviceType,
        WinMLExecutionPolicy executionPolicy)
    {
        var embeddingsPath = config.GetOptionalModelPath(config.EmbeddingsFilename);
        var lmHeadPath = config.GetOptionalModelPath(config.LmHeadFilename);

        if (embeddingsPath is null && lmHeadPath is null)
        {
            return runtime.CreatePipeline(decoderModel, deviceType, executionPolicy);
        }

        if (embeddingsPath is null || lmHeadPath is null)
        {
            throw new InvalidOperationException("genai_config.json describes a partial multi-stage pipeline. Both embeddings and lm_head stages are required when either is present.");
        }

        IWinMLPipelineBuilder? builder = null;
        IWinMLModel? embeddingsModel = null;
        IWinMLModel? lmHeadModel = null;
        IWinMLStage? embeddingsStage = null;
        IWinMLStage? decoderStage = null;
        IWinMLStage? lmHeadStage = null;

        try
        {
            builder = runtime.CreatePipelineBuilder();
            embeddingsModel = WinMLRuntimeModelLoader.LoadModelWithExternalData(runtime, embeddingsPath);
            lmHeadModel = WinMLRuntimeModelLoader.LoadModelWithExternalData(runtime, lmHeadPath);

            embeddingsStage = AddStage(builder, embeddingsModel, "embeddings", deviceType, executionPolicy);
            decoderStage = AddStage(builder, decoderModel, "decoder", deviceType, executionPolicy);
            lmHeadStage = AddStage(builder, lmHeadModel, "lm_head", deviceType, executionPolicy);

            ConnectByTensorName(builder, embeddingsStage, decoderStage, config.EmbeddingsOutputName);
            ConnectByTensorName(builder, decoderStage, lmHeadStage, config.LmHeadInputName);

            builder.Build(IntPtr.Zero, out var pipeline);
            return pipeline;
        }
        finally
        {
            ReleaseComObject(lmHeadStage);
            ReleaseComObject(decoderStage);
            ReleaseComObject(embeddingsStage);
            ReleaseComObject(lmHeadModel);
            ReleaseComObject(embeddingsModel);
            ReleaseComObject(builder);
        }
    }

    private static IWinMLStage AddStage(
        IWinMLPipelineBuilder builder,
        IWinMLModel model,
        string name,
        WinMLDeviceType deviceType,
        WinMLExecutionPolicy executionPolicy)
    {
        var namePtr = Marshal.StringToCoTaskMemUni(name);
        try
        {
            var desc = new WinMLStageDesc
            {
                Name = namePtr,
                DeviceType = deviceType,
                Device = IntPtr.Zero,
                Group = 0,
                Flags = WinMLPipelineFlags.None,
                ExecutionPolicy = executionPolicy
            };
            builder.AddStage(model, in desc, out var stage);
            return stage;
        }
        finally
        {
            Marshal.FreeCoTaskMem(namePtr);
        }
    }

    private static void ConnectByTensorName(IWinMLPipelineBuilder builder, IWinMLStage fromStage, IWinMLStage toStage, string? tensorName)
    {
        var connectionDesc = new WinMLConnectionDesc
        {
            SourceOutputIndex = FindOutputIndex(fromStage, tensorName),
            TargetInputIndex = FindInputIndex(toStage, tensorName),
            MaxIterations = 0
        };
        builder.Connect(fromStage, toStage, in connectionDesc, IntPtr.Zero, IntPtr.Zero);
    }

    private static uint FindOutputIndex(IWinMLStage stage, string? tensorName)
    {
        if (string.IsNullOrWhiteSpace(tensorName))
        {
            return 0;
        }

        try
        {
            stage.FindOutputIndex(tensorName, out var index);
            return index;
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException($"The output tensor '{tensorName}' referenced by genai_config.json was not found in the Runtime pipeline stage.", ex);
        }
    }

    private static uint FindInputIndex(IWinMLStage stage, string? tensorName)
    {
        if (string.IsNullOrWhiteSpace(tensorName))
        {
            return 0;
        }

        try
        {
            stage.FindInputIndex(tensorName, out var index);
            return index;
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException($"The input tensor '{tensorName}' referenced by genai_config.json was not found in the Runtime pipeline stage.", ex);
        }
    }

    private static void SetMaxSequenceLength(IWinMLPipeline pipeline, uint maxSequenceLength)
    {
        pipeline.GetStageCount(out var stageCount);
        for (uint i = 0; i < stageCount; i++)
        {
            pipeline.GetStage(i, out var stage);
            try
            {
                if (stage is IWinMLStatefulStage statefulStage)
                {
                    statefulStage.SetMaxSequenceLength(maxSequenceLength);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(stage);
            }
        }
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is not null)
        {
            Marshal.FinalReleaseComObject(comObject);
        }
    }

    private static WinMLSamplingDesc CreateSamplingDesc(WinMLGenAIConfig config)
    {
        return new WinMLSamplingDesc
        {
            Temperature = config.DoSample ? config.Temperature : 0.0f,
            TopK = config.DoSample ? checked((int)config.TopK) : 0,
            TopP = config.DoSample ? config.TopP : 1.0f,
            MinP = 0.0f,
            RepetitionPenalty = config.RepetitionPenalty == 0 ? 1.0f : config.RepetitionPenalty
        };
    }

    private static ulong GetMaxLength(WinMLGenAIConfig config)
    {
        if (config.MaxLength > 0)
        {
            return config.MaxLength;
        }

        if (config.ContextLength > 0)
        {
            return config.ContextLength;
        }

        return DefaultMaxNewTokens;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _generator.Dispose();
            _tokenizer.Dispose();

            if (_pipeline != null)
            {
                Marshal.FinalReleaseComObject(_pipeline);
            }

            if (_model != null)
            {
                Marshal.FinalReleaseComObject(_model);
            }

            _runtime.Dispose();
            _disposed = true;
        }
    }
}
#endif
