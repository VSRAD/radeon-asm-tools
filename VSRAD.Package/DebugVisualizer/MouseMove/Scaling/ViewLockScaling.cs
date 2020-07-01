﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VSRAD.Package.DebugVisualizer.MouseMove.Scaling
{
    class ViewLockScaling
    {
        private readonly DataGridView _table;
        private TableState _tableState;
        private ScaleState _scaleState;
        private float _currentFullDiff = 0;

        public ViewLockScaling(DataGridView table, TableState tableState, ScaleState scaleState)
        {
            _table = table;
            _tableState = tableState;
            _scaleState = scaleState;
        }

        public void ApplyScaling(int diff)
        {
            ScaleDataColumns(diff);
        }

        public void ScaleDataColumns(int diff)
        {
            _currentFullDiff += diff;
            if (diff == 0 || Math.Abs(_currentFullDiff) < _scaleState.VisibleBetweenFirstAndTarget)
                return;

            diff = (int)(_currentFullDiff / _scaleState.VisibleBetweenFirstAndTarget);
            _currentFullDiff = 0;

            var width = _scaleState.TargetColumn.Width + diff;
            if (width < 30)
                return;

            _tableState.ResizeController.BeginBulkColumnWidthChange();

            var scrollingOffset = (int)(width * _scaleState.VisibleColumnsToLeftOutOfView + width * _scaleState.FirstColumnInvisisblePart);

            for (int i = _tableState.DataColumnOffset; i < _table.ColumnCount; ++i)
            {
                if (i == _scaleState.FirstVisibleIndex || i == _tableState.PhantomColumnIndex) continue;
                _table.Columns[i].Width = width;
            }

            _table.Columns[_scaleState.FirstVisibleIndex].Width += diff;
            if (scrollingOffset < 0)
                _table.Columns[_scaleState.FirstVisibleIndex].Width += Math.Abs(scrollingOffset);

            var maxScrollingOffset = _tableState.ResizeController.GetTotalWidthInBulkColumnWidthChange() - _scaleState.TableDataAreaWidth;

            if (scrollingOffset > maxScrollingOffset)
                _table.Columns[_tableState.PhantomColumnIndex].Width += scrollingOffset - maxScrollingOffset;

            _scaleState.CurrentWidth = width;
            _tableState.ColumnWidth = width;

            _tableState.ResizeController.CommitBulkColumnWidthChange(scrollingOffset);
        }
    }
}
