// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

#pragma warning disable SA1518 // File may not end with a newline character

using System;
using System.Runtime.InteropServices;

namespace AIDevGallery.Interop.WinMLRuntime;

internal static partial class WinMLNativeMethods
{
    [LibraryImport("WinMLRuntime.dll")]
    internal static unsafe partial int WinMLCreateRuntime(
        WinMLRuntimeDesc* desc,
        in Guid riid,
        out IntPtr runtime);

    [LibraryImport("WinMLTokenizer.dll", EntryPoint = "WinMLCreateTokenizerFromFile", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int WinMLCreateTokenizerFromFile(
        string configPath,
        out IntPtr tokenizer);

    [LibraryImport("WinMLTextGeneration.dll")]
    internal static partial int WinMLCreateSampler(
        in WinMLSamplingDesc desc,
        out IntPtr sampler);

    [LibraryImport("WinMLTextGeneration.dll", EntryPoint = "WinMLParseGenAIConfigFromFile", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int WinMLParseGenAIConfigFromFile(
        string configFilePath,
        out WinMLGenAIConfigNative config);

    [LibraryImport("WinMLTextGeneration.dll", EntryPoint = "WinMLParseGenAIConfig", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int WinMLParseGenAIConfig(
        string configFilePath,
        out WinMLGenAIConfigNative config);

    [LibraryImport("WinMLTextGeneration.dll")]
    internal static partial int WinMLCreateTextGenerator(
        IntPtr pipeline,
        in WinMLGenerationDesc desc,
        IntPtr tokenizer,
        IntPtr sampler,
        out IntPtr generator);
}
#endif
