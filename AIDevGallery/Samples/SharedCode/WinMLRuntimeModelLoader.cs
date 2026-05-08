// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

#pragma warning disable SYSLIB1099 // Experimental WinML interop uses runtime-based COM marshalling with [GeneratedComInterface] types
#pragma warning disable SA1518 // File may not end with a newline character

using AIDevGallery.Interop.WinMLRuntime;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AIDevGallery.Samples.SharedCode;

internal static class WinMLRuntimeModelLoader
{
    public static IWinMLModel LoadModelWithExternalData(WinMLRuntimeWrapper runtime, string modelPath)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        var model = runtime.LoadModelFromFile(modelPath);
        try
        {
            AttachExternalDataIfPresent(model, modelPath);
            return model;
        }
        catch
        {
            Marshal.FinalReleaseComObject(model);
            throw;
        }
    }

    public static void AttachExternalDataIfPresent(IWinMLModel model, string modelPath)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        var resourceFile = GetExternalDataFileForModel(modelPath);
        if (resourceFile is not null)
        {
            model.AttachWeightsFromFile(resourceFile);
        }
    }

    internal static string? GetExternalDataFileForModel(string modelPath)
    {
        var resourceFile = modelPath + ".data";
        return File.Exists(resourceFile) ? resourceFile : null;
    }
}
#endif
