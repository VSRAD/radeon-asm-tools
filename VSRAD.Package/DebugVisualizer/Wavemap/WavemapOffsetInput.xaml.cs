﻿using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    public sealed partial class WavemapOffsetInput : UserControl, INotifyPropertyChanged
    {
        private VisualizerContext _context;
        private WavemapImage _image;

        public event PropertyChangedEventHandler PropertyChanged;

        private string _offsetLabel = "No data";
        public string OffsetLabel
        {
            get => _offsetLabel;
            set { _offsetLabel = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OffsetLabel))); }
        }

        private bool _showOffsetSelector = true;
        public bool ShowOffsetSelector
        {
            get => _showOffsetSelector;
            set { _showOffsetSelector = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowOffsetSelector))); }
        }

        public WavemapOffsetInput()
        {
            InitializeComponent();
            // Do not change this.DataContext, it inherits from the control that uses NumberInput
            Root.DataContext = this;
        }

        private void Increment(object sender, RoutedEventArgs e)
        {
            _image.FirstGroup += _image.GridSizeX - _image.FirstGroup % _image.GridSizeX;
        }

        private void Decrement(object sender, RoutedEventArgs e)
        {
            var stepRem = _image.FirstGroup % _image.GridSizeX;
            var dec = stepRem == 0 ? _image.GridSizeX : stepRem;
            _image.FirstGroup = (_image.FirstGroup > dec) ? _image.FirstGroup - dec : 0;
        }

        public void Setup(VisualizerContext context, WavemapImage image)
        {
            _context = context;
            _context.PropertyChanged += UpdateTooltipAndWaveInfo;
            _image = image;
            _image.Updated += UpdateControls;
        }

        private void UpdateTooltipAndWaveInfo(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VisualizerContext.WavemapSelection))
            {
                if (_context.WavemapSelection is WaveInfo waveInfo)
                {
                    {
                        var info = $"G: {waveInfo.GroupIndex}\nW: {waveInfo.WaveIndex}";
                        if (waveInfo.PartialExecMask && waveInfo.BreakLine != null)
                            info += " (E)";
                        info += "\n";
                        info += waveInfo.BreakLine is uint breakLine ? $"L: {breakLine}" : "No break";
                        WaveInfoTextBlock.Text = info;
                    }
                    {
                        var tooltip = $"Group: {waveInfo.GroupIndex}\nWave: {waveInfo.WaveIndex}";
                        if (waveInfo.PartialExecMask && waveInfo.BreakLine != null)
                            tooltip += " (partial EXEC mask)";
                        tooltip += "\n";
                        tooltip += waveInfo.BreakLine is uint breakLine ? $"Line: {breakLine}" : "No breakpoint reached";
                        WaveInfoTextBlock.ToolTip = tooltip;
                    }
                }
                else
                {
                    WaveInfoTextBlock.Text = "";
                    WaveInfoTextBlock.ToolTip = "";
                }
            }
        }

        private void UpdateControls(object sender, EventArgs e)
        {
            var groupCount = (_context.BreakData?.NumThreadsInProgram ?? 0) / MathUtils.RoundUpToMultiple(_context.Options.DebuggerOptions.GroupSize, _context.Options.DebuggerOptions.WaveSize);
            if (groupCount > 0 && _image.GridSizeX > 0)
            {
                DecButton.IsEnabled = _image.FirstGroup != 0;
                IncButton.IsEnabled = _image.FirstGroup + _image.GridSizeX < groupCount;

                ShowOffsetSelector = _image.FirstGroup != 0 || IncButton.IsEnabled;

                if (_image.FirstGroup < groupCount)
                {
                    var lastDisplayedGroup = Math.Min(groupCount, _image.FirstGroup + _image.GridSizeX) - 1;
                    OffsetLabel = $"{_image.FirstGroup} - {lastDisplayedGroup}";
                }
                else
                {
                    OffsetLabel = $"{_image.FirstGroup} - {_image.FirstGroup}";
                }
            }
            else
            {
                DecButton.IsEnabled = false;
                IncButton.IsEnabled = false;
                OffsetLabel = "No data";
            }
        }
    }
}
