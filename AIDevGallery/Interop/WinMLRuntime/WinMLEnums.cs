// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

using System;

namespace AIDevGallery.Interop.WinMLRuntime;

internal enum WinMLTensorDataType
{
    Undefined = 0,
    Float32 = 1,
    Float16 = 2,
    BFloat16 = 3,
    Int8 = 4,
    UInt8 = 5,
    Int16 = 6,
    UInt16 = 7,
    Int32 = 8,
    UInt32 = 9,
    Int64 = 10,
    UInt64 = 11,
    Float64 = 12,
    Bool = 13,
    Int4 = 14,
    UInt4 = 15
}

internal enum WinMLExecutionPolicy
{
    Default = 0,
    PreferEfficiency = 1,
    PreferPerformance = 2
}

internal enum WinMLDeviceType
{
    Default = 0,
    CPU = 1,
    GPU = 2,
    NPU = 3
}

internal enum WinMLTensorLockMode
{
    Read = 1,
    Write = 2,
    ReadWrite = 3
}

[Flags]
internal enum WinMLTensorLockFlags
{
    None = 0,
    AllowSynchronizedCpuAccess = 0x1
}

[Flags]
internal enum WinMLPipelineFlags
{
    None = 0x0,
    PreferLowLatency = 0x1,
    PreferLowPower = 0x2,
    ForceSoftwareDevice = 0x4
}

[Flags]
internal enum WinMLModelSerializeFlags
{
    Default = 0x0,
    EmbedWeights = 0x1
}

#endif