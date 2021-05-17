﻿using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using System;
using VSRAD.Syntax.IntelliSense.Completion.Providers;
using CompletionItem = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal class CompletionSource : IAsyncCompletionSource
    {
        private readonly IDocument _document;
        private readonly IIntellisenseDescriptionBuilder _descriptionBuilder;
        private readonly IReadOnlyList<RadCompletionProvider> _completionProviders;

        public CompletionSource(IDocument document,
            IIntellisenseDescriptionBuilder descriptionBuilder,
            IReadOnlyList<RadCompletionProvider> providers)
        {
            _document = document;
            _descriptionBuilder = descriptionBuilder;
            _completionProviders = providers;
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) =>
            ShouldTriggerCompletion(trigger, triggerLocation)
                ? new CompletionStartData(CompletionParticipation.ProvidesItems, triggerLocation.GetExtent().Span)
                : CompletionStartData.DoesNotParticipateInCompletion;

        public async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken)
        {
            var completionContexts = await ComputeNonEmptyCompletionContextsAsync(triggerLocation, applicableToSpan, cancellationToken);

            if (completionContexts.Length == 0)
                return null;

            var completionItems = new LinkedList<CompletionItem>();
            foreach (var context in completionContexts)
            {
                foreach (var item in context.Items)
                {
                    var completionItem = item.CreateVsCompletionItem(this);
                    completionItems.AddLast(completionItem);
                }
            }

            return new CompletionContext(completionItems.ToImmutableArray());
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) =>
            item.GetDescriptionAsync(_descriptionBuilder, token);

        private async Task<ImmutableArray<RadCompletionContext>> ComputeNonEmptyCompletionContextsAsync(SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken)
        {
            var completionContextTasks = _completionProviders.Select(provider => 
                provider.GetContextAsync(_document, triggerLocation, applicableToSpan, cancellationToken))
                .ToList();

            var completionContexts = await Task.WhenAll(completionContextTasks).ConfigureAwait(false);
            return completionContexts.Where(c => c.Items.Count > 0).ToImmutableArray();
        }

        private bool ShouldTriggerCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation)
        {
            if (triggerLocation < 3 || triggerLocation == triggerLocation.Snapshot.Length)
                return false;

            if (trigger.Reason == CompletionTriggerReason.Insertion && (trigger.Character == '\n' || trigger.Character == '\t')
                || trigger.Reason == CompletionTriggerReason.Deletion
                || trigger.Reason == CompletionTriggerReason.Backspace)
            {
                return false;
            }

            var extend = triggerLocation.GetExtent();
            if (extend.Span.Length < 3)
                return false;

            try
            {
                // currentLocation has the next position behind the cursor position
                var currentLocation = triggerLocation - 1;
                var currentToken = _document.DocumentTokenizer.CurrentResult.GetToken(currentLocation);

                var currentTokenType = _document.DocumentTokenizer.GetTokenType(currentToken.Type);
                if (currentTokenType == RadAsmTokenType.Comment 
                    || currentTokenType == RadAsmTokenType.Whitespace)
                {
                    return false;
                }

                var currentLine = currentLocation.GetContainingLine();
                var checkSpanEnd = currentToken.Start.GetPoint(currentLocation.Snapshot);
                var checkSpan = new SnapshotSpan(currentLine.Start, checkSpanEnd);

                var currentLineTokenTypes = _document.DocumentTokenizer
                    .CurrentResult
                    .GetTokens(checkSpan)
                    .Select(t => _document.DocumentTokenizer.GetTokenType(t.Type))
                    .Where(t => t != RadAsmTokenType.Whitespace);

                // if current line is a function signature or variable declaration
                // then do not trigger completion
                var tokenTypes = currentLineTokenTypes.ToList();
                if (tokenTypes.Any() && 
                    tokenTypes.All(t => t == RadAsmTokenType.Keyword))
                {
                    return false;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // outdated parser results so you cannot get valid tokens for the trigger location
                return false;
            }

            return true;
        }
    }
}
