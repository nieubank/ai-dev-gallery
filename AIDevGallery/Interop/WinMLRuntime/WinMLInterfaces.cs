// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace AIDevGallery.Interop.WinMLRuntime;

// ---------- IWinMLTensorDataLock ----------
[GeneratedComInterface]
[Guid("7d2780ed-66b4-4fa5-a935-bae6d28082bd")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLTensorDataLock
{
    unsafe void GetData(out byte* data, out ulong dataSizeInBytes);
}

// ---------- IWinMLTensorSynchronizedDataLock ----------
[GeneratedComInterface]
[Guid("c1754906-62ce-46a2-a2fb-cc13cd10ffcd")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe partial interface IWinMLTensorSynchronizedDataLock : IWinMLTensorDataLock
{
    void Commit();
}

// ---------- IWinMLTensor ----------
[GeneratedComInterface]
[Guid("22f5cf71-27be-4a1b-9087-1a36f7a230ad")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLTensor
{
    void GetDesc(out WinMLTensorDesc desc);
    void GetBufferBinding(out WinMLBufferBinding binding);
    void Lock(WinMLTensorLockMode mode, WinMLTensorLockFlags flags, out IWinMLTensorDataLock dataLock);
}

// ---------- IWinMLMutableTensor ----------
[GeneratedComInterface]
[Guid("a7c3e1d4-6f82-4b59-9e1a-3d8c5f2a7b94")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLMutableTensor
{
    void IndexSelect(IWinMLTensor indices, int axis, out IWinMLTensor output);
    void IndexCopy(IWinMLTensor indices, IWinMLTensor source, int axis);
    void CopyFrom(IWinMLTensor source);
    void Fill(IntPtr value, uint valueSizeInBytes);
}

// ---------- IWinMLModel ----------
[GeneratedComInterface]
[Guid("2453f1b4-81ce-4fd4-99ce-b83136883ff4")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLModel
{
    void GetInputCount(out uint count);

    void GetInputName(uint index, out IntPtr name);

    void GetInputTensorDesc(uint index, out WinMLTensorDesc desc);

    void GetOutputCount(out uint count);

    void GetOutputName(uint index, out IntPtr name);

    void GetOutputTensorDesc(uint index, out WinMLTensorDesc desc);

    void AttachWeightsFromFile([MarshalAs(UnmanagedType.LPWStr)] string resourcesFile);

    unsafe void AttachWeightsFromBuffer(byte* buffer, ulong bufferSizeInBytes);

    void AttachCompilerConfigFromFile([MarshalAs(UnmanagedType.LPWStr)] string patternsFile);

    unsafe void AttachCompilerConfigFromBuffer(byte* buffer, ulong bufferSizeInBytes);
}

// ---------- IWinMLStage ----------
[GeneratedComInterface]
[Guid("88596a2b-24fe-476e-bc90-c37161edb477")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLStage
{
    void GetId(out uint id);

    void GetName(out IntPtr name);

    void BindInput(uint index, IWinMLTensor tensor);
    void BindOutput(uint index, IWinMLTensor tensor);
    void GetOutput(uint index, out IWinMLTensor tensor);
    void GetInputCount(out uint count);
    void GetOutputCount(out uint count);
    void FindInputIndex([MarshalAs(UnmanagedType.LPWStr)] string name, out uint index);
    void FindOutputIndex([MarshalAs(UnmanagedType.LPWStr)] string name, out uint index);
}

// ---------- IWinMLStatefulStage ----------
[GeneratedComInterface]
[Guid("d058a861-844c-435f-b820-2f3dd06754e4")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLStatefulStage
{
    void ResetState();
    void RewindTo(ulong position);
    void SetMaxSequenceLength(ulong maxLength);
    void GetSequencePosition(out ulong position);
}

// ---------- IWinMLPipeline ----------
[GeneratedComInterface]
[Guid("c22e40c2-4e8a-4403-bf98-a0d50ac099c9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLPipeline
{
    void Initialize();
    void Run(uint groupMask, out IntPtr completionFence);
    void RunAfter(IntPtr waitFence, uint groupMask, out IntPtr completionFence);
    void ResetExecutionState();
    void GetStageCount(out uint count);
    void GetStage(uint index, out IWinMLStage stage);
    void GetDevice(out IntPtr device);
}

// ---------- IWinMLPipelineBuilder ----------
[GeneratedComInterface]
[Guid("3bcfcf5a-8b40-4931-9812-d84679123006")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLPipelineBuilder
{
    void AddStage(IWinMLModel model, in WinMLStageDesc desc, out IWinMLStage stage);
    void Connect(IWinMLStage fromStage, IWinMLStage toStage, in WinMLConnectionDesc desc, IntPtr callback, IntPtr callbackContext);
    void AddProcessorStage(IntPtr processor, in WinMLStageDesc desc, out IWinMLStage stage);
    void Build(IntPtr options, out IWinMLPipeline pipeline);
}

// ---------- IWinMLCapabilities ----------
[GeneratedComInterface]
[Guid("b50bdbbd-9d63-4a31-8e7a-618dacfee6f2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLCapabilities
{
    void IsDeviceTypeSupported(WinMLDeviceType deviceType, out int isSupported);
}

// ---------- IWinMLRuntime ----------
[GeneratedComInterface]
[Guid("6954707d-3987-491a-ada9-2bea9b0e13f9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLRuntime
{
    void LoadModelFromFile([MarshalAs(UnmanagedType.LPWStr)] string path, out IWinMLModel model);

    unsafe void LoadModelFromBuffer(byte* buffer, ulong bufferSizeInBytes, out IWinMLModel model);

    void CreateBlobFromFile([MarshalAs(UnmanagedType.LPWStr)] string filePath, out IntPtr blob);

    void CreateFence(IntPtr d3d12Fence, ulong waitValue, out IntPtr fence);

    unsafe void CreateTensor(WinMLTensorDesc* desc, void* initialData, ulong dataSizeInBytes, out IWinMLTensor tensor);

    void CreatePipeline(IWinMLModel model, in WinMLPipelineDesc desc, out IWinMLPipeline pipeline);

    void CreatePipelineBuilder(out IWinMLPipelineBuilder builder);

    void CreateDevice(in WinMLDeviceDesc desc, out IntPtr device);

    void GetCapabilities(out IWinMLCapabilities capabilities);

    void CompileModel(IWinMLModel model, IntPtr device, IntPtr options, out IntPtr compiledModel);

    void LoadCompiledModel(IntPtr blob, IntPtr device, [MarshalAs(UnmanagedType.LPWStr)] string? resourceSearchPath, out IntPtr compiledModel);

    void CreateCompileOptions(out IntPtr options);
}

// ---------- IWinMLTokenizer ----------
[GeneratedComInterface]
[Guid("a7c1d5e3-9f48-4b62-8d1a-3e5c7f2b0a94")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLTokenizer
{
    unsafe void Encode([MarshalAs(UnmanagedType.LPWStr)] string text, out uint tokenCount, out uint* tokenIds);

    unsafe void Decode(uint* tokenIds, uint tokenCount, out IntPtr text);

    void DecodeIncremental(uint tokenId, out IntPtr text);

    void ResetDecodeState();
}

// ---------- IWinMLSampler ----------
[GeneratedComInterface]
[Guid("b8e2f4d6-1a73-4c85-9e3d-7f6a0b5c8d12")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLSampler
{
    unsafe void Sample(float* logits, uint vocabSize, uint* contextTokens, uint contextLength, out uint sampledToken);
    void GetDesc(out WinMLSamplingDesc desc);
    void SetDesc(in WinMLSamplingDesc desc);
    unsafe void SampleFromTensor(IWinMLTensor logitsTensor, uint vocabSize, uint* contextTokens, uint contextLength, out uint sampledToken);
}

// ---------- IWinMLTextGenerator ----------
[GeneratedComInterface]
[Guid("c9f3a5b7-2d84-4e96-af1b-8d7c0e6f3a25")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IWinMLTextGenerator
{
    unsafe void AppendPromptTokens(uint* tokenIds, uint tokenCount);
    void GenerateNextToken(out int manualSamplingRequired, out uint tokenId);
    void GetLastLogitsTensor(out IWinMLTensor logitsTensor, out uint vocabSize);
    void CommitToken(uint tokenId);
    void IsDone(out int done);
    void Reset();
    void RewindTo(ulong position);
    void GetSequencePosition(out ulong position);
    unsafe void GetGeneratedTokens(out uint* tokenIds, out uint tokenCount);
    void SetTokenCallback(IntPtr callback, IntPtr context);
    unsafe void GenerateAll(uint* promptTokenIds, uint promptTokenCount, out uint* generatedTokenIds, out uint generatedTokenCount);
}

#endif