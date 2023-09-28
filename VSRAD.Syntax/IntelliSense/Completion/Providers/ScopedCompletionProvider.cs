﻿using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using System.Threading;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class ScopedCompletionProvider : RadCompletionProvider
    {
        private static readonly ImageElement LabelIcon = GetImageElement(KnownImageIds.Label);
        private static readonly ImageElement GlobalVariableIcon = GetImageElement(KnownImageIds.GlobalVariable);
        private static readonly ImageElement LocalVariableIcon = GetImageElement(KnownImageIds.LocalVariable);
        private static readonly ImageElement ArgumentIcon = GetImageElement(KnownImageIds.Parameter);
        private readonly IIntelliSenseService _intelliSenseService;

        private bool _autocompleteLabels;
        private bool _autocompleteVariables;

        public ScopedCompletionProvider(OptionsProvider optionsProvider, IIntelliSenseService intelliSenseService)
            : base(optionsProvider)
        {
            _intelliSenseService = intelliSenseService;
            _autocompleteLabels = optionsProvider.AutocompleteLabels;
            _autocompleteVariables = optionsProvider.AutocompleteVariables;
        }

        public override void DisplayOptionsUpdated(OptionsProvider sender)
        {
            _autocompleteLabels = sender.AutocompleteLabels;
            _autocompleteVariables = sender.AutocompleteVariables;
        }

        public override async Task<RadCompletionContext> GetContextAsync(IDocument document, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken)
        {
            if (!_autocompleteLabels && !_autocompleteVariables) return RadCompletionContext.Empty;

            var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(triggerLocation.Snapshot);
            var completions = GetScopedCompletions(document, analysisResult, triggerLocation, cancellationToken);

            return new RadCompletionContext(completions.ToList());
        }

        private IEnumerable<CompletionItem> GetScopedCompletions(IDocument document, IAnalysisResult analysisResult, SnapshotPoint triggerPoint, CancellationToken cancellationToken)
        {
            CompletionItem CreateCompletionItem(AnalysisToken analysisToken, ImageElement imageElement) =>
                new CompletionItem(_intelliSenseService.GetIntelliSenseInfo(document, analysisToken), imageElement);

            var currentBlock = analysisResult.GetBlock(triggerPoint);
            while (currentBlock != null)
            {
                foreach (var token in currentBlock.Tokens)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_autocompleteLabels && token.Type == RadAsmTokenType.Label)
                    {
                        yield return CreateCompletionItem(token, LabelIcon); break;
                    }
                    else if (_autocompleteVariables)
                    {
                        switch (token.Type)
                        {
                            case RadAsmTokenType.GlobalVariable:
                                yield return CreateCompletionItem(token, GlobalVariableIcon); break;
                            case RadAsmTokenType.LocalVariable:
                                yield return CreateCompletionItem(token, LocalVariableIcon); break;
                            case RadAsmTokenType.FunctionParameter:
                                yield return CreateCompletionItem(token, ArgumentIcon); break;
                        }
                    }
                }

                currentBlock = currentBlock.Parent;
            }
        }
    }
}
