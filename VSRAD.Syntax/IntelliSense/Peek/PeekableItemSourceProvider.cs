﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    [Export(typeof(IPeekableItemSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(PeekableItemSourceProvider))]
    [SupportsStandaloneFiles(true)]
    [SupportsPeekRelationship("IsDefinedBy")]
    internal sealed class PeekableItemSourceProvider : IPeekableItemSourceProvider
    {
        private readonly RadeonServiceProvider _serviceProvider;
        private readonly IIntelliSenseService _intelliSenseService;

        [ImportingConstructor]
        public PeekableItemSourceProvider(
            RadeonServiceProvider serviceProvider,
            IIntelliSenseService intelliSenseService)
        {
            _serviceProvider = serviceProvider;
            _intelliSenseService = intelliSenseService;
        }

        public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            return textBuffer.Properties.GetOrCreateSingletonProperty(
                () => new PeekableItemSource(textBuffer, _serviceProvider.PeekResultFactory, _intelliSenseService));
        }
    }
}