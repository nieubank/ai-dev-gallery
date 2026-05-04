// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

#pragma warning disable SYSLIB1099 // Experimental WinML interop uses runtime-based COM marshalling with [GeneratedComInterface] types
#pragma warning disable SA1615 // Element return value should be documented
#pragma warning disable SA1518 // File may not end with a newline character

using AIDevGallery.Interop.WinMLRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.SharedCode;

/// <summary>
/// A managed wrapper that provides an API surface similar to ORT's InferenceSession
/// but backed by IWinMLPipeline. Enables per-sample WinML Runtime integration with
/// minimal diff — samples swap InferenceSession for WinMLInferenceSession and adjust
/// tensor I/O calls.
/// </summary>
internal sealed class WinMLInferenceSession : IDisposable
{
    private readonly WinMLRuntimeWrapper _runtime;
    private readonly IWinMLModel _model;
    private readonly IWinMLPipeline _pipeline;
    private readonly IWinMLStage _stage;
    private bool _disposed;

    private WinMLInferenceSession(
        WinMLRuntimeWrapper runtime,
        IWinMLModel model,
        IWinMLPipeline pipeline,
        IWinMLStage stage)
    {
        _runtime = runtime;
        _model = model;
        _pipeline = pipeline;
        _stage = stage;
    }

    /// <summary>
    /// Creates a WinML Runtime inference session from a model file path.
    /// </summary>
    public static async Task<WinMLInferenceSession> CreateAsync(
        string modelPath,
        WinMLDeviceType deviceType = WinMLDeviceType.Default,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var runtime = WinMLRuntimeWrapper.Create();
                var model = WinMLRuntimeModelLoader.LoadModelWithExternalData(runtime, modelPath);

                var pipeline = runtime.CreatePipeline(model, deviceType);
                pipeline.Initialize();
                pipeline.GetStage(0, out var stage);

                cancellationToken.ThrowIfCancellationRequested();

                return new WinMLInferenceSession(runtime, model, pipeline, stage);
            },
            cancellationToken);
    }

    /// <summary>
    /// Runs inference with named DenseTensor inputs and returns named DenseTensor outputs.
    /// This is the primary API matching the ORT NamedOnnxValue pattern.
    /// </summary>
    public Dictionary<string, DenseTensor<float>> Run(Dictionary<string, DenseTensor<float>> inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Bind inputs by name
        foreach (var (name, tensor) in inputs)
        {
            _stage.FindInputIndex(name, out var index);
            var winmlTensor = WinMLTensorHelper.FromDenseTensor(_runtime.GetRawRuntime(), tensor);
            try
            {
                _stage.BindInput(index, winmlTensor);
            }
            finally
            {
                Marshal.FinalReleaseComObject(winmlTensor);
            }
        }

        // Execute
        RunPipeline();

        // Read outputs
        _stage.GetOutputCount(out var outputCount);
        var results = new Dictionary<string, DenseTensor<float>>((int)outputCount);

        for (uint i = 0; i < outputCount; i++)
        {
            _model.GetOutputName(i, out var namePtr);
            var name = Marshal.PtrToStringUni(namePtr) ?? string.Empty;
            _stage.GetOutput(i, out var outputTensor);
            try
            {
                results[name] = WinMLTensorHelper.ToDenseTensorFloat(outputTensor);
            }
            finally
            {
                Marshal.FinalReleaseComObject(outputTensor);
            }
        }

        return results;
    }

    /// <summary>
    /// Runs inference with a single named input and returns the first output as DenseTensor.
    /// Convenience overload for single-input/single-output models.
    /// </summary>
    public DenseTensor<float> RunSingle(string inputName, DenseTensor<float> input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _stage.FindInputIndex(inputName, out var inputIndex);
        var winmlTensor = WinMLTensorHelper.FromDenseTensor(_runtime.GetRawRuntime(), input);
        try
        {
            _stage.BindInput(inputIndex, winmlTensor);
        }
        finally
        {
            Marshal.FinalReleaseComObject(winmlTensor);
        }

        RunPipeline();

        _stage.GetOutput(0, out var outputTensor);
        try
        {
            return WinMLTensorHelper.ToDenseTensorFloat(outputTensor);
        }
        finally
        {
            Marshal.FinalReleaseComObject(outputTensor);
        }
    }

    /// <summary>
    /// Gets an input name by index, matching session.InputMetadata usage.
    /// </summary>
    public string GetInputName(uint index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _model.GetInputName(index, out var namePtr);
        return Marshal.PtrToStringUni(namePtr) ?? string.Empty;
    }

    /// <summary>
    /// Gets an output name by index.
    /// </summary>
    public string GetOutputName(uint index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _model.GetOutputName(index, out var namePtr);
        return Marshal.PtrToStringUni(namePtr) ?? string.Empty;
    }

    /// <summary>
    /// Gets the input count.
    /// </summary>
    public uint InputCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _model.GetInputCount(out var count);
            return count;
        }
    }

    /// <summary>
    /// Gets the output count.
    /// </summary>
    public uint OutputCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _model.GetOutputCount(out var count);
            return count;
        }
    }

    /// <summary>
    /// Gets the input tensor descriptor (shape, data type).
    /// </summary>
    public unsafe (WinMLTensorDataType DataType, ulong[] Dimensions) GetInputTensorDesc(uint index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _model.GetInputTensorDesc(index, out var desc);
        var dims = new ulong[desc.DimensionCount];
        for (int i = 0; i < (int)desc.DimensionCount; i++)
        {
            dims[i] = desc.Dimensions[i];
        }

        return (desc.DataType, dims);
    }

    /// <summary>
    /// Gets direct access to the underlying stage for advanced scenarios
    /// (e.g. binding raw IWinMLTensor, using FindInputIndex/FindOutputIndex directly).
    /// </summary>
    public IWinMLStage Stage => _stage;

    /// <summary>
    /// Gets direct access to the pipeline for advanced scenarios.
    /// </summary>
    public IWinMLPipeline Pipeline => _pipeline;

    private void RunPipeline()
    {
        _pipeline.Run(0, out var completionFence);
        if (completionFence != IntPtr.Zero)
        {
            Marshal.Release(completionFence);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_stage != null)
            {
                Marshal.FinalReleaseComObject(_stage);
            }

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
