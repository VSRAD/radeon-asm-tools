﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System;

namespace VSRAD.Syntax.IntelliSense
{
    internal class IntelliSenseController : IOleCommandTarget
    {
        private readonly ITextView _textView;
        private readonly RadeonServiceProvider _editorService;
        private readonly IIntelliSenseService _intelliSenseService;

        public IOleCommandTarget Next { get; set; }

        public IntelliSenseController(RadeonServiceProvider editorService, IIntelliSenseService intelliSenseService, ITextView textView)
        {
            _textView = textView;
            _editorService = editorService;
            _intelliSenseService = intelliSenseService;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                for (int i = 0; i < cCmds; i++)
                {
                    switch ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID)
                    {
                        case VSConstants.VSStd97CmdID.GotoDefn:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;
                    }
                }
            }
            else if (pguidCmdGroup == VSConstants.VsStd12)
            {
                for (int i = 0; i < cCmds; i++)
                {
                    switch ((VSConstants.VSStd12CmdID)prgCmds[i].cmdID)
                    {
                        case VSConstants.VSStd12CmdID.PeekDefinition:
                            var canPeek = _editorService.PeekBroker.CanTriggerPeekSession(
                                _textView,
                                PredefinedPeekRelationships.Definitions.Name,
                                isStandaloneFilePredicate: (string filename) => false
                            );
                            prgCmds[i].cmdf = (uint)OLECMDF.OLECMDF_SUPPORTED;
                            prgCmds[0].cmdf |= (uint)(canPeek == true ? OLECMDF.OLECMDF_ENABLED : OLECMDF.OLECMDF_INVISIBLE);
                            return VSConstants.S_OK;
                    }
                }
            }

            return Next.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                switch ((VSConstants.VSStd97CmdID)nCmdID)
                {
                    case VSConstants.VSStd97CmdID.GotoDefn:
                        if (TryGoToDefinition()) return VSConstants.S_OK;
                        break;
                }
            }
            else if (pguidCmdGroup == VSConstants.VsStd12)
            {
                switch ((VSConstants.VSStd12CmdID)nCmdID)
                {
                    case VSConstants.VSStd12CmdID.PeekDefinition:
                        if (TryTriggerPeekDefinition()) return VSConstants.S_OK;
                        break;
                }
            }

            return Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool TryGoToDefinition()
        {
            var point = _textView.Caret.Position.BufferPosition;

            var intelliSenseToken = ThreadHelper.JoinableTaskFactory.Run(() => _intelliSenseService.GetIntelliSenseInfoAsync(point));
            if (intelliSenseToken == null || intelliSenseToken.Definitions.Count == 0) return false;

            _intelliSenseService.NavigateOrOpenNavigationList(intelliSenseToken.Definitions);
            return true;
        }

        private bool TryTriggerPeekDefinition()
        {
            if (_textView.Roles.Contains(PredefinedTextViewRoles.EmbeddedPeekTextView) ||
                _textView.Roles.Contains(PredefinedTextViewRoles.CodeDefinitionView)) 
                return false;

            var peekSession = _editorService
                .PeekBroker.TriggerPeekSession(_textView, 
                    PredefinedPeekRelationships.Definitions.Name);

            return peekSession != null;
        }
    }
}
