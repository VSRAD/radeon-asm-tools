﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.ContextMenus
{
    public sealed class SubgroupContextMenu : IContextMenu
    {
        private readonly VisualizerTable _table;
        private readonly TableState _state;
        private readonly ColumnStylingOptions _stylingOptions;
        private readonly VisualizerTable.GetGroupSize _getGroupSize;
        private readonly ContextMenu _menu;

        private int _clickedColumnIndex;

        public SubgroupContextMenu(VisualizerTable table, TableState state, ColumnStylingOptions stylingOptions, VisualizerTable.GetGroupSize getGroupSize)
        {
            _table = table;
            _state = state;
            _stylingOptions = stylingOptions;
            _getGroupSize = getGroupSize;
            _menu = PrepareContextMenu();
        }

        public bool Show(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (hit.RowIndex != -1 || hit.ColumnIndex < 0)
                return false;
            _clickedColumnIndex = hit.ColumnIndex;
            _menu.MenuItems.Cast<MenuItem>().First(m => m.Text == "Hide This").Enabled = hit.ColumnIndex >= VisualizerTable.DataColumnOffset;
            _menu.Show(_table, new Point(e.X, e.Y));
            return true;
        }

        private ContextMenu PrepareContextMenu()
        {
            var keepFirst = CreatePartialSubgroupMenu(minSubgroupSize: 1, maxSubgroupSize: 512, displayLast: false);
            var keepLast = CreatePartialSubgroupMenu(minSubgroupSize: 1, maxSubgroupSize: 512, displayLast: true);

            var showAll = new MenuItem("Show All Columns", (s, e) => SetColumnSelector($"0-{_getGroupSize() - 1}"));

            var fgColor = new MenuItem("Font Color", new[]
            {
                new MenuItem("Green", (s, e) => SetForegroundColor(DataHighlightColor.Green)),
                new MenuItem("Red", (s, e) => SetForegroundColor(DataHighlightColor.Red)),
                new MenuItem("Blue", (s, e) => SetForegroundColor(DataHighlightColor.Blue)),
                new MenuItem("None", (s, e) => SetForegroundColor(DataHighlightColor.None))
            });
            var bgColor = new MenuItem("Background Color", new[]
            {
                new MenuItem("Green", (s, e) => SetBackgroundColor(DataHighlightColor.Green)),
                new MenuItem("Red", (s, e) => SetBackgroundColor(DataHighlightColor.Red)),
                new MenuItem("Blue", (s, e) => SetBackgroundColor(DataHighlightColor.Blue)),
                new MenuItem("None", (s, e) => SetBackgroundColor(DataHighlightColor.None))
            });

            var fitWidth = new MenuItem("Autofit Width", (s, e) =>
                _state.FitWidth(_clickedColumnIndex));

            var hideThis = new MenuItem("Hide This", HideColumns);

            var menuItems = new[] { new MenuItem("Keep First") { Enabled = false } }
                .Concat(keepFirst)
                .Append(new MenuItem("-"))
                .Append(new MenuItem("Keep Last", keepLast))
                .Append(showAll)
                .Append(new MenuItem("-"))
                .Append(fgColor)
                .Append(bgColor)
                .Append(new MenuItem("-"))
                .Append(fitWidth)
                .Append(new MenuItem("-"))
                .Append(hideThis);

            return new ContextMenu(menuItems.ToArray());
        }

        private void HideColumns(object sender, EventArgs e)
        {
            var selectedColumns = _table.GetSelectedDataColumnIndexes(_clickedColumnIndex);
            var newColumnIndexes = ColumnSelector.ToIndexes(_stylingOptions.VisibleColumns, _table.DataColumnCount).Except(selectedColumns);
            var newSelector = ColumnSelector.FromIndexes(newColumnIndexes);
            SetColumnSelector(newSelector);
        }

        private void SelectPartialSubgroups(uint subgroupSize, uint displayedCount, bool displayLast)
        {
            //string subgroupsSelector = ColumnSelector.PartialSubgroups(_getGroupSize(), subgroupSize, displayedCount, displayLast);
            //string newSelector = ColumnSelector.GetSelectorMultiplication(_stylingOptions.VisibleColumns, subgroupsSelector, _table.DataColumnCount);
            //SetColumnSelector(newSelector);
            _stylingOptions.Range = new Utils.VisibleColumnsRange(displayLast ? Utils.SelectorType.Last : Utils.SelectorType.First,
                                            (int)displayedCount, (int)subgroupSize);
            _table.ClearSelection();
        }

        private void SetBackgroundColor(DataHighlightColor color)
        {
            var selectedColumns = _table.GetSelectedDataColumnIndexes(_clickedColumnIndex);
            _stylingOptions.BackgroundColors = DataHighlightColors.UpdateColorStringRange(_stylingOptions.BackgroundColors, selectedColumns, color, _table.DataColumnCount);
            _table.ClearSelection();
        }

        private void SetForegroundColor(DataHighlightColor color)
        {
            var selectedColumns = _table.GetSelectedDataColumnIndexes(_clickedColumnIndex);
            _stylingOptions.ForegroundColors = DataHighlightColors.UpdateColorStringRange(_stylingOptions.ForegroundColors, selectedColumns, color, _table.DataColumnCount);
            _table.ClearSelection();
        }

        private void SetColumnSelector(string newSelector)
        {
            //_stylingOptions.VisibleColumns = newSelector;
            //_table.ClearSelection();
            _stylingOptions.Range = new Utils.VisibleColumnsRange(newSelector);
            _table.ClearSelection();
        }

        private MenuItem[] CreatePartialSubgroupMenu(uint minSubgroupSize, uint maxSubgroupSize, bool displayLast) =>
            PowersOfTwo(from: minSubgroupSize, upto: maxSubgroupSize / 2)
                .Select(displayedCount =>
                {
                    var groupSizeSubmenu = PowersOfTwo(from: displayedCount * 2, upto: maxSubgroupSize)
                        .Select(groupSize => new MenuItem(groupSize.ToString(),
                            (s, e) => SelectPartialSubgroups(groupSize, displayedCount, displayLast)))
                        .ToArray();
                    return new MenuItem(displayedCount.ToString(), groupSizeSubmenu);
                })
                .ToArray();

        private static IEnumerable<uint> PowersOfTwo(uint from, uint upto)
        {
            while (from <= upto)
            {
                yield return from;
                from *= 2;
            }
        }
    }
}
