// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#if WINML_RUNTIME_EXPERIMENTAL

#pragma warning disable CA1707 // Test method names use underscores by convention
#pragma warning disable CA1310 // Specify StringComparison for correctness
#pragma warning disable CA2263 // Prefer generic overload
#pragma warning disable MSTEST0039 // Use Assert.ThrowsExactly
#pragma warning disable SA1518 // File may not end with a newline character
#pragma warning disable SA1512 // Single-line comments should not be followed by blank line
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line

using AIDevGallery.Interop.WinMLRuntime;
using AIDevGallery.Samples.SharedCode;
using Microsoft.Extensions.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AIDevGallery.Tests.UnitTests.WinMLRuntime;

/// <summary>
/// Tests for WinMLRuntimeChatClient focusing on pure functions (prompt formatting)
/// that don't require COM runtime initialization.
/// Mirrors the pattern in FoundryLocalChatClientAdapterTests.
/// </summary>
[TestClass]
public class WinMLRuntimeChatClientTests
{
    // ── FormatPrompt tests ──────────────────────────────────────────

    [TestMethod]
    public void FormatPrompt_WithNullTemplate_ConcatenatesMessages()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello"),
            new(ChatRole.User, "World")
        };

        var result = InvokeFormatPrompt(messages, promptTemplate: null);

        Assert.AreEqual("Hello. World", result);
    }

    [TestMethod]
    public void FormatPrompt_WithTemplate_FormatsSystemAndUser()
    {
        var template = new LlmPromptTemplate
        {
            System = "<|system|>\n{{CONTENT}}<|end|>\n",
            User = "<|user|>\n{{CONTENT}}<|end|>\n",
            Assistant = "<|assistant|>\n{{CONTENT}}<|end|>\n"
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are helpful."),
            new(ChatRole.User, "How are you?")
        };

        var result = InvokeFormatPrompt(messages, template);

        Assert.IsTrue(result.Contains("<|system|>\nYou are helpful.<|end|>"), "System message not formatted correctly");
        Assert.IsTrue(result.Contains("<|user|>\nHow are you?<|end|>"), "User message not formatted correctly");
        // Should end with the assistant preamble (before {{CONTENT}})
        Assert.IsTrue(result.EndsWith("<|assistant|>\n"), "Should end with assistant preamble");
    }

    [TestMethod]
    public void FormatPrompt_WithTemplate_IncludesAssistantHistory()
    {
        var template = new LlmPromptTemplate
        {
            User = "<|user|>{{CONTENT}}<|end|>",
            Assistant = "<|assistant|>{{CONTENT}}<|end|>"
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "First question"),
            new(ChatRole.Assistant, "First answer"),
            new(ChatRole.User, "Second question")
        };

        var result = InvokeFormatPrompt(messages, template);

        Assert.IsTrue(result.Contains("<|user|>First question<|end|>"));
        Assert.IsTrue(result.Contains("<|assistant|>First answer<|end|>"));
        Assert.IsTrue(result.Contains("<|user|>Second question<|end|>"));
    }

    [TestMethod]
    public void FormatPrompt_WithTemplate_NoSystemTemplate_SkipsSystem()
    {
        var template = new LlmPromptTemplate
        {
            System = null,
            User = "[user]{{CONTENT}}[/user]",
            Assistant = "[assistant]{{CONTENT}}[/assistant]"
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "I should be ignored"),
            new(ChatRole.User, "Hello")
        };

        var result = InvokeFormatPrompt(messages, template);

        Assert.IsFalse(result.Contains("I should be ignored"), "System message should be skipped when System template is null");
        Assert.IsTrue(result.Contains("[user]Hello[/user]"));
    }

    [TestMethod]
    public void FormatPrompt_SystemMessageOnlyFirstMessage()
    {
        // System template should only apply to the first message, even if it's ChatRole.System
        var template = new LlmPromptTemplate
        {
            System = "<SYS>{{CONTENT}}</SYS>",
            User = "<USR>{{CONTENT}}</USR>",
            Assistant = "<AST>{{CONTENT}}</AST>"
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Not system at index 0"),
            new(ChatRole.System, "System at index 1 treated as user")
        };

        var result = InvokeFormatPrompt(messages, template);

        // First message is User role (index 0), so System template should NOT apply
        Assert.IsTrue(result.Contains("<USR>Not system at index 0</USR>"));
        // System at index 1 should not use System template (only index 0 gets system treatment)
    }

    [TestMethod]
    public void FormatPrompt_EmptyMessages_ReturnsAssistantPreamble()
    {
        var template = new LlmPromptTemplate
        {
            Assistant = "<|assistant|>\n{{CONTENT}}<|end|>\n"
        };

        var messages = new List<ChatMessage>();

        var result = InvokeFormatPrompt(messages, template);

        Assert.AreEqual("<|assistant|>\n", result);
    }

    [TestMethod]
    public void FormatPrompt_NullTemplate_EmptyMessages_ReturnsEmpty()
    {
        var messages = new List<ChatMessage>();
        var result = InvokeFormatPrompt(messages, promptTemplate: null);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void FormatPrompt_WithNullUserText_DoesNotThrow()
    {
        var template = new LlmPromptTemplate
        {
            User = "<|user|>{{CONTENT}}<|end|>",
            Assistant = "<|assistant|>{{CONTENT}}<|end|>"
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, (string?)null)
        };

        // Should not throw NullReferenceException
        var result = InvokeFormatPrompt(messages, template);
        Assert.IsNotNull(result);
    }

    // ── Metadata tests ──────────────────────────────────────────────

    [TestMethod]
    public void ChatClientMetadata_PropertyExists()
    {
        // Verify via reflection since we can't construct without COM runtime
        var metadataProp = typeof(WinMLRuntimeChatClient).GetProperty(
            "Metadata",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(metadataProp, "Metadata property should exist");
        Assert.AreEqual(typeof(ChatClientMetadata), metadataProp.PropertyType);
    }

    [TestMethod]
    public void WinMLRuntimeChatClient_ImplementsIChatClient()
    {
        Assert.IsTrue(typeof(IChatClient).IsAssignableFrom(typeof(WinMLRuntimeChatClient)));
    }

    [TestMethod]
    public void WinMLRuntimeChatClient_ImplementsIDisposable()
    {
        Assert.IsTrue(typeof(System.IDisposable).IsAssignableFrom(typeof(WinMLRuntimeChatClient)));
    }

    [TestMethod]
    public void WinMLRuntimeChatClient_HasExpectedPublicMethods()
    {
        var publicMethods = typeof(WinMLRuntimeChatClient)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet();

        Assert.IsTrue(publicMethods.Contains("GetResponseAsync"), "Missing GetResponseAsync");
        Assert.IsTrue(publicMethods.Contains("GetStreamingResponseAsync"), "Missing GetStreamingResponseAsync");
        Assert.IsTrue(publicMethods.Contains("GetService"), "Missing GetService");
        Assert.IsTrue(publicMethods.Contains("Dispose"), "Missing Dispose");
    }

    [TestMethod]
    public void WinMLRuntimeChatClient_CreateAsyncIsStaticFactory()
    {
        var createMethod = typeof(WinMLRuntimeChatClient).GetMethod(
            "CreateAsync",
            BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(createMethod, "CreateAsync should be a public static method");
        Assert.AreEqual(typeof(System.Threading.Tasks.Task<WinMLRuntimeChatClient>), createMethod.ReturnType);
    }

    // ── GenAI config parser tests ───────────────────────────────────

    [TestMethod]
    public void WinMLGenAIConfig_LoadFromDirectory_ParsesRuntimeConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "genai_config.json"),
                """
                {
                  "model": {
                    "bos_token_id": 1,
                    "context_length": 4096,
                    "decoder": {
                      "filename": "decoder.onnx",
                      "head_size": 96,
                      "hidden_size": 3072,
                      "inputs": {
                        "past_key_names": "past_key_values.%d.key",
                        "past_value_names": "past_key_values.%d.value"
                      },
                      "outputs": {
                        "logits": "logits",
                        "present_key_names": "present.%d.key",
                        "present_value_names": "present.%d.value"
                      },
                      "num_attention_heads": 32,
                      "num_hidden_layers": 32,
                      "num_key_value_heads": 32
                    },
                    "eos_token_id": [32000, 32001, 32007],
                    "pad_token_id": 32000,
                    "vocab_size": 32064
                  },
                  "search": {
                    "do_sample": false,
                    "max_length": 4096,
                    "repetition_penalty": 1.0,
                    "temperature": 1.0,
                    "top_k": 1,
                    "top_p": 1.0
                  }
                }
                """);

            var config = WinMLGenAIConfig.LoadFromDirectory(tempDir);

            Assert.AreEqual("decoder.onnx", config.DecoderFilename);
            Assert.AreEqual(4096u, config.ContextLength);
            Assert.AreEqual(4096u, config.MaxLength);
            Assert.AreEqual(32064u, config.VocabSize);
            CollectionAssert.AreEqual(new uint[] { 32000, 32001, 32007 }, config.EosTokenIds);
            Assert.IsFalse(config.DoSample);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── Helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Invokes the private FormatPrompt instance method via reflection.
    /// Creates a minimal WinMLRuntimeChatClient shell using reflection to bypass COM initialization.
    /// </summary>
    private static string InvokeFormatPrompt(List<ChatMessage> messages, LlmPromptTemplate? promptTemplate)
    {
        // FormatPrompt is private instance, so we need an instance.
        // Use RuntimeHelpers to create without calling constructor (avoids COM initialization).
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(WinMLRuntimeChatClient));

        // Set the _promptTemplate field
        var templateField = typeof(WinMLRuntimeChatClient).GetField(
            "_promptTemplate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(templateField, "_promptTemplate field not found");
        templateField.SetValue(obj, promptTemplate);

        // Invoke FormatPrompt
        var method = typeof(WinMLRuntimeChatClient).GetMethod(
            "FormatPrompt",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "FormatPrompt method not found");

        return (string)method.Invoke(obj, [messages])!;
    }
}

#endif
