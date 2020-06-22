﻿using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public sealed class SliceVisualizerContext : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<TypedSliceWatchView> WatchSelected;
        public event EventHandler<bool> HeatMapStateChanged;

        public Options.ProjectOptions Options { get; }

        private string _selectedWatch;
        public string SelectedWatch { get => _selectedWatch; set => SetField(ref _selectedWatch, value); }

        private VariableType _selectedType;
        public VariableType SelectedType { get => _selectedType; set => SetField(ref _selectedType, value); }

        public List<string> Watches { get; } = new List<string>();

        private readonly VisualizerContext _visualizerContext;
        private readonly EnvDTE80.WindowVisibilityEvents _windowVisibilityEvents;

        private bool _windowVisible;

        public SliceVisualizerContext(Options.ProjectOptions options, VisualizerContext visualizerContext, EnvDTE80.WindowVisibilityEvents visibilityEvents)
        {
            Options = options;
            Options.SliceVisualizerOptions.PropertyChanged += SliceOptionChanged;
            _visualizerContext = visualizerContext;
            _visualizerContext.GroupFetching += SetupDataFetch;
            _visualizerContext.GroupFetched += DisplayFetchedData;
            _windowVisibilityEvents = visibilityEvents;
            _windowVisibilityEvents.WindowShowing += OnToolWindowVisibilityChanged;
            _windowVisibilityEvents.WindowHiding += OnToolWindowVisibilityChanged;
        }

        public void Dispose()
        {
            _windowVisibilityEvents.WindowShowing -= OnToolWindowVisibilityChanged;
            _windowVisibilityEvents.WindowHiding -= OnToolWindowVisibilityChanged;
        }

        private void SliceOptionChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.SliceVisualizerOptions.GroupsInRow):
                case nameof(Options.SliceVisualizerOptions.SubgroupSize):
                case nameof(Options.SliceVisualizerOptions.VisibleColumns):
                    WatchSelectionChanged();
                    break;
                case nameof(Options.SliceVisualizerOptions.UseHeatMap):
                    HeatMapStateChanged(this, Options.SliceVisualizerOptions.UseHeatMap);
                    break;
            }
        }

        private void SetupDataFetch(object sender, GroupFetchingEventArgs e)
        {
            e.FetchWholeFile |= _windowVisible;
        }

        private void DisplayFetchedData(object sender, GroupFetchedEventArgs e)
        {
            if (!Watches.SequenceEqual(_visualizerContext.BreakData.Watches))
            {
                Watches.Clear();
                Watches.Add("System");
                Watches.AddRange(_visualizerContext.BreakData.Watches);
                RaisePropertyChanged(nameof(Watches));
            }
            if (_windowVisible && !string.IsNullOrEmpty(SelectedWatch))
            {
                var watchView = _visualizerContext.BreakData.GetSliceWatch(SelectedWatch, Options.SliceVisualizerOptions.GroupsInRow,
                    (int)_visualizerContext.Options.DebuggerOptions.NGroups);
                var typedView = new TypedSliceWatchView(watchView, SelectedType);
                WatchSelected(this, typedView);
            }
        }

        private void WatchSelectionChanged()
        {
            if (!_windowVisible || string.IsNullOrEmpty(SelectedWatch) || _visualizerContext.BreakData == null)
                return;
            _visualizerContext.GroupIndex.Update();
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            switch (propertyName)
            {
                case nameof(SelectedWatch):
                case nameof(SelectedType):
                    WatchSelectionChanged();
                    break;
            }
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            RaisePropertyChanged(propertyName);

            return true;
        }

        private void OnToolWindowVisibilityChanged(EnvDTE.Window window)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (window.Caption != VSPackage.SliceVisualizerToolWindow.Caption)
                return;
            if (_windowVisible == window.Visible)
                return;
            if (window.Visible)
            {
                _windowVisible = true;
                WatchSelectionChanged();
            }
            else
            {
                _windowVisible = false;
            }
        }
    }
}
