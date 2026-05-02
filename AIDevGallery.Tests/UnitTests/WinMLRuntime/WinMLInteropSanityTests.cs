// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

#pragma warning disable CA1707 // Test method names use underscores by convention
#pragma warning disable MSTEST0032 // Enum value assertions validate IDL contract compliance
#pragma warning disable SA1518 // File may not end with a newline character
#pragma warning disable SA1512 // Single-line comments should not be followed by blank line
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line

using AIDevGallery.Interop.WinMLRuntime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.InteropServices;

namespace AIDevGallery.Tests.UnitTests.WinMLRuntime;

/// <summary>
/// Validates that C# COM interop definitions (enums, structs, GUIDs) match the
/// WinMLRuntime.idl contract. Incorrect values cause hard-to-debug runtime crashes
/// rather than compile errors, so these tests serve as a safety net.
/// </summary>
[TestClass]
public class WinMLInteropSanityTests
{
    // ── Enum value tests ────────────────────────────────────────────────

    [TestMethod]
    public void WinMLTensorDataType_ValuesMatchIdl()
    {
        Assert.AreEqual(0, (int)WinMLTensorDataType.Undefined);
        Assert.AreEqual(1, (int)WinMLTensorDataType.Float32);
        Assert.AreEqual(2, (int)WinMLTensorDataType.Float16);
        Assert.AreEqual(3, (int)WinMLTensorDataType.BFloat16);
        Assert.AreEqual(4, (int)WinMLTensorDataType.Int8);
        Assert.AreEqual(5, (int)WinMLTensorDataType.UInt8);
        Assert.AreEqual(6, (int)WinMLTensorDataType.Int16);
        Assert.AreEqual(7, (int)WinMLTensorDataType.UInt16);
        Assert.AreEqual(8, (int)WinMLTensorDataType.Int32);
        Assert.AreEqual(9, (int)WinMLTensorDataType.UInt32);
        Assert.AreEqual(10, (int)WinMLTensorDataType.Int64);
        Assert.AreEqual(11, (int)WinMLTensorDataType.UInt64);
        Assert.AreEqual(12, (int)WinMLTensorDataType.Float64);
        Assert.AreEqual(13, (int)WinMLTensorDataType.Bool);
        Assert.AreEqual(14, (int)WinMLTensorDataType.Int4);
        Assert.AreEqual(15, (int)WinMLTensorDataType.UInt4);
    }

    [TestMethod]
    public void WinMLExecutionPolicy_ValuesMatchIdl()
    {
        Assert.AreEqual(0, (int)WinMLExecutionPolicy.Default);
        Assert.AreEqual(1, (int)WinMLExecutionPolicy.PreferEfficiency);
        Assert.AreEqual(2, (int)WinMLExecutionPolicy.PreferPerformance);
    }

    [TestMethod]
    public void WinMLDeviceType_ValuesMatchIdl()
    {
        Assert.AreEqual(0, (int)WinMLDeviceType.Default);
        Assert.AreEqual(1, (int)WinMLDeviceType.CPU);
        Assert.AreEqual(2, (int)WinMLDeviceType.GPU);
        Assert.AreEqual(3, (int)WinMLDeviceType.NPU);
    }

    [TestMethod]
    public void WinMLTensorLockMode_ValuesMatchIdl()
    {
        Assert.AreEqual(1, (int)WinMLTensorLockMode.Read);
        Assert.AreEqual(2, (int)WinMLTensorLockMode.Write);
        Assert.AreEqual(3, (int)WinMLTensorLockMode.ReadWrite);
    }

    [TestMethod]
    public void WinMLTensorLockFlags_ValuesMatchIdl()
    {
        Assert.AreEqual(0x0, (int)WinMLTensorLockFlags.None);
        Assert.AreEqual(0x1, (int)WinMLTensorLockFlags.AllowSynchronizedCpuAccess);
    }

    [TestMethod]
    public void WinMLPipelineFlags_ValuesMatchIdl()
    {
        Assert.AreEqual(0x0, (int)WinMLPipelineFlags.None);
        Assert.AreEqual(0x1, (int)WinMLPipelineFlags.PreferLowLatency);
        Assert.AreEqual(0x2, (int)WinMLPipelineFlags.PreferLowPower);
        Assert.AreEqual(0x4, (int)WinMLPipelineFlags.ForceSoftwareDevice);
    }

    [TestMethod]
    public void WinMLPipelineFlags_AreCombinableFlags()
    {
        var combined = WinMLPipelineFlags.PreferLowLatency | WinMLPipelineFlags.PreferLowPower;
        Assert.AreEqual(0x3, (int)combined);
        Assert.IsTrue(combined.HasFlag(WinMLPipelineFlags.PreferLowLatency));
        Assert.IsTrue(combined.HasFlag(WinMLPipelineFlags.PreferLowPower));
        Assert.IsFalse(combined.HasFlag(WinMLPipelineFlags.ForceSoftwareDevice));
    }

    [TestMethod]
    public void WinMLModelSerializeFlags_ValuesMatchIdl()
    {
        Assert.AreEqual(0x0, (int)WinMLModelSerializeFlags.Default);
        Assert.AreEqual(0x1, (int)WinMLModelSerializeFlags.EmbedWeights);
    }

    // ── Struct layout tests ─────────────────────────────────────────────

    [TestMethod]
    public void WinMLTensorDesc_LayoutIsSequential()
    {
        // TypeAttributes.SequentialLayout is the reliable way to verify LayoutKind.Sequential
        Assert.IsTrue(
            typeof(WinMLTensorDesc).IsLayoutSequential,
            "WinMLTensorDesc should have sequential layout");
    }

    [TestMethod]
    public unsafe void WinMLTensorDesc_FieldOffsetsAreCorrect()
    {
        // DataType (4 bytes) + DimensionCount (4 bytes) + Dimensions ptr (8 bytes on x64)
        Assert.AreEqual(0, Marshal.OffsetOf<WinMLTensorDesc>(nameof(WinMLTensorDesc.DataType)).ToInt32());
        Assert.AreEqual(4, Marshal.OffsetOf<WinMLTensorDesc>(nameof(WinMLTensorDesc.DimensionCount)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<WinMLTensorDesc>(nameof(WinMLTensorDesc.Dimensions)).ToInt32());
    }

    [TestMethod]
    public void WinMLRuntimeDesc_LayoutIsSequential()
    {
        Assert.IsTrue(
            typeof(WinMLRuntimeDesc).IsLayoutSequential,
            "WinMLRuntimeDesc should have sequential layout");
        // Single field: ExecutionPolicy (4 bytes enum)
        Assert.AreEqual(0, Marshal.OffsetOf<WinMLRuntimeDesc>(nameof(WinMLRuntimeDesc.ExecutionPolicy)).ToInt32());
    }

    [TestMethod]
    public void WinMLPipelineDesc_FieldOffsetsAreCorrect()
    {
        Assert.AreEqual(0, Marshal.OffsetOf<WinMLPipelineDesc>(nameof(WinMLPipelineDesc.DeviceType)).ToInt32());
        Assert.AreEqual(4, Marshal.OffsetOf<WinMLPipelineDesc>(nameof(WinMLPipelineDesc.Flags)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<WinMLPipelineDesc>(nameof(WinMLPipelineDesc.ExecutionPolicy)).ToInt32());
        // Device ptr at offset 16 (8-byte aligned after 12 bytes of enums + 4 padding)
        Assert.AreEqual(16, Marshal.OffsetOf<WinMLPipelineDesc>(nameof(WinMLPipelineDesc.Device)).ToInt32());
        Assert.AreEqual(24, Marshal.OffsetOf<WinMLPipelineDesc>(nameof(WinMLPipelineDesc.CommandQueue)).ToInt32());
    }

    [TestMethod]
    public void WinMLConnectionDesc_FieldOffsetsAreCorrect()
    {
        Assert.AreEqual(0, Marshal.OffsetOf<WinMLConnectionDesc>(nameof(WinMLConnectionDesc.SourceOutputIndex)).ToInt32());
        Assert.AreEqual(4, Marshal.OffsetOf<WinMLConnectionDesc>(nameof(WinMLConnectionDesc.TargetInputIndex)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<WinMLConnectionDesc>(nameof(WinMLConnectionDesc.MaxIterations)).ToInt32());
    }

    [TestMethod]
    public void WinMLSamplingDesc_FieldOffsetsAreCorrect()
    {
        Assert.AreEqual(0, Marshal.OffsetOf<WinMLSamplingDesc>(nameof(WinMLSamplingDesc.Temperature)).ToInt32());
        Assert.AreEqual(4, Marshal.OffsetOf<WinMLSamplingDesc>(nameof(WinMLSamplingDesc.TopK)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<WinMLSamplingDesc>(nameof(WinMLSamplingDesc.TopP)).ToInt32());
        Assert.AreEqual(12, Marshal.OffsetOf<WinMLSamplingDesc>(nameof(WinMLSamplingDesc.MinP)).ToInt32());
        Assert.AreEqual(16, Marshal.OffsetOf<WinMLSamplingDesc>(nameof(WinMLSamplingDesc.RepetitionPenalty)).ToInt32());
    }

    [TestMethod]
    public void WinMLGenerationDesc_ContainsSamplingDescEmbedded()
    {
        Assert.AreEqual(0, Marshal.OffsetOf<WinMLGenerationDesc>(nameof(WinMLGenerationDesc.MaxLength)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<WinMLGenerationDesc>(nameof(WinMLGenerationDesc.Sampling)).ToInt32());
        var enableSamplingOffset = Marshal.OffsetOf<WinMLGenerationDesc>(nameof(WinMLGenerationDesc.EnableSampling)).ToInt32();
        var vocabSizeOffset = Marshal.OffsetOf<WinMLGenerationDesc>(nameof(WinMLGenerationDesc.VocabSize)).ToInt32();
        var eosTokenIdCountOffset = Marshal.OffsetOf<WinMLGenerationDesc>(nameof(WinMLGenerationDesc.EosTokenIdCount)).ToInt32();
        var eosTokenIdsOffset = Marshal.OffsetOf<WinMLGenerationDesc>(nameof(WinMLGenerationDesc.EosTokenIds)).ToInt32();
        // EnableSampling follows Sampling (offset 8 + 20 = 28)
        Assert.AreEqual(28, enableSamplingOffset);
        Assert.AreEqual(32, vocabSizeOffset);
        Assert.AreEqual(36, eosTokenIdCountOffset);
        Assert.AreEqual(40, eosTokenIdsOffset);
    }

    [TestMethod]
    public void WinMLDeviceDesc_FieldOffsetsAreCorrect()
    {
        Assert.AreEqual(0, Marshal.OffsetOf<WinMLDeviceDesc>(nameof(WinMLDeviceDesc.DeviceType)).ToInt32());
        // LUID: LowPart (DWORD) + HighPart (LONG) — 8 bytes total after DeviceType (4 bytes)
        Assert.AreEqual(4, Marshal.OffsetOf<WinMLDeviceDesc>(nameof(WinMLDeviceDesc.AdapterLuidLowPart)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<WinMLDeviceDesc>(nameof(WinMLDeviceDesc.AdapterLuidHighPart)).ToInt32());
        // D3D12Device ptr after 12 bytes of value types → aligned to 16
        Assert.AreEqual(16, Marshal.OffsetOf<WinMLDeviceDesc>(nameof(WinMLDeviceDesc.D3D12Device)).ToInt32());
        Assert.AreEqual(24, Marshal.OffsetOf<WinMLDeviceDesc>(nameof(WinMLDeviceDesc.D3D12CommandQueue)).ToInt32());
    }

    // ── COM GUID tests ──────────────────────────────────────────────

    [TestMethod]
    public void IWinMLRuntime_GuidMatchesIdl()
    {
        AssertGuid<IWinMLRuntime>("6954707d-3987-491a-ada9-2bea9b0e13f9");
    }

    [TestMethod]
    public void IWinMLModel_GuidMatchesIdl()
    {
        AssertGuid<IWinMLModel>("2453f1b4-81ce-4fd4-99ce-b83136883ff4");
    }

    [TestMethod]
    public void IWinMLStage_GuidMatchesIdl()
    {
        AssertGuid<IWinMLStage>("88596a2b-24fe-476e-bc90-c37161edb477");
    }

    [TestMethod]
    public void IWinMLTensorDataLock_GuidMatchesIdl()
    {
        AssertGuid<IWinMLTensorDataLock>("7d2780ed-66b4-4fa5-a935-bae6d28082bd");
    }

    [TestMethod]
    public void IWinMLTensorSynchronizedDataLock_GuidMatchesIdl()
    {
        AssertGuid<IWinMLTensorSynchronizedDataLock>("c1754906-62ce-46a2-a2fb-cc13cd10ffcd");
    }

    [TestMethod]
    public void IWinMLStatefulStage_GuidMatchesIdl()
    {
        AssertGuid<IWinMLStatefulStage>("d058a861-844c-435f-b820-2f3dd06754e4");
    }

    [TestMethod]
    public void IWinMLPipeline_GuidMatchesIdl()
    {
        AssertGuid<IWinMLPipeline>("c22e40c2-4e8a-4403-bf98-a0d50ac099c9");
    }

    [TestMethod]
    public void IWinMLPipelineBuilder_GuidMatchesIdl()
    {
        AssertGuid<IWinMLPipelineBuilder>("3bcfcf5a-8b40-4931-9812-d84679123006");
    }

    [TestMethod]
    public void IWinMLCapabilities_GuidMatchesIdl()
    {
        AssertGuid<IWinMLCapabilities>("b50bdbbd-9d63-4a31-8e7a-618dacfee6f2");
    }

    [TestMethod]
    public void IWinMLTensor_GuidMatchesIdl()
    {
        AssertGuid<IWinMLTensor>("22f5cf71-27be-4a1b-9087-1a36f7a230ad");
    }

    [TestMethod]
    public void IWinMLMutableTensor_GuidMatchesIdl()
    {
        AssertGuid<IWinMLMutableTensor>("a7c3e1d4-6f82-4b59-9e1a-3d8c5f2a7b94");
    }

    [TestMethod]
    public void IWinMLTokenizer_GuidMatchesIdl()
    {
        AssertGuid<IWinMLTokenizer>("a7c1d5e3-9f48-4b62-8d1a-3e5c7f2b0a94");
    }

    [TestMethod]
    public void IWinMLSampler_GuidMatchesIdl()
    {
        AssertGuid<IWinMLSampler>("b8e2f4d6-1a73-4c85-9e3d-7f6a0b5c8d12");
    }

    [TestMethod]
    public void IWinMLTextGenerator_GuidMatchesIdl()
    {
        AssertGuid<IWinMLTextGenerator>("c9f3a5b7-2d84-4e96-af1b-8d7c0e6f3a25");
    }

    // ── COM interface vtable slot count tests ───────────────────────

    [TestMethod]
    public void IWinMLRuntime_HasExpectedMethodCount()
    {
        // IUnknown (3) + 12 methods (LoadModelFromFile..CreateCompileOptions)
        AssertInterfaceMethodCount<IWinMLRuntime>(12);
    }

    [TestMethod]
    public void IWinMLModel_HasExpectedMethodCount()
    {
        // IUnknown (3) + 10 methods
        AssertInterfaceMethodCount<IWinMLModel>(10);
    }

    [TestMethod]
    public void IWinMLStage_HasExpectedMethodCount()
    {
        // IUnknown (3) + 9 methods
        AssertInterfaceMethodCount<IWinMLStage>(9);
    }

    [TestMethod]
    public void IWinMLPipeline_HasExpectedMethodCount()
    {
        // IUnknown (3) + 7 methods
        AssertInterfaceMethodCount<IWinMLPipeline>(7);
    }

    [TestMethod]
    public void IWinMLPipelineBuilder_HasExpectedMethodCount()
    {
        // IUnknown (3) + 4 methods
        AssertInterfaceMethodCount<IWinMLPipelineBuilder>(4);
    }

    [TestMethod]
    public void IWinMLStatefulStage_HasExpectedMethodCount()
    {
        // IUnknown (3) + 4 methods
        AssertInterfaceMethodCount<IWinMLStatefulStage>(4);
    }

    [TestMethod]
    public void IWinMLTensor_HasExpectedMethodCount()
    {
        // IUnknown (3) + 3 methods
        AssertInterfaceMethodCount<IWinMLTensor>(3);
    }

    [TestMethod]
    public void IWinMLTensorDataLock_HasExpectedMethodCount()
    {
        // IUnknown (3) + 1 method
        AssertInterfaceMethodCount<IWinMLTensorDataLock>(1);
    }

    [TestMethod]
    public void IWinMLTokenizer_HasExpectedMethodCount()
    {
        // IUnknown (3) + 4 methods
        AssertInterfaceMethodCount<IWinMLTokenizer>(4);
    }

    [TestMethod]
    public void IWinMLTextGenerator_HasExpectedMethodCount()
    {
        // IUnknown (3) + 11 methods
        AssertInterfaceMethodCount<IWinMLTextGenerator>(11);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static void AssertGuid<T>(string expected)
    {
        var attr = (GuidAttribute?)typeof(T).GetCustomAttributes(typeof(GuidAttribute), false)[0];
        Assert.IsNotNull(attr, $"GuidAttribute missing on {typeof(T).Name}");
        Assert.AreEqual(
            new Guid(expected),
            new Guid(attr.Value),
            $"GUID mismatch for {typeof(T).Name}");
    }

    private static void AssertInterfaceMethodCount<T>(int expectedCount)
    {
        var methods = typeof(T).GetMethods(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.DeclaredOnly);
        Assert.AreEqual(
            expectedCount,
            methods.Length,
            $"{typeof(T).Name}: expected {expectedCount} declared methods but found {methods.Length}");
    }
}

#endif
