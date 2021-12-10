﻿using System.ComponentModel;
using System.Windows.Controls;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ToolWindows;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public sealed partial class SliceVisualizerControl : UserControl, IDisposableToolWindow
    {
        private readonly SliceVisualizerTable _table;
        private readonly SliceVisualizerContext _context;

        public SliceVisualizerControl(IToolWindowIntegration integration)
        {
            _context = integration.GetSliceVisualizerContext();
            _context.WatchSelected += WatchSelected;
            _context.HeatMapStateChanged += HeatMapStateChanged;

            DataContext = _context;
            PropertyChangedEventManager.AddHandler(_context.Options.SliceVisualizerOptions, SliceVisualizerOptionChanged, "");
            InitializeComponent();

            var tableFontAndColor = new FontAndColorProvider();
            _table = new SliceVisualizerTable(tableFontAndColor);
            TableHost.Setup(_table);
        }

        void IDisposableToolWindow.DisposeToolWindow()
        {
            ((DockPanel)Content).Children.Clear();
            TableHost.Dispose();
        }

        private void SliceVisualizerOptionChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.SliceVisualizerOptions.VisibleColumns):
                    break;
                default:
                    break;
            }
        }

        private void WatchSelected(object sender, TypedSliceWatchView watch) =>
            _table.DisplayWatch(watch);

        private void HeatMapStateChanged(object sender, bool state) =>
            _table.SetHeatMapMode(state);
    }
}
