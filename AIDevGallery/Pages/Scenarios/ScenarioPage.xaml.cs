// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls;
using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.ProjectGenerator;
using AIDevGallery.Samples;
using AIDevGallery.Samples.SharedCode;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

#pragma warning disable SYSLIB1099 // Experimental WinML Runtime COM interop uses runtime-based marshalling

namespace AIDevGallery.Pages;

internal record WinMlEp(List<HardwareAccelerator> HardwareAccelerators, string Name, string ShortName, string DeviceType);

internal sealed partial class ScenarioPage : Page
{
    private readonly Dictionary<string, ExecutionProviderDevicePolicy> executionProviderDevicePolicies = new()
    {
        { "Default", ExecutionProviderDevicePolicy.DEFAULT },
        { "Max Efficiency", ExecutionProviderDevicePolicy.MAX_EFFICIENCY },
        { "Max Performance", ExecutionProviderDevicePolicy.MAX_PERFORMANCE },
        { "Minimize Overall Power", ExecutionProviderDevicePolicy.MIN_OVERALL_POWER },
        { "Prefer NPU", ExecutionProviderDevicePolicy.PREFER_NPU },
        { "Prefer GPU", ExecutionProviderDevicePolicy.PREFER_GPU },
        { "Prefer CPU", ExecutionProviderDevicePolicy.PREFER_CPU },
    };

#if WINML_RUNTIME_EXPERIMENTAL
    private static readonly string[] RuntimeDeviceTypeOrder = ["CPU", "GPU", "NPU"];
#endif

    private Scenario? scenario;
    private List<Sample>? samples;
    private Sample? sample;
    private ObservableCollection<ModelDetails?> modelDetails = new();
    private static List<WinMlEp>? supportedHardwareAccelerators;
#if WINML_RUNTIME_EXPERIMENTAL
    private static HashSet<string>? runtimeSupportedDeviceTypes;
#endif

    public ScenarioPage()
    {
        this.InitializeComponent();
        this.Loaded += (s, e) =>
        BackgroundShadow.Receivers.Add(ShadowCastGrid);
        App.MainWindow.ModelPicker.SelectedModelsChanged += ModelOrApiPicker_SelectedModelsChanged;
        this.Unloaded += (s, e) => App.MainWindow.ModelPicker.SelectedModelsChanged -= ModelOrApiPicker_SelectedModelsChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        VisualStateManager.GoToState(this, "PageLoading", true);
        base.OnNavigatedTo(e);
        _ = LoadPage(e.Parameter);
    }

    private async Task LoadPage(object parameter)
    {
        if (parameter is Scenario scenario)
        {
            this.scenario = scenario;
            await LoadPicker();
        }
        else if (parameter is SampleNavigationArgs sampleArgs)
        {
            this.scenario = ScenarioCategoryHelpers.AllScenarioCategories.SelectMany(sc => sc.Scenarios).FirstOrDefault(s => s.ScenarioType == sampleArgs.Sample.Scenario);
            await LoadPicker(sampleArgs.ModelDetails);

            if (sampleArgs.OpenCodeView.HasValue && sampleArgs.OpenCodeView.Value)
            {
                CodeToggle.IsChecked = true;
                HandleCodePane();
            }
        }

        samples = SampleDetails.Samples.Where(sample => sample.Scenario == this.scenario!.ScenarioType).ToList();
    }

    private async Task LoadPicker(ModelDetails? initialModelToLoad = null)
    {
        if (scenario == null)
        {
            return;
        }

        samples = [.. SampleDetails.Samples.Where(sample => sample.Scenario == scenario.ScenarioType)];

        if (samples.Count == 0)
        {
            return;
        }

        List<List<ModelType>> modelDetailsList = [samples.SelectMany(s => s.Model1Types).ToList()];

        // if any sample has a second model, collect all Model2Types from samples that define them
        if (samples[0].Model2Types != null)
        {
            modelDetailsList.Add(samples.Where(s => s.Model2Types != null).SelectMany(s => s.Model2Types!).ToList());
        }

        var preSelectedModels = await App.MainWindow.ModelPicker.Load(modelDetailsList, initialModelToLoad);
        HandleModelSelectionChanged(preSelectedModels);

        if (preSelectedModels.Contains(null) || preSelectedModels.Count == 0)
        {
            // user needs to select a model if one is not selected at first
            App.MainWindow.ModelPicker.Show(preSelectedModels);
            return;
        }
    }

    private static async Task<List<WinMlEp>> GetSupportedHardwareAccelerators()
    {
        if (supportedHardwareAccelerators != null)
        {
            return supportedHardwareAccelerators;
        }

        OrtEnv.Instance();
        var catalog = Microsoft.Windows.AI.MachineLearning.ExecutionProviderCatalog.GetDefault();

        try
        {
            var registeredProviders = await catalog.EnsureAndRegisterCertifiedAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register execution providers: {ex}");
        }

        supportedHardwareAccelerators = [new([HardwareAccelerator.CPU], "CPU", "CPU", "CPU")];

        foreach (var keyValuePair in WinMLHelpers.GetEpDeviceMap())
        {
            var epName = keyValuePair.Key;
            var epDevices = keyValuePair.Value;
            var epDeviceTypes = epDevices.Select(d => d.HardwareDevice.Type.ToString());

            switch (epName)
            {
                case ExecutionProviderNames.VitisAI:
                    supportedHardwareAccelerators.Add(new([HardwareAccelerator.VitisAI, HardwareAccelerator.NPU], ExecutionProviderNames.VitisAI, "VitisAI", "NPU"));
                    break;

                case ExecutionProviderNames.OpenVINO:
                    if (epDeviceTypes.Contains("CPU"))
                    {
                        supportedHardwareAccelerators.Add(new([HardwareAccelerator.OpenVINO, HardwareAccelerator.CPU], ExecutionProviderNames.OpenVINO, "OpenVINO", "CPU"));
                    }

                    if (epDeviceTypes.Contains("GPU"))
                    {
                        supportedHardwareAccelerators.Add(new([HardwareAccelerator.OpenVINO, HardwareAccelerator.GPU], ExecutionProviderNames.OpenVINO, "OpenVINO", "GPU"));
                    }

                    if (epDeviceTypes.Contains("NPU"))
                    {
                        supportedHardwareAccelerators.Add(new([HardwareAccelerator.OpenVINO, HardwareAccelerator.NPU], ExecutionProviderNames.OpenVINO, "OpenVINO", "NPU"));
                    }

                    break;

                case ExecutionProviderNames.QNN:
                    supportedHardwareAccelerators.Add(new([HardwareAccelerator.QNN, HardwareAccelerator.NPU], ExecutionProviderNames.QNN, "QNN", "NPU"));
                    break;

                case ExecutionProviderNames.DML:
                    supportedHardwareAccelerators.Add(new([HardwareAccelerator.DML, HardwareAccelerator.GPU], ExecutionProviderNames.DML, "DML", "GPU"));
                    break;

                case ExecutionProviderNames.NvTensorRTRTX:
                    supportedHardwareAccelerators.Add(new([HardwareAccelerator.NvTensorRT, HardwareAccelerator.GPU], ExecutionProviderNames.NvTensorRTRTX, "NvTensorRT", "GPU"));
                    break;
            }
        }

        return supportedHardwareAccelerators;
    }

    private async void HandleModelSelectionChanged(List<ModelDetails?> selectedModels)
    {
        if (selectedModels.Contains(null) || selectedModels.Count == 0)
        {
            // user needs to select a model
            VisualStateManager.GoToState(this, "NoModelSelected", true);
            return;
        }

        VisualStateManager.GoToState(this, "PageLoading", true);

        modelDetails.Clear();
        foreach (var model in selectedModels)
        {
            if (model != null)
            {
                modelDetails.Add(model!);
            }
        }

        // temporary fix EP dropdown list for useradded local languagemodel
        bool hasOnnxNonLanguageModel = selectedModels.Any(m => m != null && m.IsOnnxModel() && string.IsNullOrEmpty(m.ParameterSize) && m.Id.StartsWith("useradded-local-languagemodel", System.StringComparison.InvariantCultureIgnoreCase) == false);
#if WINML_RUNTIME_EXPERIMENTAL
        // In WinML Runtime experimental mode, also show EP options for language models
        bool hasOnnxLanguageModel = selectedModels.Any(m => m != null && m.IsOnnxModel() && !string.IsNullOrEmpty(m.ParameterSize));
        bool showWinMlOptions = hasOnnxNonLanguageModel || hasOnnxLanguageModel;
#else
        bool showWinMlOptions = hasOnnxNonLanguageModel;
#endif
        if (showWinMlOptions)
        {
            var delayTask = Task.Delay(1000);
            var supportedHardwareAcceleratorsTask = GetSupportedHardwareAccelerators();

            if (await Task.WhenAny(delayTask, supportedHardwareAcceleratorsTask) == delayTask)
            {
                VisualStateManager.GoToState(this, "PageLoadingWithMessage", true);
            }

            var supportedHardwareAccelerators = await supportedHardwareAcceleratorsTask;

            HashSet<WinMlEp> eps = [supportedHardwareAccelerators[0]];

            DeviceComboBox.Items.Clear();

            foreach (var hardwareAccelerator in selectedModels.SelectMany(m => m!.HardwareAccelerators).Distinct())
            {
                foreach (var ep in supportedHardwareAccelerators.Where(ep => ep.HardwareAccelerators.Contains(hardwareAccelerator)))
                {
                    eps.Add(ep);
                }
            }

            foreach (var ep in eps.OrderBy(ep => ep.Name))
            {
                DeviceComboBox.Items.Add(ep);
            }

            UpdateWinMLFlyout();

            WinMlModelOptionsButton.Visibility = Visibility.Visible;

#if WINML_RUNTIME_EXPERIMENTAL
            // Windows ML Runtime text generation is currently wired for ONNX language models.
            InferenceEngineComboBox.Visibility = hasOnnxLanguageModel ? Visibility.Visible : Visibility.Collapsed;
            InferenceEngineComboBox.SelectedIndex = hasOnnxLanguageModel && App.AppData.UseWinMLRuntime ? 1 : 0;
            WinMlModelOptionsButton.Visibility = hasOnnxLanguageModel && App.AppData.UseWinMLRuntime ? Visibility.Collapsed : Visibility.Visible;
            WinMLRuntimeOptionsButton.Visibility = hasOnnxLanguageModel && App.AppData.UseWinMLRuntime ? Visibility.Visible : Visibility.Collapsed;
            if (hasOnnxLanguageModel && App.AppData.UseWinMLRuntime)
            {
                await EnsureRuntimeDeviceOptionIsValidAsync(supportedHardwareAccelerators);
                UpdateRuntimeOptionsButtonText();
            }
#endif
        }
        else
        {
            WinMlModelOptionsButton.Visibility = Visibility.Collapsed;
#if WINML_RUNTIME_EXPERIMENTAL
            InferenceEngineComboBox.Visibility = Visibility.Collapsed;
            WinMLRuntimeOptionsButton.Visibility = Visibility.Collapsed;
#endif
        }

        if (selectedModels.Count == 1)
        {
            // add the second model with null
            selectedModels = [selectedModels[0], null];
        }

        List<Sample> viableSamples = samples!.Where(s =>
                IsModelFromTypes(s.Model1Types, selectedModels[0]) &&
                IsModelFromTypes(s.Model2Types, selectedModels[1])).ToList();

        if (viableSamples.Count == 0)
        {
            // this should never happen
            App.MainWindow.ModelPicker.Show(selectedModels);
            return;
        }

        if (viableSamples.Count > 1)
        {
            SampleSelection.Items.Clear();
            foreach (var sample in viableSamples)
            {
                SampleSelection.Items.Add(sample);
            }

            SampleSelection.SelectedItem = viableSamples[0];
            SampleContainer.ShowFooter = true;
        }
        else
        {
            SampleContainer.ShowFooter = false;
            LoadSample(viableSamples[0]);
        }
    }

    private void UpdateWinMLFlyout()
    {
        var options = App.AppData.WinMLSampleOptions;
        if (options.Policy != null)
        {
            var key = executionProviderDevicePolicies.FirstOrDefault(kvp => kvp.Value == options.Policy).Key;
            ExecutionPolicyComboBox.SelectedItem = key;
            WinMlModelOptionsButtonText.Text = key;
            DeviceComboBox.SelectedIndex = 0;
            segmentedControl.SelectedIndex = 0;
        }
        else if (options.EpName != null)
        {
            var selectedDevice = DeviceComboBox.Items.Where(i => (i as WinMlEp)?.Name == options.EpName && (i as WinMlEp)?.DeviceType == options.DeviceType).FirstOrDefault();
            if (selectedDevice != null)
            {
                DeviceComboBox.SelectedItem = selectedDevice;
            }
            else
            {
                DeviceComboBox.SelectedIndex = 0;
            }

            ExecutionPolicyComboBox.SelectedIndex = 0;
            CompileModelCheckBox.IsChecked = options.CompileModel;
            WinMlModelOptionsButtonText.Text = (DeviceComboBox.SelectedItem as WinMlEp)?.ShortName;
            segmentedControl.SelectedIndex = 1;
            UpdateCompileModelVisibility();
        }

        // in case already saved options do not apply to this sample
        _ = UpdateSampleOptions();
    }

    private void LoadSample(Sample? sampleToLoad)
    {
        sample = sampleToLoad;

        if (sample == null)
        {
            return;
        }

        VisualStateManager.GoToState(this, "ModelSelected", true);

        // TODO: don't load sample if model is not cached, but still let code to be seen
        //       this would probably be handled in the SampleContainer
        _ = SampleContainer.LoadSampleAsync(sample, modelDetails.Where(m => m != null).Select(m => m!).ToList(), App.AppData.WinMLSampleOptions);
        _ = App.AppData.AddMru(
            new MostRecentlyUsedItem()
            {
                Type = MostRecentlyUsedItemType.Scenario,
                ItemId = scenario!.Id,
                Icon = scenario.Icon,
                Description = scenario.Description,
                SubItemId = modelDetails[0]!.Id,
                DisplayName = scenario.Name
            },
            modelDetails.Select(m => (m!.Id, m.HardwareAccelerators.First())).ToList());
    }

    private bool IsModelFromTypes(List<ModelType>? types, ModelDetails? model)
    {
        if (types == null && model == null)
        {
            return true;
        }

        if (types == null || model == null)
        {
            return false;
        }

        if (types.Contains(ModelType.LanguageModels) && model.IsLanguageModel())
        {
            return true;
        }

        List<string> modelIds = [];

        foreach (var type in types)
        {
            modelIds.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(type).Select(m => m.Id));
            if (App.AppData.TryGetUserAddedModelIds(type, out var ids))
            {
                modelIds.AddRange(ids!);
            }
        }

        return modelIds.Any(id => id == model.Id);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText("aidevgallery://scenarios/" + scenario!.Id);
        Clipboard.SetContentWithOptions(dataPackage, null);
    }

    private void CodeToggle_Click(object sender, RoutedEventArgs args)
    {
        HandleCodePane();
    }

    private void HandleCodePane()
    {
        if (sample != null)
        {
            ToggleCodeButtonEvent.Log(sample.Name ?? string.Empty, CodeToggle.IsChecked == true);
        }

        if (CodeToggle.IsChecked == true)
        {
            SampleContainer.ShowCode();
        }
        else
        {
            SampleContainer.HideCode();
        }
    }

    private void ExportSampleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || sample == null)
        {
            return;
        }

        _ = Generator.AskGenerateAndOpenAsync(sample, modelDetails.Where(m => m != null).Select(m => m!), App.AppData.WinMLSampleOptions, XamlRoot);
    }

    private void ActionButtonsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Calculate if the modelselectors collide with the export/code buttons
        if ((ActionsButtonHolderPanel.ActualWidth + ButtonsPanel.ActualWidth) >= e.NewSize.Width)
        {
            VisualStateManager.GoToState(this, "NarrowLayout", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "WideLayout", true);
        }
    }

    private void ModelBtn_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;

        var picker = App.MainWindow.ModelPicker;
        picker.Closed += (s, args) =>
        {
            button?.Focus(FocusState.Programmatic);
        };

        App.MainWindow.ModelPicker.Show(modelDetails.ToList());
    }

    private void ModelOrApiPicker_SelectedModelsChanged(object sender, List<ModelDetails?> modelDetails)
    {
        HandleModelSelectionChanged(modelDetails);
        contentHost.Focus(FocusState.Programmatic);
    }

    private void SampleSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedSample = e.AddedItems
            .OfType<Sample>()
            .ToList().FirstOrDefault();

        LoadSample(selectedSample);
    }

    private async void ApplySampleOptions(object sender, RoutedEventArgs e)
    {
        WinMLOptionsFlyout.Hide();
        await UpdateSampleOptions();
        LoadSample(sample);
    }

    private async Task UpdateSampleOptions()
    {
        var oldOptions = App.AppData.WinMLSampleOptions;

        if (segmentedControl.SelectedIndex == 0)
        {
            var key = (ExecutionPolicyComboBox.SelectedItem as string) ?? executionProviderDevicePolicies.Keys.First();
            WinMlModelOptionsButtonText.Text = key;
            App.AppData.WinMLSampleOptions = new WinMlSampleOptions(executionProviderDevicePolicies[key], null, false, null);
        }
        else
        {
            var device = (DeviceComboBox.SelectedItem as WinMlEp) ?? (DeviceComboBox.Items.First() as WinMlEp);
            WinMlModelOptionsButtonText.Text = device!.ShortName;
            App.AppData.WinMLSampleOptions = new WinMlSampleOptions(null, device.Name, CompileModelCheckBox.IsChecked!.Value, device.DeviceType);
        }

        if (oldOptions == App.AppData.WinMLSampleOptions)
        {
            return;
        }

        await App.AppData.SaveAsync();
    }

    private void WinMLOptionsFlyout_Opening(object sender, object e)
    {
        UpdateWinMLFlyout();
        UpdateCompileModelVisibility();
    }

    private async void InferenceEngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
#if WINML_RUNTIME_EXPERIMENTAL
        if (InferenceEngineComboBox.SelectedItem is ComboBoxItem selected)
        {
            var useRuntime = selected.Tag?.ToString() == "WinMLRuntime";
            App.AppData.UseWinMLRuntime = useRuntime;
            _ = App.AppData.SaveAsync();

            // Toggle EP options vs Runtime options visibility
            WinMlModelOptionsButton.Visibility = useRuntime ? Visibility.Collapsed : Visibility.Visible;
            WinMLRuntimeOptionsButton.Visibility = useRuntime ? Visibility.Visible : Visibility.Collapsed;

            // Update Runtime options button text
            if (useRuntime)
            {
                var supportedAccelerators = await GetSupportedHardwareAccelerators();
                await EnsureRuntimeDeviceOptionIsValidAsync(supportedAccelerators);
                UpdateRuntimeOptionsButtonText();
            }

            // Reload the sample with the new engine
            LoadSample(sample);
        }
#endif
    }

#if WINML_RUNTIME_EXPERIMENTAL
    private async void WinMLRuntimeOptionsFlyout_Opening(object sender, object e)
    {
        var supportedAccelerators = await GetSupportedHardwareAccelerators();
        await EnsureRuntimeDeviceOptionIsValidAsync(supportedAccelerators);
        UpdateRuntimeDeviceTypeComboBox(supportedAccelerators);

        var options = App.AppData.WinMLRuntimeOptions;

        // Sync Device Type combo
        foreach (ComboBoxItem item in RuntimeDeviceTypeComboBox.Items)
        {
            if (string.Equals(item.Tag?.ToString(), options.DeviceType, System.StringComparison.OrdinalIgnoreCase))
            {
                RuntimeDeviceTypeComboBox.SelectedItem = item;
                break;
            }
        }

        if (RuntimeDeviceTypeComboBox.SelectedIndex < 0)
        {
            RuntimeDeviceTypeComboBox.SelectedIndex = 0;
        }

        // Sync Execution Policy combo
        foreach (ComboBoxItem item in RuntimeExecutionPolicyComboBox.Items)
        {
            if (string.Equals(item.Tag?.ToString(), options.ExecutionPolicy, System.StringComparison.OrdinalIgnoreCase))
            {
                RuntimeExecutionPolicyComboBox.SelectedItem = item;
                break;
            }
        }

        if (RuntimeExecutionPolicyComboBox.SelectedIndex < 0)
        {
            RuntimeExecutionPolicyComboBox.SelectedIndex = 0;
        }
    }

    private async void ApplyRuntimeOptions(object sender, RoutedEventArgs e)
    {
        WinMLRuntimeOptionsFlyout.Hide();

        var deviceType = (RuntimeDeviceTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Default";
        if (string.IsNullOrWhiteSpace(deviceType))
        {
            return;
        }

        var executionPolicy = (RuntimeExecutionPolicyComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Default";

        var oldOptions = App.AppData.WinMLRuntimeOptions;
        App.AppData.WinMLRuntimeOptions = new WinMLRuntimeOptions(deviceType, executionPolicy);

        if (oldOptions == App.AppData.WinMLRuntimeOptions)
        {
            return;
        }

        UpdateRuntimeOptionsButtonText();
        await App.AppData.SaveAsync();
        LoadSample(sample);
    }

    private void UpdateRuntimeOptionsButtonText()
    {
        var options = App.AppData.WinMLRuntimeOptions;
        var deviceLabel = options.DeviceType == "Default" ? "Auto" : options.DeviceType;
        var policyLabel = options.ExecutionPolicy == "Default" ? string.Empty : $" · {options.ExecutionPolicy}";
        RuntimeOptionsButtonText.Text = $"{deviceLabel}{policyLabel}";
    }

    private async Task EnsureRuntimeDeviceOptionIsValidAsync(IReadOnlyList<WinMlEp> supportedAccelerators)
    {
        var eligibleDeviceTypes = GetEligibleRuntimeDeviceTypes(supportedAccelerators);
        UpdateRuntimeDeviceTypeComboBox(supportedAccelerators);

        if (eligibleDeviceTypes.Count == 0)
        {
            return;
        }

        if (!eligibleDeviceTypes.Contains(App.AppData.WinMLRuntimeOptions.DeviceType))
        {
            App.AppData.WinMLRuntimeOptions = App.AppData.WinMLRuntimeOptions with
            {
                DeviceType = eligibleDeviceTypes[0]
            };
            UpdateRuntimeOptionsButtonText();
            await App.AppData.SaveAsync();
        }
    }

    private void UpdateRuntimeDeviceTypeComboBox(IReadOnlyList<WinMlEp> supportedAccelerators)
    {
        var eligibleDeviceTypes = GetEligibleRuntimeDeviceTypes(supportedAccelerators);
        RuntimeDeviceTypeComboBox.Items.Clear();

        foreach (var deviceType in eligibleDeviceTypes)
        {
            RuntimeDeviceTypeComboBox.Items.Add(new ComboBoxItem
            {
                Content = deviceType,
                Tag = deviceType
            });
        }

        if (eligibleDeviceTypes.Count == 0)
        {
            RuntimeDeviceTypeComboBox.Items.Add(new ComboBoxItem
            {
                Content = "No supported device",
                Tag = string.Empty,
                IsEnabled = false
            });
            RuntimeDeviceTypeComboBox.SelectedIndex = 0;
            RuntimeOptionsApplyButton.IsEnabled = false;
            return;
        }

        RuntimeOptionsApplyButton.IsEnabled = true;
    }

    private List<string> GetEligibleRuntimeDeviceTypes(IReadOnlyList<WinMlEp> supportedAccelerators)
    {
        var modelDeviceTypes = GetSelectedModelRuntimeDeviceTypes(supportedAccelerators);
        var machineDeviceTypes = supportedAccelerators
            .Select(ep => ep.DeviceType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        machineDeviceTypes.IntersectWith(GetRuntimeSupportedDeviceTypes());

        return RuntimeDeviceTypeOrder
            .Where(deviceType => modelDeviceTypes.Contains(deviceType) && machineDeviceTypes.Contains(deviceType))
            .ToList();
    }

    private HashSet<string> GetSelectedModelRuntimeDeviceTypes(IReadOnlyList<WinMlEp> supportedAccelerators)
    {
        HashSet<string>? selectedDeviceTypes = null;
        foreach (var model in modelDetails.Where(m => m != null))
        {
            var modelDeviceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var hardwareAccelerator in model!.HardwareAccelerators)
            {
                foreach (var ep in supportedAccelerators.Where(ep => ep.HardwareAccelerators.Contains(hardwareAccelerator)))
                {
                    modelDeviceTypes.Add(ep.DeviceType);
                }

                AddRuntimeDeviceTypeForAccelerator(modelDeviceTypes, hardwareAccelerator);
            }

            selectedDeviceTypes = selectedDeviceTypes is null
                ? modelDeviceTypes
                : new HashSet<string>(selectedDeviceTypes.Intersect(modelDeviceTypes), StringComparer.OrdinalIgnoreCase);
        }

        return selectedDeviceTypes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddRuntimeDeviceTypeForAccelerator(HashSet<string> deviceTypes, HardwareAccelerator hardwareAccelerator)
    {
        switch (hardwareAccelerator)
        {
            case HardwareAccelerator.CPU:
                deviceTypes.Add("CPU");
                break;
            case HardwareAccelerator.DML:
            case HardwareAccelerator.GPU:
            case HardwareAccelerator.NvTensorRT:
                deviceTypes.Add("GPU");
                break;
            case HardwareAccelerator.QNN:
            case HardwareAccelerator.NPU:
            case HardwareAccelerator.VitisAI:
                deviceTypes.Add("NPU");
                break;
        }
    }

    private static HashSet<string> GetRuntimeSupportedDeviceTypes()
    {
        if (runtimeSupportedDeviceTypes != null)
        {
            return runtimeSupportedDeviceTypes;
        }

        runtimeSupportedDeviceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var runtime = AIDevGallery.Interop.WinMLRuntime.WinMLRuntimeWrapper.Create();
            runtime.GetRawRuntime().GetCapabilities(out var capabilities);
            try
            {
                AddIfRuntimeSupports(capabilities, AIDevGallery.Interop.WinMLRuntime.WinMLDeviceType.CPU, "CPU");
                AddIfRuntimeSupports(capabilities, AIDevGallery.Interop.WinMLRuntime.WinMLDeviceType.GPU, "GPU");
                AddIfRuntimeSupports(capabilities, AIDevGallery.Interop.WinMLRuntime.WinMLDeviceType.NPU, "NPU");
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(capabilities);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to query WinML Runtime capabilities: {ex}");
            runtimeSupportedDeviceTypes.Add("CPU");
        }

        return runtimeSupportedDeviceTypes;
    }

    private static void AddIfRuntimeSupports(
        AIDevGallery.Interop.WinMLRuntime.IWinMLCapabilities capabilities,
        AIDevGallery.Interop.WinMLRuntime.WinMLDeviceType deviceType,
        string deviceTypeName)
    {
        capabilities.IsDeviceTypeSupported(deviceType, out var isSupported);
        if (isSupported != 0)
        {
            runtimeSupportedDeviceTypes!.Add(deviceTypeName);
        }
    }
#endif

    private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCompileModelVisibility();
    }

    private void UpdateCompileModelVisibility()
    {
        var device = DeviceComboBox.SelectedItem as WinMlEp;
        bool supported = device != null && WinMLHelpers.IsCompileModelSupported(device.DeviceType);
        CompileModelCheckBox.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
        if (!supported)
        {
            CompileModelCheckBox.IsChecked = false;
        }
    }
}