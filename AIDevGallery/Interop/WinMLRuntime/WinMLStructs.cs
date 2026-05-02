// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

using System;
using System.Runtime.InteropServices;

namespace AIDevGallery.Interop.WinMLRuntime;

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLTensorDesc
{
    public WinMLTensorDataType DataType;
    public uint DimensionCount;
    public unsafe ulong* Dimensions;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLBufferBinding
{
    public IntPtr Resource;
    public ulong OffsetInBytes;
    public ulong SizeInBytes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLRuntimeDesc
{
    public WinMLExecutionPolicy ExecutionPolicy;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLPipelineDesc
{
    public WinMLDeviceType DeviceType;
    public WinMLPipelineFlags Flags;
    public WinMLExecutionPolicy ExecutionPolicy;
    public IntPtr Device;
    public IntPtr CommandQueue;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLStageDesc
{
    public IntPtr Name;
    public WinMLDeviceType DeviceType;
    public IntPtr Device;
    public uint Group;
    public WinMLPipelineFlags Flags;
    public WinMLExecutionPolicy ExecutionPolicy;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLConnectionDesc
{
    public uint SourceOutputIndex;
    public uint TargetInputIndex;
    public uint MaxIterations;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLSamplingDesc
{
    public float Temperature;
    public int TopK;
    public float TopP;
    public float MinP;
    public float RepetitionPenalty;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLGenerationDesc
{
    public ulong MaxLength;
    public WinMLSamplingDesc Sampling;
    public int EnableSampling;
    public uint VocabSize;
    public uint EosTokenIdCount;
    public unsafe uint* EosTokenIds;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLDeviceDesc
{
    public WinMLDeviceType DeviceType;
    public uint AdapterLuidLowPart;
    public int AdapterLuidHighPart;
    public IntPtr D3D12Device;
    public IntPtr D3D12CommandQueue;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinMLGenAIConfigNative
{
    public IntPtr DecoderFilename;
    public IntPtr EncoderFilename;
    public uint NumHiddenLayers;
    public uint NumAttentionHeads;
    public uint NumKeyValueHeads;
    public uint HeadSize;
    public uint HiddenSize;
    public uint BosTokenId;
    public IntPtr EosTokenIds;
    public uint EosTokenIdCount;
    public uint PadTokenId;
    public uint VocabSize;
    public uint ContextLength;
    public float Temperature;
    public uint TopK;
    public float TopP;
    public float RepetitionPenalty;
    public int DoSample;
    public uint MaxLength;
    public int PastPresentShareBuffer;
    public IntPtr PastKeyPattern;
    public IntPtr PastValuePattern;
    public IntPtr PresentKeyPattern;
    public IntPtr PresentValuePattern;
    public IntPtr LogitsOutputName;
    public IntPtr PositionIdsInputName;
    public IntPtr EmbeddingsFilename;
    public IntPtr LmHeadFilename;
    public IntPtr EmbeddingsOutputName;
    public IntPtr LmHeadInputName;
}

#endif