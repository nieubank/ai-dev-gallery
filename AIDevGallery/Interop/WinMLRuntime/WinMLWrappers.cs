// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

#pragma warning disable SYSLIB1099 // Experimental WinML interop uses runtime-based COM marshalling with [GeneratedComInterface] types
#pragma warning disable SA1615 // Element return value should be documented
#pragma warning disable SA1618 // Generic type parameter should be documented
#pragma warning disable SA1518 // File may not end with a newline character

using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AIDevGallery.Interop.WinMLRuntime;

/// <summary>
/// Safe managed wrapper around the WinML Runtime COM API.
/// Provides factory methods for creating runtime, models, pipelines, tokenizers, and generators.
/// </summary>
internal sealed class WinMLRuntimeWrapper : IDisposable
{
    private static readonly Guid IID_IWinMLRuntime = new("6954707d-3987-491a-ada9-2bea9b0e13f9");
    private IWinMLRuntime? _runtime;
    private bool _disposed;

    private WinMLRuntimeWrapper(IWinMLRuntime runtime)
    {
        _runtime = runtime;
    }

    public static WinMLRuntimeWrapper Create(WinMLExecutionPolicy policy = WinMLExecutionPolicy.Default)
    {
        unsafe
        {
            var desc = new WinMLRuntimeDesc { ExecutionPolicy = policy };
            Marshal.ThrowExceptionForHR(WinMLNativeMethods.WinMLCreateRuntime(&desc, IID_IWinMLRuntime, out var ptr));
            var runtime = (IWinMLRuntime)Marshal.GetObjectForIUnknown(ptr);
            Marshal.Release(ptr);
            return new WinMLRuntimeWrapper(runtime);
        }
    }

    public IWinMLModel LoadModelFromFile(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _runtime!.LoadModelFromFile(path, out var model);
        return model;
    }

    public IWinMLPipeline CreatePipeline(IWinMLModel model, WinMLDeviceType deviceType = WinMLDeviceType.Default, WinMLExecutionPolicy executionPolicy = WinMLExecutionPolicy.Default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var desc = new WinMLPipelineDesc
        {
            DeviceType = deviceType,
            Flags = WinMLPipelineFlags.None,
            ExecutionPolicy = executionPolicy,
            Device = IntPtr.Zero,
            CommandQueue = IntPtr.Zero
        };
        _runtime!.CreatePipeline(model, in desc, out var pipeline);
        return pipeline;
    }

    public unsafe IWinMLTensor CreateTensor(WinMLTensorDataType dataType, ReadOnlySpan<ulong> dimensions, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        fixed (ulong* dimsPtr = dimensions)
        {
            fixed (byte* dataPtr = data)
            {
                var desc = new WinMLTensorDesc
                {
                    DataType = dataType,
                    DimensionCount = (uint)dimensions.Length,
                    Dimensions = dimsPtr
                };
                _runtime!.CreateTensor(&desc, dataPtr, (ulong)data.Length, out var tensor);
                return tensor;
            }
        }
    }

    public unsafe IWinMLTensor CreateEmptyTensor(WinMLTensorDataType dataType, ReadOnlySpan<ulong> dimensions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        fixed (ulong* dimsPtr = dimensions)
        {
            var desc = new WinMLTensorDesc
            {
                DataType = dataType,
                DimensionCount = (uint)dimensions.Length,
                Dimensions = dimsPtr
            };
            _runtime!.CreateTensor(&desc, null, 0, out var tensor);
            return tensor;
        }
    }

    public IWinMLPipelineBuilder CreatePipelineBuilder()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _runtime!.CreatePipelineBuilder(out var builder);
        return builder;
    }

    public IWinMLRuntime GetRawRuntime()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _runtime!;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_runtime != null)
            {
                Marshal.FinalReleaseComObject(_runtime);
                _runtime = null;
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Helper methods for creating and working with WinML tensors.
/// Bridges between DenseTensor&lt;T&gt; and IWinMLTensor.
/// </summary>
internal static class WinMLTensorHelper
{
    /// <summary>
    /// Creates an IWinMLTensor from a ReadOnlySpan of data and dimensions.
    /// </summary>
    public static unsafe IWinMLTensor CreateTensor(IWinMLRuntime runtime, WinMLTensorDataType dataType, ReadOnlySpan<ulong> dimensions, ReadOnlySpan<byte> data)
    {
        fixed (ulong* dimsPtr = dimensions)
        {
            fixed (byte* dataPtr = data)
            {
                var desc = new WinMLTensorDesc
                {
                    DataType = dataType,
                    DimensionCount = (uint)dimensions.Length,
                    Dimensions = dimsPtr
                };
                runtime.CreateTensor(&desc, dataPtr, (ulong)data.Length, out var tensor);
                return tensor;
            }
        }
    }

    /// <summary>
    /// Creates an IWinMLTensor from a DenseTensor
    /// </summary>
    public static unsafe IWinMLTensor FromDenseTensor(IWinMLRuntime runtime, DenseTensor<float> denseTensor)
    {
        var dims = GetDimensions(denseTensor);
        var buffer = MemoryMarshal.AsBytes(denseTensor.Buffer.Span);
        return CreateTensor(runtime, WinMLTensorDataType.Float32, dims, buffer);
    }

    /// <summary>
    /// Creates an empty IWinMLTensor with the given shape.
    /// </summary>
    public static unsafe IWinMLTensor CreateEmpty(IWinMLRuntime runtime, WinMLTensorDataType dataType, ReadOnlySpan<ulong> dimensions)
    {
        fixed (ulong* dimsPtr = dimensions)
        {
            var desc = new WinMLTensorDesc
            {
                DataType = dataType,
                DimensionCount = (uint)dimensions.Length,
                Dimensions = dimsPtr
            };
            runtime.CreateTensor(&desc, null, 0, out var tensor);
            return tensor;
        }
    }

    /// <summary>
    /// Converts an IWinMLTensor to a DenseTensor&lt;float&gt;.
    /// </summary>
    public static unsafe DenseTensor<float> ToDenseTensorFloat(IWinMLTensor tensor)
    {
        tensor.GetDesc(out var desc);
        var dims = new int[desc.DimensionCount];
        for (int i = 0; i < (int)desc.DimensionCount; i++)
        {
            dims[i] = (int)desc.Dimensions[i];
        }

        var result = new DenseTensor<float>(dims);
        tensor.Lock(WinMLTensorLockMode.Read, WinMLTensorLockFlags.None, out var dataLock);
        try
        {
            dataLock.GetData(out var data, out var dataSize);
            int floatCount = (int)(dataSize / sizeof(float));
            new Span<float>(data, floatCount).CopyTo(result.Buffer.Span);
        }
        finally
        {
            Marshal.FinalReleaseComObject(dataLock);
        }

        return result;
    }

    /// <summary>
    /// Gets the dimensions of a tensor as ulong[].
    /// </summary>
    public static unsafe ulong[] GetTensorDimensions(IWinMLTensor tensor)
    {
        tensor.GetDesc(out var desc);
        var dims = new ulong[desc.DimensionCount];
        for (int i = 0; i < (int)desc.DimensionCount; i++)
        {
            dims[i] = desc.Dimensions[i];
        }

        return dims;
    }

    private static ulong[] GetDimensions(DenseTensor<float> denseTensor)
    {
        var dims = new ulong[denseTensor.Rank];
        for (int i = 0; i < denseTensor.Rank; i++)
        {
            dims[i] = (ulong)denseTensor.Dimensions[i];
        }

        return dims;
    }
}

/// <summary>
/// Safe wrapper around IWinMLTokenizer with CoTaskMemFree for returned arrays.
/// </summary>
internal sealed class WinMLTokenizerWrapper : IDisposable
{
    private IWinMLTokenizer? _tokenizer;
    private bool _disposed;

    private WinMLTokenizerWrapper(IWinMLTokenizer tokenizer)
    {
        _tokenizer = tokenizer;
    }

    public static WinMLTokenizerWrapper Create(string configPath)
    {
        Marshal.ThrowExceptionForHR(WinMLNativeMethods.WinMLCreateTokenizerFromFile(configPath, out var ptr));
        var tokenizer = (IWinMLTokenizer)Marshal.GetObjectForIUnknown(ptr);
        Marshal.Release(ptr);
        return new WinMLTokenizerWrapper(tokenizer);
    }

    public unsafe int[] Encode(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _tokenizer!.Encode(text, out var tokenCount, out var tokenIds);
        try
        {
            var result = new int[checked((int)tokenCount)];
            var source = new Span<uint>(tokenIds, (int)tokenCount);
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = checked((int)source[i]);
            }

            return result;
        }
        finally
        {
            Marshal.FreeCoTaskMem((IntPtr)tokenIds);
        }
    }

    public unsafe string Decode(ReadOnlySpan<int> tokenIds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var unsignedTokenIds = ToUInt32Array(tokenIds);
        fixed (uint* ptr = unsignedTokenIds)
        {
            _tokenizer!.Decode(ptr, (uint)unsignedTokenIds.Length, out var textPtr);
            try
            {
                return Marshal.PtrToStringUni(textPtr) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeCoTaskMem(textPtr);
            }
        }
    }

    public string DecodeIncremental(int tokenId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _tokenizer!.DecodeIncremental(checked((uint)tokenId), out var textPtr);
        try
        {
            return Marshal.PtrToStringUni(textPtr) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeCoTaskMem(textPtr);
        }
    }

    public void ResetDecodeState()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _tokenizer!.ResetDecodeState();
    }

    public IWinMLTokenizer GetRawTokenizer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tokenizer!;
    }

    private static uint[] ToUInt32Array(ReadOnlySpan<int> tokenIds)
    {
        var result = new uint[tokenIds.Length];
        for (int i = 0; i < tokenIds.Length; i++)
        {
            result[i] = checked((uint)tokenIds[i]);
        }

        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_tokenizer != null)
            {
                Marshal.FinalReleaseComObject(_tokenizer);
                _tokenizer = null;
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Safe wrapper around IWinMLTextGenerator for autoregressive text generation.
/// </summary>
internal sealed class WinMLTextGeneratorWrapper : IDisposable
{
    private IWinMLTextGenerator? _generator;
    private bool _disposed;

    internal WinMLTextGeneratorWrapper(IWinMLTextGenerator generator)
    {
        _generator = generator;
    }

    public static WinMLTextGeneratorWrapper Create(
        IWinMLPipeline pipeline,
        WinMLGenerationDesc desc,
        ReadOnlySpan<uint> eosTokenIds,
        IWinMLTokenizer? tokenizer = null,
        IWinMLSampler? sampler = null)
    {
        unsafe
        {
            fixed (uint* eosTokenIdsPtr = eosTokenIds)
            {
                desc.EosTokenIdCount = (uint)eosTokenIds.Length;
                desc.EosTokenIds = eosTokenIds.IsEmpty ? null : eosTokenIdsPtr;
                return Create(pipeline, desc, tokenizer, sampler);
            }
        }
    }

    public static WinMLTextGeneratorWrapper Create(
        IWinMLPipeline pipeline,
        WinMLGenerationDesc desc,
        IWinMLTokenizer? tokenizer = null,
        IWinMLSampler? sampler = null)
    {
        var pipelinePtr = Marshal.GetIUnknownForObject(pipeline);
        var tokenizerPtr = tokenizer != null ? Marshal.GetIUnknownForObject(tokenizer) : IntPtr.Zero;
        var samplerPtr = sampler != null ? Marshal.GetIUnknownForObject(sampler) : IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(WinMLNativeMethods.WinMLCreateTextGenerator(
                pipelinePtr, in desc, tokenizerPtr, samplerPtr, out var ptr));
            var generator = (IWinMLTextGenerator)Marshal.GetObjectForIUnknown(ptr);
            Marshal.Release(ptr);
            return new WinMLTextGeneratorWrapper(generator);
        }
        finally
        {
            Marshal.Release(pipelinePtr);
            if (tokenizerPtr != IntPtr.Zero)
            {
                Marshal.Release(tokenizerPtr);
            }

            if (samplerPtr != IntPtr.Zero)
            {
                Marshal.Release(samplerPtr);
            }
        }
    }

    public unsafe void AppendPromptTokens(ReadOnlySpan<int> tokenIds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var unsignedTokenIds = ToUInt32Array(tokenIds);
        fixed (uint* ptr = unsignedTokenIds)
        {
            _generator!.AppendPromptTokens(ptr, (uint)unsignedTokenIds.Length);
        }
    }

    public int GenerateNextToken()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _generator!.GenerateNextToken(out var manualSamplingRequired, out var tokenId);
        if (manualSamplingRequired != 0)
        {
            throw new InvalidOperationException("WinML Runtime requested manual sampling, but no managed manual-sampling path is configured.");
        }

        return checked((int)tokenId);
    }

    public bool IsDone()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _generator!.IsDone(out var done);
        return done != 0;
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _generator!.Reset();
    }

    public unsafe int[] GenerateAll(ReadOnlySpan<int> promptTokenIds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var unsignedPromptTokenIds = ToUInt32Array(promptTokenIds);
        fixed (uint* promptPtr = unsignedPromptTokenIds)
        {
            _generator!.GenerateAll(promptPtr, (uint)unsignedPromptTokenIds.Length, out var tokensPtr, out var count);
            try
            {
                var result = new int[checked((int)count)];
                var source = new Span<uint>(tokensPtr, (int)count);
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = checked((int)source[i]);
                }

                return result;
            }
            finally
            {
                Marshal.FreeCoTaskMem((IntPtr)tokensPtr);
            }
        }
    }

    private static uint[] ToUInt32Array(ReadOnlySpan<int> tokenIds)
    {
        var result = new uint[tokenIds.Length];
        for (int i = 0; i < tokenIds.Length; i++)
        {
            result[i] = checked((uint)tokenIds[i]);
        }

        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_generator != null)
            {
                Marshal.FinalReleaseComObject(_generator);
                _generator = null;
            }

            _disposed = true;
        }
    }
}

internal sealed record WinMLGenAIConfig(
    string ConfigPath,
    string ModelDirectory,
    string? DecoderFilename,
    string? EncoderFilename,
    uint VocabSize,
    uint ContextLength,
    float Temperature,
    uint TopK,
    float TopP,
    float RepetitionPenalty,
    bool DoSample,
    uint MaxLength,
    uint[] EosTokenIds,
    string? EmbeddingsFilename,
    string? LmHeadFilename,
    string? EmbeddingsOutputName,
    string? LmHeadInputName)
{
    public static WinMLGenAIConfig LoadFromDirectory(string modelDirectory)
    {
        var configPath = Path.Combine(modelDirectory, "genai_config.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Windows ML Runtime requires genai_config.json for language-model loading.", configPath);
        }

        int hr;
        WinMLGenAIConfigNative nativeConfig;
        try
        {
            hr = WinMLNativeMethods.WinMLParseGenAIConfigFromFile(configPath, out nativeConfig);
        }
        catch (EntryPointNotFoundException)
        {
            hr = WinMLNativeMethods.WinMLParseGenAIConfig(configPath, out nativeConfig);
        }

        Marshal.ThrowExceptionForHR(hr);
        try
        {
            return FromNative(configPath, modelDirectory, nativeConfig);
        }
        finally
        {
            FreeNative(ref nativeConfig);
        }
    }

    public string GetRequiredDecoderPath()
    {
        var filename = string.IsNullOrWhiteSpace(DecoderFilename) ? "model.onnx" : DecoderFilename;
        var path = Path.Combine(ModelDirectory, filename);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The decoder model referenced by genai_config.json was not found.", path);
        }

        return path;
    }

    public string? GetOptionalModelPath(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return null;
        }

        var path = Path.Combine(ModelDirectory, filename);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("A model stage referenced by genai_config.json was not found.", path);
        }

        return path;
    }

    private static unsafe WinMLGenAIConfig FromNative(string configPath, string modelDirectory, WinMLGenAIConfigNative nativeConfig)
    {
        var eosTokenIds = new uint[checked((int)nativeConfig.EosTokenIdCount)];
        if (nativeConfig.EosTokenIds != IntPtr.Zero && eosTokenIds.Length > 0)
        {
            new ReadOnlySpan<uint>((void*)nativeConfig.EosTokenIds, eosTokenIds.Length).CopyTo(eosTokenIds);
        }

        return new WinMLGenAIConfig(
            configPath,
            modelDirectory,
            PtrToStringUni(nativeConfig.DecoderFilename),
            PtrToStringUni(nativeConfig.EncoderFilename),
            nativeConfig.VocabSize,
            nativeConfig.ContextLength,
            nativeConfig.Temperature,
            nativeConfig.TopK,
            nativeConfig.TopP,
            nativeConfig.RepetitionPenalty,
            nativeConfig.DoSample != 0,
            nativeConfig.MaxLength,
            eosTokenIds,
            PtrToStringUni(nativeConfig.EmbeddingsFilename),
            PtrToStringUni(nativeConfig.LmHeadFilename),
            PtrToStringUtf8(nativeConfig.EmbeddingsOutputName),
            PtrToStringUtf8(nativeConfig.LmHeadInputName));
    }

    private static string? PtrToStringUni(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUni(ptr);
    }

    private static string? PtrToStringUtf8(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    private static void FreeNative(ref WinMLGenAIConfigNative nativeConfig)
    {
        Marshal.FreeCoTaskMem(nativeConfig.DecoderFilename);
        Marshal.FreeCoTaskMem(nativeConfig.EncoderFilename);
        Marshal.FreeCoTaskMem(nativeConfig.EosTokenIds);
        Marshal.FreeCoTaskMem(nativeConfig.PastKeyPattern);
        Marshal.FreeCoTaskMem(nativeConfig.PastValuePattern);
        Marshal.FreeCoTaskMem(nativeConfig.PresentKeyPattern);
        Marshal.FreeCoTaskMem(nativeConfig.PresentValuePattern);
        Marshal.FreeCoTaskMem(nativeConfig.LogitsOutputName);
        Marshal.FreeCoTaskMem(nativeConfig.PositionIdsInputName);
        Marshal.FreeCoTaskMem(nativeConfig.EmbeddingsFilename);
        Marshal.FreeCoTaskMem(nativeConfig.LmHeadFilename);
        Marshal.FreeCoTaskMem(nativeConfig.EmbeddingsOutputName);
        Marshal.FreeCoTaskMem(nativeConfig.LmHeadInputName);
        nativeConfig = default;
    }
}
#endif
