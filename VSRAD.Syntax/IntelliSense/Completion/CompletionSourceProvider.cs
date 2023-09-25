﻿using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.IntelliSense.Completion.Providers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(CompletionSourceProvider))]
    internal class CompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly OptionsProvider _optionsEventProvider;
        private readonly IIntelliSenseDescriptionBuilder _descriptionBuilder;
        private readonly IDocumentFactory _documentFactory;
        private readonly IReadOnlyList<RadCompletionProvider> _providers;

        [ImportingConstructor]
        public CompletionSourceProvider(OptionsProvider optionsEventProvider,
            IInstructionListManager instructionListManager,
            IIntelliSenseDescriptionBuilder descriptionBuilder,
            IDocumentFactory documentFactory, 
            IIntelliSenseService intelliSenseService)
        {
            _optionsEventProvider = optionsEventProvider;
            _descriptionBuilder = descriptionBuilder;
            _documentFactory = documentFactory;

            _providers = new List<RadCompletionProvider>()
            {
                new InstructionCompletionProvider(optionsEventProvider, instructionListManager),
                new FunctionCompletionProvider(optionsEventProvider, intelliSenseService),
                new ScopedCompletionProvider(optionsEventProvider, intelliSenseService),
            };
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView == null)
                throw new ArgumentNullException(nameof(textView));

            var document = _documentFactory.GetOrCreateDocument(textView.TextBuffer);
            if (document == null) return null;

            return textView.Properties.GetOrCreateSingletonProperty(() => 
                new CompletionSource(document, _descriptionBuilder, _providers));
        }
    }
}
