﻿using Microsoft.VisualStudio.Text;
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
        private static readonly ImageElement LabelIcon = RadAsmTokenType.Label.GetImageElement();
        private static readonly ImageElement GlobalVariableIcon = RadAsmTokenType.GlobalVariable.GetImageElement();
        private static readonly ImageElement LocalVariableIcon = RadAsmTokenType.LocalVariable.GetImageElement();
        private readonly INavigationTokenService _navigationTokenService;

        private bool _autocompleteLabels;
        private bool _autocompleteVariables;

        public ScopedCompletionProvider(GeneralOptionProvider generalOptionProvider, INavigationTokenService navigationTokenService)
            : base(generalOptionProvider)
        {
            _navigationTokenService = navigationTokenService;
            _autocompleteLabels = generalOptionProvider.AutocompleteLabels;
            _autocompleteVariables = generalOptionProvider.AutocompleteVariables;
        }

        public override void DisplayOptionsUpdated(GeneralOptionProvider sender)
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
            CompletionItem CreateCompletionItem(IAnalysisToken analysisToken, ImageElement imageElement) =>
                new CompletionItem(_navigationTokenService.CreateToken((IDefinitionToken)analysisToken, document), imageElement);

            var currentBlock = analysisResult.GetBlock(triggerPoint);
            while (currentBlock != null)
            {
                foreach (var token in currentBlock.Tokens)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_autocompleteLabels && token.Type == RadAsmTokenType.Label)
                    {
                        yield return CreateCompletionItem(token, LabelIcon);
                    }
                    else if (_autocompleteVariables)
                    {
                        switch (token.Type)
                        {
                            case RadAsmTokenType.GlobalVariable:
                                yield return CreateCompletionItem(token, GlobalVariableIcon); break;
                            case RadAsmTokenType.LocalVariable:
                            case RadAsmTokenType.FunctionParameter:
                                yield return CreateCompletionItem(token, LocalVariableIcon); break;
                        }
                    }
                }

                currentBlock = currentBlock.Parent;
            }
        }
    }
}
