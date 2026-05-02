// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AIDevGallery.Utils;

/// <summary>
/// Determines which inference runtime the app uses for EP acquisition.
/// FoundryLocal and WinML each bundle their own onnxruntime.dll and cannot coexist.
/// </summary>
internal enum InferenceBackend
{
    /// <summary>
    /// Use the WinML ExecutionProviderCatalog for EP registration (default).
    /// </summary>
    WinML,

    /// <summary>
    /// Use Foundry Local SDK for model loading and EP provisioning.
    /// </summary>
    FoundryLocal
}