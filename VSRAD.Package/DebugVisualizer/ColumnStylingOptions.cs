﻿using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnStylingOptions : DefaultNotifyPropertyChanged
    {
        private string _visibleColumns = "0:1-8191";
        public string VisibleColumns { get => _visibleColumns; set => SetField(ref _visibleColumns, value); }

        private VisibleColumnsRange _range = new VisibleColumnsRange("0:1-8191");
        public VisibleColumnsRange Range { get => _range; set => SetField(ref _range, value); }

        private string _backgroundColors;
        public string BackgroundColors { get => _backgroundColors; set => SetField(ref _backgroundColors, value); }

        private string _foregroundColors;
        public string ForegroundColors { get => _foregroundColors; set => SetField(ref _foregroundColors, value); }
    }
}
