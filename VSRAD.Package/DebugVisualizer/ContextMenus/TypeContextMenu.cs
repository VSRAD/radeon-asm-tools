﻿using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.ContextMenus
{
    public sealed class TypeContextMenu : IContextMenu
    {
        public delegate void TypeChanged(int rowIndex, VariableInfo type);
        public delegate void AVGPRStateChanged(int rowIndex, bool state);
        public delegate void InsertRow(int rowIndex, bool after);

        private readonly VisualizerTable _table;
        private readonly ContextMenu _menu;
        private readonly MenuItem _avgprButton;
        private int _currentRow;

        public TypeContextMenu(VisualizerTable table, TypeChanged typeChanged, AVGPRStateChanged avgprChanged, Action processCopy, InsertRow insertRow)
        {
            _table = table;

            var typeItems = new MenuItem[]
            {
                new MenuItem("Hex", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Hex, 32))),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Hex, 16))),
                    new MenuItem("8" , (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Hex,  8)))
                }),
                new MenuItem("Int", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Int, 32))),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Int, 16))),
                    new MenuItem("8" , (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Int,  8)))
                }),
                new MenuItem("UInt", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Uint, 32))),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Uint, 16))),
                    new MenuItem("8" , (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Uint,  8)))
                }),
                new MenuItem("Float", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Float, 32))),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Float, 16)))
                }),
                new MenuItem("Bin", new MenuItem[]
                {
                    new MenuItem("32", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Bin, 32))),
                    new MenuItem("16", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Bin, 16))),
                    new MenuItem("8" , (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Bin,  8)))
                }),
                new MenuItem("Half", (s, e) => typeChanged(_currentRow, new VariableInfo(VariableType.Half, 0)))
            };

            var fgColor = new MenuItem("Font Color", new[]
            {
                new MenuItem("Green", (s, e) => _table.ApplyRowHighlight(_currentRow, changeFg: DataHighlightColor.Green)),
                new MenuItem("Red", (s, e) => _table.ApplyRowHighlight(_currentRow, changeFg: DataHighlightColor.Red)),
                new MenuItem("Blue", (s, e) => _table.ApplyRowHighlight(_currentRow, changeFg: DataHighlightColor.Blue)),
                new MenuItem("None", (s, e) => _table.ApplyRowHighlight(_currentRow, changeFg: DataHighlightColor.None))
            });
            var bgColor = new MenuItem("Background Color", new[]
            {
                new MenuItem("Green", (s, e) => _table.ApplyRowHighlight(_currentRow, changeBg: DataHighlightColor.Green)),
                new MenuItem("Red", (s, e) => _table.ApplyRowHighlight(_currentRow, changeBg: DataHighlightColor.Red)),
                new MenuItem("Blue", (s, e) => _table.ApplyRowHighlight(_currentRow, changeBg: DataHighlightColor.Blue)),
                new MenuItem("None", (s, e) => _table.ApplyRowHighlight(_currentRow, changeBg: DataHighlightColor.None))
            });

            var insertRowBefore = new MenuItem("Insert Row Before", (s, e) => insertRow(_currentRow, false));
            var insertRowAfter = new MenuItem("Insert Row After", (s, e) => insertRow(_currentRow, true));

            _avgprButton = new MenuItem("AVGPR", (s, e) =>
            {
                _avgprButton.Checked = !_avgprButton.Checked;
                avgprChanged(_currentRow, _avgprButton.Checked);
            });

            var copy = new MenuItem("Copy", (s, e) => processCopy());

            var menuItems = typeItems.Concat(new[]
            {
                new MenuItem("-"),
                fgColor,
                bgColor,
                new MenuItem("-"),
                copy,
                new MenuItem("-"),
                insertRowBefore,
                insertRowAfter
                //_avgprButton
            });

            _menu = new ContextMenu(menuItems.ToArray());
        }

        public bool Show(MouseEventArgs e, DataGridView.HitTestInfo hit)
        {
            if (hit.RowIndex == _table.NewWatchRowIndex || hit.RowIndex == -1) return false;
            if (hit.ColumnIndex != VisualizerTable.NameColumnIndex && hit.ColumnIndex != -1) return false;

            _currentRow = hit.RowIndex;

            foreach (MenuItem item in _menu.MenuItems)
                item.Checked = false;

            var selectedWatch = VisualizerTable.GetRowWatchState(_table.Rows[hit.RowIndex]);
            _avgprButton.Enabled = _currentRow != 0 || !_table.ShowSystemRow;
            _avgprButton.Checked = selectedWatch.IsAVGPR;

            _menu.Show(_table, new Point(e.X, e.Y));
            return true;
        }
    }
}
