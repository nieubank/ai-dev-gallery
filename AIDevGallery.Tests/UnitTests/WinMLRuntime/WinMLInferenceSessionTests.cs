// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

#pragma warning disable CA1707 // Test method names use underscores by convention
#pragma warning disable SA1518 // File may not end with a newline character
#pragma warning disable SA1512 // Single-line comments should not be followed by blank line
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line

using AIDevGallery.Interop.WinMLRuntime;
using AIDevGallery.Samples.SharedCode;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace AIDevGallery.Tests.UnitTests.WinMLRuntime;

/// <summary>
/// Tests for WinMLInferenceSession and WinMLTensorHelper focusing on type contracts,
/// API surface, and pure data transformation logic.
/// Does NOT require COM runtime — validates structure and public contracts only.
/// </summary>
[TestClass]
public class WinMLInferenceSessionTests
{
    // ── Type contract tests ─────────────────────────────────────────

    [TestMethod]
    public void WinMLInferenceSession_ImplementsIDisposable()
    {
        Assert.IsTrue(typeof(IDisposable).IsAssignableFrom(typeof(WinMLInferenceSession)));
    }

    [TestMethod]
    public void WinMLInferenceSession_IsSealed()
    {
        Assert.IsTrue(typeof(WinMLInferenceSession).IsSealed);
    }

    [TestMethod]
    public void WinMLInferenceSession_HasCreateAsyncFactory()
    {
        var method = typeof(WinMLInferenceSession).GetMethod(
            "CreateAsync",
            BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(method, "CreateAsync should be a public static method");

        var parameters = method.GetParameters();
        Assert.AreEqual(3, parameters.Length, "CreateAsync should take modelPath, deviceType, cancellationToken");
        Assert.AreEqual(typeof(string), parameters[0].ParameterType);
        Assert.AreEqual(typeof(WinMLDeviceType), parameters[1].ParameterType);
    }

    [TestMethod]
    public void WinMLInferenceSession_RunAcceptsDictionaryOfDenseTensors()
    {
        var method = typeof(WinMLInferenceSession).GetMethod(
            "Run",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, "Run method should exist");

        var paramType = method.GetParameters()[0].ParameterType;
        Assert.IsTrue(paramType.IsGenericType);
        Assert.AreEqual(typeof(System.Collections.Generic.Dictionary<,>), paramType.GetGenericTypeDefinition());
    }

    [TestMethod]
    public void WinMLInferenceSession_RunSingleExistsWithCorrectSignature()
    {
        var method = typeof(WinMLInferenceSession).GetMethod(
            "RunSingle",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, "RunSingle method should exist");

        var parameters = method.GetParameters();
        Assert.AreEqual(2, parameters.Length);
        Assert.AreEqual(typeof(string), parameters[0].ParameterType, "First param should be inputName");
        Assert.AreEqual(
            typeof(Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>),
            parameters[1].ParameterType,
            "Second param should be DenseTensor<float>");
        Assert.AreEqual(
            typeof(Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>),
            method.ReturnType,
            "Return should be DenseTensor<float>");
    }

    [TestMethod]
    public void WinMLInferenceSession_HasNameAccessors()
    {
        var getInputName = typeof(WinMLInferenceSession).GetMethod("GetInputName");
        var getOutputName = typeof(WinMLInferenceSession).GetMethod("GetOutputName");
        Assert.IsNotNull(getInputName);
        Assert.IsNotNull(getOutputName);
        Assert.AreEqual(typeof(string), getInputName.ReturnType);
        Assert.AreEqual(typeof(string), getOutputName.ReturnType);
    }

    [TestMethod]
    public void WinMLInferenceSession_HasCountProperties()
    {
        var inputCount = typeof(WinMLInferenceSession).GetProperty("InputCount");
        var outputCount = typeof(WinMLInferenceSession).GetProperty("OutputCount");
        Assert.IsNotNull(inputCount);
        Assert.IsNotNull(outputCount);
        Assert.AreEqual(typeof(uint), inputCount.PropertyType);
        Assert.AreEqual(typeof(uint), outputCount.PropertyType);
    }

    [TestMethod]
    public void WinMLInferenceSession_ExposesStageAndPipeline()
    {
        var stage = typeof(WinMLInferenceSession).GetProperty("Stage");
        var pipeline = typeof(WinMLInferenceSession).GetProperty("Pipeline");
        Assert.IsNotNull(stage, "Stage property should exist for advanced scenarios");
        Assert.IsNotNull(pipeline, "Pipeline property should exist for advanced scenarios");
        Assert.AreEqual(typeof(IWinMLStage), stage.PropertyType);
        Assert.AreEqual(typeof(IWinMLPipeline), pipeline.PropertyType);
    }

    [TestMethod]
    public void WinMLInferenceSession_GetInputTensorDescReturnsDataTypeAndDimensions()
    {
        var method = typeof(WinMLInferenceSession).GetMethod("GetInputTensorDesc");
        Assert.IsNotNull(method);
        // Return type should be a ValueTuple of (WinMLTensorDataType, ulong[])
        var returnType = method.ReturnType;
        Assert.IsTrue(returnType.IsGenericType, "Should return a tuple");
        var genericArgs = returnType.GetGenericArguments();
        Assert.AreEqual(typeof(WinMLTensorDataType), genericArgs[0]);
        Assert.AreEqual(typeof(ulong[]), genericArgs[1]);
    }

    // ── WinMLTensorHelper pure logic tests ──────────────────────────

    [TestMethod]
    public void WinMLTensorHelper_GetDimensions_ExtractsDenseTensorDims()
    {
        // Access the private GetDimensions helper via reflection
        var method = typeof(WinMLTensorHelper).GetMethod(
            "GetDimensions",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "GetDimensions helper should exist");

        var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(new[] { 1, 3, 224, 224 });
        var result = (ulong[])method.Invoke(null, [tensor])!;

        Assert.AreEqual(4, result.Length);
        Assert.AreEqual(1UL, result[0]);
        Assert.AreEqual(3UL, result[1]);
        Assert.AreEqual(224UL, result[2]);
        Assert.AreEqual(224UL, result[3]);
    }

    [TestMethod]
    public void WinMLTensorHelper_GetDimensions_ScalarLikeTensor()
    {
        var method = typeof(WinMLTensorHelper).GetMethod(
            "GetDimensions",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(new[] { 1 });
        var result = (ulong[])method.Invoke(null, [tensor])!;

        Assert.AreEqual(1, result.Length);
        Assert.AreEqual(1UL, result[0]);
    }

    [TestMethod]
    public void WinMLTensorHelper_GetDimensions_LargeRank()
    {
        var method = typeof(WinMLTensorHelper).GetMethod(
            "GetDimensions",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        // 5D tensor (e.g., batch × seq × heads × height × width)
        var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(new[] { 2, 8, 12, 64, 64 });
        var result = (ulong[])method.Invoke(null, [tensor])!;

        Assert.AreEqual(5, result.Length);
        Assert.AreEqual(2UL, result[0]);
        Assert.AreEqual(8UL, result[1]);
        Assert.AreEqual(12UL, result[2]);
        Assert.AreEqual(64UL, result[3]);
        Assert.AreEqual(64UL, result[4]);
    }

    // ── WinMLRuntimeWrapper type contract ───────────────────────────

    [TestMethod]
    public void WinMLRuntimeWrapper_IsSealed()
    {
        Assert.IsTrue(typeof(WinMLRuntimeWrapper).IsSealed);
    }

    [TestMethod]
    public void WinMLRuntimeWrapper_ImplementsIDisposable()
    {
        Assert.IsTrue(typeof(IDisposable).IsAssignableFrom(typeof(WinMLRuntimeWrapper)));
    }

    [TestMethod]
    public void WinMLRuntimeWrapper_HasExpectedFactoryMethods()
    {
        var createMethod = typeof(WinMLRuntimeWrapper).GetMethod(
            "Create",
            BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(createMethod, "Create factory method should exist");

        var loadModel = typeof(WinMLRuntimeWrapper).GetMethod("LoadModelFromFile");
        Assert.IsNotNull(loadModel, "LoadModelFromFile should exist");

        var createPipeline = typeof(WinMLRuntimeWrapper).GetMethod("CreatePipeline");
        Assert.IsNotNull(createPipeline, "CreatePipeline should exist");

        var createBuilder = typeof(WinMLRuntimeWrapper).GetMethod("CreatePipelineBuilder");
        Assert.IsNotNull(createBuilder, "CreatePipelineBuilder should exist");
    }

    // ── WinMLTokenizerWrapper type contract ─────────────────────────

    [TestMethod]
    public void WinMLTokenizerWrapper_IsSealed()
    {
        Assert.IsTrue(typeof(WinMLTokenizerWrapper).IsSealed);
    }

    [TestMethod]
    public void WinMLTokenizerWrapper_HasEncodeDecodeDecodeIncremental()
    {
        var encode = typeof(WinMLTokenizerWrapper).GetMethod("Encode");
        var decode = typeof(WinMLTokenizerWrapper).GetMethod("Decode");
        var decodeInc = typeof(WinMLTokenizerWrapper).GetMethod("DecodeIncremental");
        var reset = typeof(WinMLTokenizerWrapper).GetMethod("ResetDecodeState");

        Assert.IsNotNull(encode);
        Assert.IsNotNull(decode);
        Assert.IsNotNull(decodeInc);
        Assert.IsNotNull(reset);

        Assert.AreEqual(typeof(int[]), encode.ReturnType, "Encode should return int[]");
        Assert.AreEqual(typeof(string), decode.ReturnType, "Decode should return string");
        Assert.AreEqual(typeof(string), decodeInc.ReturnType, "DecodeIncremental should return string");
    }

    // ── WinMLTextGeneratorWrapper type contract ─────────────────────

    [TestMethod]
    public void WinMLTextGeneratorWrapper_IsSealed()
    {
        Assert.IsTrue(typeof(WinMLTextGeneratorWrapper).IsSealed);
    }

    [TestMethod]
    public void WinMLTextGeneratorWrapper_HasGenerationMethods()
    {
        var appendPrompt = typeof(WinMLTextGeneratorWrapper).GetMethod("AppendPromptTokens");
        var generateNext = typeof(WinMLTextGeneratorWrapper).GetMethod("GenerateNextToken");
        var isDone = typeof(WinMLTextGeneratorWrapper).GetMethod("IsDone");
        var reset = typeof(WinMLTextGeneratorWrapper).GetMethod("Reset");
        var generateAll = typeof(WinMLTextGeneratorWrapper).GetMethod("GenerateAll");

        Assert.IsNotNull(appendPrompt);
        Assert.IsNotNull(generateNext);
        Assert.IsNotNull(isDone);
        Assert.IsNotNull(reset);
        Assert.IsNotNull(generateAll);

        Assert.AreEqual(typeof(int), generateNext.ReturnType, "GenerateNextToken should return int");
        Assert.AreEqual(typeof(bool), isDone.ReturnType, "IsDone should return bool");
        Assert.AreEqual(typeof(int[]), generateAll.ReturnType, "GenerateAll should return int[]");
    }
}

#endif
