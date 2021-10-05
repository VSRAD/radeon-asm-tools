﻿using System.Windows.Controls;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public partial class SliceVisualizerControl : UserControl
    {
        private readonly SliceVisualizerTable _table;
        private readonly SliceVisualizerContext _context;

        public SliceVisualizerControl(IToolWindowIntegration integration)
        {
            _context = integration.GetSliceVisualizerContext();
            _context.WatchSelected += WatchSelected;
            _context.HeatMapStateChanged += HeatMapStateChanged;
            _context.Options.SliceVisualizerOptions.PropertyChanged += SliceVisualizerOptionChanged;
            _context.DivierWidthChanged += () => _table.ColumnStyling.Recompute(_context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns, _context.Options.VisualizerAppearance);
            _context.ColorRangeChanged += () => _table.Invalidate();
            DataContext = _context;
            InitializeComponent();

            var tableFontAndColor = new FontAndColorProvider();
            _table = new SliceVisualizerTable(tableFontAndColor, _context.Options.VisualizerAppearance, _context.Options.VisualizerColumnStyling);
            _table.ColumnStyling.Recompute(_context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns, _context.Options.VisualizerAppearance);
            TableHost.Setup(_table);
        }

        private void SliceVisualizerOptionChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.SliceVisualizerOptions.VisibleColumns):
                case nameof(Options.SliceVisualizerOptions.SubgroupSize):
                    _table.ColumnStyling.Recompute(
                        _context.Options.SliceVisualizerOptions.SubgroupSize,
                        _context.Options.SliceVisualizerOptions.VisibleColumns
                    );
                    break;
                default:
                    break;
            }
        }

        private void WatchSelected(object sender, TypedSliceWatchView watch) =>
            _table.DisplayWatch(watch, _context.Options.SliceVisualizerOptions.SubgroupSize, _context.Options.SliceVisualizerOptions.VisibleColumns);

        private void HeatMapStateChanged(object sender, bool state) =>
            _table.SetHeatMapMode(state);
    }
}
