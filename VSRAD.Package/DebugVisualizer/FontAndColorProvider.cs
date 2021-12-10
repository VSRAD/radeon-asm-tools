﻿using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using static Microsoft.VisualStudio.Shell.Package;

namespace VSRAD.Package.DebugVisualizer
{
    public interface IFontAndColorProvider
    {
        FontAndColorState FontAndColorState { get; }
    }

    public sealed class FontAndColorProvider : IFontAndColorProvider, IDisposable
    {
        public event Action FontAndColorInfoChanged;

        public FontAndColorState FontAndColorState { get; private set; }

        private readonly FontAndColorService _service;
        private readonly IVsFontAndColorStorage _storage;
        private const uint _storageFlags = (uint)(__FCSTORAGEFLAGS.FCSF_LOADDEFAULTS
            | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES
            | __FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS);

        public FontAndColorProvider()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _storage = (IVsFontAndColorStorage)GetGlobalService(typeof(SVsFontAndColorStorage));
            Assumes.Present(_storage);
            ErrorHandler.ThrowOnFailure(_storage.OpenCategory(Constants.FontAndColorsCategoryGuid, _storageFlags));

            _service = (FontAndColorService)GetGlobalService(typeof(FontAndColorService));
            _service.ItemsChanged += FontAndColorItemsChanged;
            FontAndColorState = new FontAndColorState(this);
        }

        public void Dispose()
        {
            _service.ItemsChanged -= FontAndColorItemsChanged;
        }

        private void FontAndColorItemsChanged()
        {
            FontAndColorState = new FontAndColorState(this);
            FontAndColorInfoChanged?.Invoke();
        }

        public (string name, float size) GetFontInfo()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var fontw = new LOGFONTW[1];
                var fontInfo = new FontInfo[1];
                ErrorHandler.ThrowOnFailure(_storage.GetFont(fontw, fontInfo));
                return (name: fontInfo[0].bstrFaceName, size: fontInfo[0].wPointSize);
            }
            catch
            {
                return (Control.DefaultFont.Name, Control.DefaultFont.Size);
            }
        }

        public (Color fg, Color bg, bool bold) GetInfo(FontAndColorItem item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetInfo(item.GetDisplayName());
        }

        public (Color fg, Color bg, bool bold) GetHighlightInfo(DataHighlightColor highlight)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetInfo(highlight.GetDisplayName());
        }

        public (Color fg, Color bg, bool bold) GetInfo(HeatmapColor item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetInfo(item.GetDisplayName());
        }

        private (Color fg, Color bg, bool bold) GetInfo(string item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var colorInfo = new ColorableItemInfo[1];
            ErrorHandler.ThrowOnFailure(_storage.GetItem(item, colorInfo));

            var fg = FontAndColorService.ReadVsColor(colorInfo[0].crForeground);
            var bg = FontAndColorService.ReadVsColor(colorInfo[0].crBackground);
            var isBold = ((FONTFLAGS)colorInfo[0].dwFontFlags & FONTFLAGS.FF_BOLD) == FONTFLAGS.FF_BOLD;

            return (fg, bg, isBold);
        }
    }
}
