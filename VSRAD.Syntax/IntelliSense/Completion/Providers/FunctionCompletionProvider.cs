﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;
using System.Threading;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class FunctionCompletionProvider : RadCompletionProvider
    {
        private static readonly ImageElement Icon = RadAsmTokenType.FunctionName.GetImageElement();
        private bool _autocompleteFunctions;
        private readonly INavigationTokenService _navigationTokenservice;

        public FunctionCompletionProvider(OptionsProvider optionsProvider, INavigationTokenService navigationTokenService)
            : base(optionsProvider)
        {
            _autocompleteFunctions = optionsProvider.AutocompleteFunctions;
            _navigationTokenservice = navigationTokenService;
        }

        public override void DisplayOptionsUpdated(OptionsProvider sender) =>
            _autocompleteFunctions = sender.AutocompleteFunctions;

        public override async Task<RadCompletionContext> GetContextAsync(IDocument document, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken)
        {
            if (!_autocompleteFunctions)
                return RadCompletionContext.Empty;

            var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(triggerLocation.Snapshot);
            var completionItems = analysisResult.Root.Tokens
                .AsParallel()
                .WithCancellation(cancellationToken)
                .Where(t => t.Type == Core.Tokens.RadAsmTokenType.FunctionName)
                .Select(t => _navigationTokenservice.CreateToken(t, document.Path))
                .Select(t => new CompletionItem(t, Icon));

            return new RadCompletionContext(completionItems.ToList());
        }
    }
}
