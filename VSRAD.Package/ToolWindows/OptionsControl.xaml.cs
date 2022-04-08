﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ToolWindows
{
    public sealed partial class OptionsControl : UserControl
    {
        public sealed class Context : DefaultNotifyPropertyChanged
        {
            public ProjectOptions Options { get; }
            public IReadOnlyList<string> ProfileNames => Options.Profiles.Keys.ToList();

            public string ConnectionInfo =>
                Options.Profile?.General?.RunActionsLocally == true ? "Local" : _channel.ConnectionOptions.ToString();

            public string ServerInfo =>
                _channel.ServerCapabilities?.ToString() ?? "";

            public Visibility DisconnectButtonVisible =>
                Options.Profile?.General?.RunActionsLocally == true ? Visibility.Collapsed : Visibility.Visible;

            public string DisconnectLabel
            {
                get => _channel.ConnectionState == ClientState.Connected ? "Disconnect"
                     : _channel.ConnectionState == ClientState.Connecting ? "Connecting..." : "Disconnected";
            }

            private string _actionStatusLabel;
            public string ActionStatusLabel { get => _actionStatusLabel; private set => SetField(ref _actionStatusLabel, value); }

            private Visibility _cancelActionButtonVisible = Visibility.Collapsed;
            public Visibility CancelActionButtonVisible { get => _cancelActionButtonVisible; private set => SetField(ref _cancelActionButtonVisible, value); }

            public ICommand DisconnectCommand { get; }
            public ICommand CancelActionCommand { get; }

            private readonly CommunicationChannel _channel;

            public Context(ProjectOptions options, ICommunicationChannel channel, IActionLauncher actionLauncher)
            {
                Options = options;
                _channel = (CommunicationChannel)channel;

                PropertyChangedEventManager.AddHandler(options, ProfileChanged, nameof(Options.Profiles));
                WeakEventManager<CommunicationChannel, EventArgs>.AddHandler(
                    _channel, nameof(CommunicationChannel.ConnectionStateChanged), ConnectionStateChanged);
                WeakEventManager<IActionLauncher, ActionExecutionStateChangedEventArgs>.AddHandler(
                    actionLauncher, nameof(ActionLauncher.ActionExecutionStateChanged), ActionExecutionStateChanged);

                DisconnectCommand = new WpfDelegateCommand((_) => _channel.ForceDisconnect(), isEnabled: _channel.ConnectionState == ClientState.Connected);
                CancelActionCommand = new WpfDelegateCommand((_) => actionLauncher.CancelRunningAction());
            }

            private void ProfileChanged(object sender, PropertyChangedEventArgs e)
            {
                RaisePropertyChanged(nameof(ProfileNames));
                ConnectionStateChanged(null, null);
            }

            private void ConnectionStateChanged(object sender, EventArgs e)
            {
                RaisePropertyChanged(nameof(ConnectionInfo));
                RaisePropertyChanged(nameof(ServerInfo));
                RaisePropertyChanged(nameof(DisconnectLabel));
                RaisePropertyChanged(nameof(DisconnectButtonVisible));
                ((WpfDelegateCommand)DisconnectCommand).IsEnabled = _channel.ConnectionState == ClientState.Connected;
            }

            private void ActionExecutionStateChanged(object sender, ActionExecutionStateChangedEventArgs e)
            {
                switch (e.State)
                {
                    case ActionExecutionState.Started:
                        ActionStatusLabel = $"Action {e.ActionName} is running";
                        break;
                    case ActionExecutionState.Cancelling:
                        ActionStatusLabel = $"Cancelling {e.ActionName} action...";
                        break;
                    case ActionExecutionState.Finished:
                    case ActionExecutionState.Idle:
                        ActionStatusLabel = "";
                        break;
                }
                CancelActionButtonVisible = e.State == ActionExecutionState.Started ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private readonly IToolWindowIntegration _integration;
        private readonly ProjectOptions _projectOptions;

        public OptionsControl(IToolWindowIntegration integration)
        {
            _integration = integration;
            _projectOptions = integration.ProjectOptions;
            DataContext = new Context(integration.ProjectOptions, integration.CommunicationChannel, integration.ActionLauncher);
            InitializeComponent();
        }

        private void EditProfiles(object sender, RoutedEventArgs e) =>
            new ProjectSystem.Profiles.ProfileOptionsWindow(_integration) { ShowInTaskbar = false }.ShowModal();

        private void AlignmentButtonClick(object sender, RoutedEventArgs e)
        {
            var button = ((Button)sender);
            DebugVisualizer.ContentAlignment alignment;
            switch (button.Content)
            {
                case "C":
                    alignment = DebugVisualizer.ContentAlignment.Center;
                    break;
                case "R":
                    alignment = DebugVisualizer.ContentAlignment.Right;
                    break;
                default:
                    alignment = DebugVisualizer.ContentAlignment.Left;
                    break;
            }
            switch (button.Tag)
            {
                case "data":
                    _projectOptions.VisualizerAppearance.DataColumnAlignment = alignment;
                    break;
                case "headers":
                    _projectOptions.VisualizerAppearance.HeadersAlignment = alignment;
                    break;
                default:
                    _projectOptions.VisualizerAppearance.NameColumnAlignment = alignment;
                    break;
            }
        }
    }

    public sealed class ScalingModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch (value)
            {
                case DebugVisualizer.ScalingMode.ResizeColumnAllowWide:
                    return "Resize Column, allow wide 1st column";
                case DebugVisualizer.ScalingMode.ResizeColumn:
                    return "Resize Column";
                case DebugVisualizer.ScalingMode.ResizeTable:
                    return "Resize Table";
                case DebugVisualizer.ScalingMode.ResizeQuad:
                    return "Resize on side quads, pan on middle";
                case DebugVisualizer.ScalingMode.ResizeHalf:
                    return "Resize on header, pan on data cells";
                default:
                    return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value;
    }
}
