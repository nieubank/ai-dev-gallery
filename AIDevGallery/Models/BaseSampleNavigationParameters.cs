// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.ExternalModelUtils;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Utils;
using Microsoft.Extensions.AI;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Models;

internal abstract class BaseSampleNavigationParameters(TaskCompletionSource sampleLoadedCompletionSource, CancellationToken loadingCanceledToken)
{
    public CancellationToken CancellationToken { get; private set; } = loadingCanceledToken;
    public TaskCompletionSource SampleLoadedCompletionSource { get; set; } = sampleLoadedCompletionSource;

    protected abstract string ChatClientModelPath { get; }
    protected abstract HardwareAccelerator ChatClientHardwareAccelerator { get; }
    protected abstract LlmPromptTemplate? ChatClientPromptTemplate { get; }

    public abstract WinMlSampleOptions WinMlSampleOptions { get; }

    public void NotifyCompletion()
    {
        SampleLoadedCompletionSource.SetResult();
    }

    public async Task<IChatClient?> GetIChatClientAsync()
    {
        if (ChatClientModelPath == $"file://{ModelType.PhiSilica}")
        {
            return await PhiSilicaClient.CreateAsync(CancellationToken).ConfigureAwait(false);
        }
        else if (ExternalModelHelper.IsUrlFromExternalProvider(ChatClientModelPath))
        {
#if ENABLE_FOUNDRY_LOCAL
            if (ChatClientHardwareAccelerator == HardwareAccelerator.FOUNDRYLOCAL)
            {
                await FoundryLocalModelProvider.Instance.EnsureModelReadyAsync(ChatClientModelPath, CancellationToken).ConfigureAwait(false);
            }
#endif

            return ExternalModelHelper.GetIChatClient(ChatClientModelPath);
        }

#if WINML_RUNTIME_EXPERIMENTAL
        if (App.AppData.UseWinMLRuntime)
        {
            return await GetWinMLRuntimeChatClientAsync().ConfigureAwait(false);
        }
#endif

        return await OnnxRuntimeGenAIChatClientFactory.CreateAsync(
            ChatClientModelPath,
            ChatClientPromptTemplate,
            null,
            CancellationToken).ConfigureAwait(false);
    }

#if WINML_RUNTIME_EXPERIMENTAL
    public async Task<IChatClient?> GetWinMLRuntimeChatClientAsync()
    {
        var runtimeOptions = App.AppData.WinMLRuntimeOptions;
        var deviceType = MapDeviceType(runtimeOptions.DeviceType);
        var executionPolicy = MapExecutionPolicy(runtimeOptions.ExecutionPolicy);

        return await WinMLRuntimeChatClient.CreateAsync(
            ChatClientModelPath,
            ChatClientPromptTemplate,
            deviceType,
            executionPolicy,
            CancellationToken).ConfigureAwait(false);
    }

    private static Interop.WinMLRuntime.WinMLDeviceType MapDeviceType(string? deviceType)
    {
        return deviceType?.ToUpperInvariant() switch
        {
            "CPU" => Interop.WinMLRuntime.WinMLDeviceType.CPU,
            "GPU" => Interop.WinMLRuntime.WinMLDeviceType.GPU,
            "NPU" => Interop.WinMLRuntime.WinMLDeviceType.NPU,
            _ => Interop.WinMLRuntime.WinMLDeviceType.Default
        };
    }

    private static Interop.WinMLRuntime.WinMLExecutionPolicy MapExecutionPolicy(string? policy)
    {
        return policy?.ToUpperInvariant() switch
        {
            "PREFERPERFORMANCE" => Interop.WinMLRuntime.WinMLExecutionPolicy.PreferPerformance,
            "PREFEREFFICIENCY" => Interop.WinMLRuntime.WinMLExecutionPolicy.PreferEfficiency,
            _ => Interop.WinMLRuntime.WinMLExecutionPolicy.Default
        };
    }
#endif

    internal abstract void SendSampleInteractionEvent(string? customInfo = null);
}