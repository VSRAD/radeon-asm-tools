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
        private readonly IIntelliSenseDescriptionBuilder _descriptionBuilder;
        private readonly IReadOnlyList<RadCompletionProvider> _completionProviders;

        public CompletionSource(IDocument document,
            IIntelliSenseDescriptionBuilder descriptionBuilder,
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
            var completionContextTasks = new List<Task<RadCompletionContext>>();
            foreach (var provider in _completionProviders)
                completionContextTasks
                    .Add(provider.GetContextAsync(_document, triggerLocation, applicableToSpan, cancellationToken));

            var completionContexts = await Task.WhenAll(completionContextTasks).ConfigureAwait(false);
            return completionContexts.Where(c => c.Items.Count > 0).ToImmutableArray();
        }

        private bool ShouldTriggerCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation)
        {
            if (triggerLocation == triggerLocation.Snapshot.Length)
                return false;

            try
            {
                var currentToken = _document.DocumentTokenizer.CurrentResult.GetToken(triggerLocation);
                var currentTokenType = _document.DocumentTokenizer.GetTokenType(currentToken.Type);
                if (currentTokenType == RadAsmTokenType.Comment)
                {
                    return false;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // outdated parser results so you cannot get valid tokens for the trigger location
                return false;
            }

            if (trigger.Reason == CompletionTriggerReason.Invoke
                    || trigger.Reason == CompletionTriggerReason.InvokeAndCommitIfUnique)
            {
                return true;
            }

            if (trigger.Reason == CompletionTriggerReason.Insertion && (trigger.Character == '\n' || trigger.Character == '\t')
                || trigger.Reason == CompletionTriggerReason.Deletion
                || trigger.Reason == CompletionTriggerReason.Backspace)
            {
                return false;
            }

            var extend = triggerLocation.GetExtent();
            if (extend.Span.Length < 3)
                return false;

            return true;
        }
    }
}
