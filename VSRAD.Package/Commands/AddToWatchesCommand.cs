﻿using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class AddToWatchesCommand : ICommandHandler
    {
        private readonly IToolWindowIntegration _toolIntegration;
        private readonly IActiveCodeEditor _codeEditor;
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public AddToWatchesCommand(IToolWindowIntegration toolIntegration, IActiveCodeEditor codeEditor, SVsServiceProvider serviceProvider)
        {
            _toolIntegration = toolIntegration;
            _codeEditor = codeEditor;
            _serviceProvider = serviceProvider;
        }

        public Guid CommandSet => Constants.AddToWatchesCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (commandId == Constants.MenuCommandId)
                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
            return 0;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId != Constants.MenuCommandId)
                return;

            var activeWord = _codeEditor.GetActiveWord();
            if (!string.IsNullOrWhiteSpace(activeWord))
                _toolIntegration.AddWatchFromEditor(activeWord);
        }
    }
}
