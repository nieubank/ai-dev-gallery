// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.OnnxRuntime;
using Microsoft.Windows.AI.MachineLearning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;
internal static class WinMLHelpers
{
    /// <summary>
    /// Ensures and registers all compatible execution providers using the new ExecutionProviderCatalog API.
    /// </summary>
    public static async Task EnsureAndRegisterAllAsync()
    {
        try
        {
            var catalog = ExecutionProviderCatalog.GetDefault();
            var registeredProviders = await catalog.EnsureAndRegisterAllAsync();
            Debug.WriteLine($"Successfully registered {registeredProviders.Count} execution providers");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WARNING: Failed to ensure and register execution providers: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures and registers a specific execution provider by name using the new ExecutionProviderCatalog API.
    /// </summary>
    /// <param name="providerName">The name of the execution provider to find and register</param>
    /// <returns>True if the provider was successfully registered, false otherwise</returns>
    public static async Task<bool> EnsureAndRegisterProviderAsync(string providerName)
    {
        try
        {
            var catalog = ExecutionProviderCatalog.GetDefault();
            var providers = catalog.FindAllProviders();

            var targetProvider = providers.FirstOrDefault(p => string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase));
            if (targetProvider == null)
            {
                Debug.WriteLine($"WARNING: Execution provider '{providerName}' not found in catalog");
                return false;
            }

            // Check if provider is already ready
            if (targetProvider.ReadyState() != ExecutionProviderReadyState.Ready)
            {
                var result = await targetProvider.EnsureReadyAsync();
                if (result.Status != ExecutionProviderReadyResultState.Success)
                {
                    Debug.WriteLine($"WARNING: Failed to make execution provider '{providerName}' ready: {result.DiagnosticText}");
                    return false;
                }
            }

            // Register the provider with ONNX Runtime
            if (!targetProvider.TryRegister())
            {
                Debug.WriteLine($"WARNING: Failed to register execution provider '{providerName}' with ONNX Runtime");
                return false;
            }

            Debug.WriteLine($"Successfully ensured and registered execution provider: {providerName}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WARNING: Failed to ensure and register execution provider '{providerName}': {ex.Message}");
            return false;
        }
    }

    public static bool AppendExecutionProviderFromEpName(this SessionOptions sessionOptions, string epName, OrtEnv? environment = null)
    {
        if (epName == "CPU")
        {
            // No need to append CPU execution provider
            return true;
        }

        environment ??= OrtEnv.Instance();
        var epDeviceMap = GetEpDeviceMap(environment);

        if (epDeviceMap.TryGetValue(epName, out var devices))
        {
            Dictionary<string, string> epOptions = new(StringComparer.OrdinalIgnoreCase);
            switch(epName)
            {
                case "OpenVINOExecutionProvider":
                    // Configure threading for OpenVINO EP
                    epOptions["num_of_threads"] = "4";
                    break;
                case "QNNExecutionProvider":
                    // Configure performance mode for QNN EP
                    epOptions["htp_performance_mode"] = "high_performance";
                    break;
                default:
                    break;
            }

            sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
            return true;
        }

        return false;
    }

    public static string? GetCompiledModel(this SessionOptions sessionOptions, string modelPath, string device)
    {
        var compiledModelPath = Path.Combine(Path.GetDirectoryName(modelPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelPath)) + $".{device}.onnx";

        if (!File.Exists(compiledModelPath))
        {
            using OrtModelCompilationOptions compilationOptions = new(sessionOptions);
            compilationOptions.SetInputModelPath(modelPath);
            compilationOptions.SetOutputModelPath(compiledModelPath);
            compilationOptions.CompileModel();
        }

        if (File.Exists(compiledModelPath))
        {
            return compiledModelPath;
        }

        return null;
    }

    public static Dictionary<string, List<OrtEpDevice>> GetEpDeviceMap(OrtEnv? environment = null)
    {
        environment ??= OrtEnv.Instance();
        IReadOnlyList<OrtEpDevice> epDevices = environment.GetEpDevices();
        Dictionary<string, List<OrtEpDevice>> epDeviceMap = new(StringComparer.OrdinalIgnoreCase);

        foreach (OrtEpDevice device in epDevices)
        {
            string name = device.EpName;

            if (!epDeviceMap.TryGetValue(name, out List<OrtEpDevice>? value))
            {
                value = [];
                epDeviceMap[name] = value;
            }

            value.Add(device);
        }

        return epDeviceMap;
    }
}