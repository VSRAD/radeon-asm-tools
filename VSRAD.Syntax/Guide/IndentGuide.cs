﻿using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Blocks;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Formatting;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Windows.Media;
using Task = System.Threading.Tasks.Task;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using Microsoft.VisualStudio.Text;
using System.Threading;

namespace VSRAD.Syntax.Guide
{
    internal sealed class IndentGuide
    {
        private readonly IWpfTextView _textView;
        private readonly IAdornmentLayer _layer;
        private readonly Canvas _canvas;
        private readonly IDocumentAnalysis _documentAnalysis;
        private readonly OptionsProvider _optionsProvider;
        private double _thickness;
        private double _dashSize;
        private double _spaceSize;
        private double _offsetX;
        private double _offsetY;
        private AnalysisResult _currentResult;
        private IList<Line> _currentAdornments;
        private bool _isEnabled;
        private int _tabSize;

        public IndentGuide(IWpfTextView textView, IDocumentAnalysis documentAnalysis, OptionsProvider optionsProvider)
        {
            _textView = textView ?? throw new NullReferenceException();
            _documentAnalysis = documentAnalysis;
            _optionsProvider = optionsProvider;

            _currentAdornments = new List<Line>();
            _canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _layer = _textView.GetAdornmentLayer(Constants.IndentGuideAdornmentLayerName) ?? throw new NullReferenceException();
            _layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, null, _canvas, null);

            _textView.LayoutChanged += LayoutChanged;
            _documentAnalysis.AnalysisUpdated += AnalysisUpdated;
            _optionsProvider.OptionsUpdated += IndentGuideOptionsUpdated;
            _textView.Options.OptionChanged += TabSizeOptionsChanged;
            _textView.Closed += ViewClosedEventHandler;

            _tabSize = textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);
            IndentGuideOptionsUpdated(optionsProvider);
        }

        private void ViewClosedEventHandler(object sender, EventArgs e)
        {
            _optionsProvider.OptionsUpdated -= IndentGuideOptionsUpdated;
            _documentAnalysis.AnalysisUpdated -= AnalysisUpdated;
            _textView.Options.OptionChanged -= TabSizeOptionsChanged;
            _textView.LayoutChanged -= LayoutChanged;
            _textView.Closed -= ViewClosedEventHandler;

            _layer.RemoveAdornment(_canvas);
        }

        private void LayoutChanged(object s, TextViewLayoutChangedEventArgs e) =>
            UpdateIndentGuides();

        private void TabSizeOptionsChanged(object sender, EditorOptionChangedEventArgs e)
        {
            _tabSize = _textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);
            UpdateIndentGuides();
        }

        private void AnalysisUpdated(AnalysisResult analysisResult, RescanReason reason, CancellationToken ct)
        {
            if (reason != RescanReason.ContentChanged) return;

            _currentResult = analysisResult;
            UpdateIndentGuides();
        }

        private void IndentGuideOptionsUpdated(OptionsProvider sender)
        {
            if (sender.IsEnabledIndentGuides != _isEnabled
                || sender.IndentGuideThickness != _thickness
                || sender.IndentGuideDashSize != _dashSize
                || sender.IndentGuideSpaceSize != _spaceSize
                || sender.IndentGuideOffsetX != _offsetX
                || sender.IndentGuideOffsetY != _offsetY)
            {
                _isEnabled = sender.IsEnabledIndentGuides;
                _thickness = sender.IndentGuideThickness;
                _dashSize = sender.IndentGuideDashSize;
                _spaceSize = sender.IndentGuideSpaceSize;
                _offsetX = sender.IndentGuideOffsetX;
                _offsetY = sender.IndentGuideOffsetY;

                _currentResult = _documentAnalysis.CurrentResult;
                if (_isEnabled)
                    UpdateIndentGuides();
                else
                    CleanupIndentGuidesAsync().ConfigureAwait(false);
            }
        }

        private void UpdateIndentGuides()
        {
            if (_isEnabled && _currentResult != null)
                Task.Run(async () => await SetupIndentGuidesAsync())
                    .RunAsyncWithoutAwait();
        }

        private async Task CleanupIndentGuidesAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            foreach (var oldIndentGuide in _currentAdornments)
            {
                _canvas.Children.Remove(oldIndentGuide);
            }
            _currentAdornments = new List<Line>();
        }

        private async Task SetupIndentGuidesAsync()
        {
            try
            {
                if (_textView.InLayout)
                    return;

                var firstVisibleLine = _textView.TextViewLines.First(line => line.IsFirstTextViewLineForSnapshotLine);
                var lastVisibleLine = _textView.TextViewLines.Last(line => line.IsLastTextViewLineForSnapshotLine);

                var newSpanElements = _currentResult
                    .Scopes
                    .Where(b => b.Type != BlockType.Root && b.Type != BlockType.Comment && IsInVisualBuffer(b, firstVisibleLine, lastVisibleLine));

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var updatedIndentGuides = GetUpdatedIndentGuides(newSpanElements, firstVisibleLine.TextLeft, firstVisibleLine.VirtualSpaceWidth);

                ClearAndUpdateCurrentGuides(updatedIndentGuides);
            }
            catch (ObjectDisposedException)
            {
                // If the buffer was changed during the calculation of the first and last line
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        private bool IsInVisualBuffer(IBlock block, ITextViewLine firstVisibleLine, ITextViewLine lastVisibleLine)
        {
            var blockStart = block.Area.GetStart(_textView.TextSnapshot);
            var blockEnd = block.Area.GetEnd(_textView.TextSnapshot);

            bool isOnStart = blockStart <= lastVisibleLine.End;
            bool isOnEnd = blockEnd >= firstVisibleLine.End;

            bool isInBlockAll = (blockStart <= firstVisibleLine.End) && (blockEnd >= lastVisibleLine.Start);

            return isOnStart && isOnEnd || isInBlockAll;
        }

        private IEnumerable<Line> GetUpdatedIndentGuides(IEnumerable<IBlock> blocks, double horizontalOffset, double spaceWidth)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (var block in blocks)
            {
                var pointStart = new SnapshotPoint(_textView.TextSnapshot, block.Area.GetStart(_textView.TextSnapshot));
                var pointEnd = new SnapshotPoint(_textView.TextSnapshot, block.Area.GetEnd(_textView.TextSnapshot));

                var viewLineStart = _textView.GetTextViewLineContainingBufferPosition(pointStart);
                var viewLineEnd = _textView.GetTextViewLineContainingBufferPosition(pointEnd);

                if (viewLineStart.Equals(viewLineEnd)) continue;

                var lineStart = pointStart.GetContainingLine();
                var spaceText = new SnapshotSpan(lineStart.Start, pointStart).GetText();
                var tabs = spaceText.Count(ch => ch == '\t');

                var indentStart = (spaceText.Length - tabs) + tabs * _tabSize;
                var leftOffset = indentStart * spaceWidth + horizontalOffset + _offsetX;

                yield return new Line()
                {
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = _thickness,
                    StrokeDashArray = new DoubleCollection() { _dashSize, _spaceSize },
                    X1 = leftOffset,
                    X2 = leftOffset,
                    Y1 = (viewLineStart.Top != 0) ? viewLineStart.Bottom + _offsetY : _textView.ViewportTop,
                    Y2 = (viewLineEnd.Top != 0) ? viewLineEnd.Top - _offsetY : _textView.ViewportBottom,
                };
            }
        }

        private void ClearAndUpdateCurrentGuides(IEnumerable<Line> newIndentGuides)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _canvas.Visibility = Visibility.Visible;

            foreach (var oldIndentGuide in _currentAdornments)
            {
                _canvas.Children.Remove(oldIndentGuide);
            }

            _currentAdornments = newIndentGuides.ToList();

            foreach (var newIndentGuide in _currentAdornments)
            {
                _canvas.Children.Add(newIndentGuide);
            }
        }
    }
}