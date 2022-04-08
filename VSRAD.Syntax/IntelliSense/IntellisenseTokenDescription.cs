﻿using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Core;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using System.Threading;
using Microsoft.VisualStudio.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Language.StandardClassification;

namespace VSRAD.Syntax.IntelliSense
{
    public interface IIntellisenseDescriptionBuilder
    {
        Task<object> GetColorizedDescriptionAsync(IReadOnlyList<NavigationToken> tokens, CancellationToken cancellationToken);
        Task<object> GetColorizedDescriptionAsync(NavigationToken token, CancellationToken cancellationToken);
    }

    [Export(typeof(IIntellisenseDescriptionBuilder))]
    internal class IntellisenseDescriptionBuilder : IIntellisenseDescriptionBuilder
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly INavigationTokenService _navigationTokenService;

        [ImportingConstructor]
        public IntellisenseDescriptionBuilder(IDocumentFactory documentFactory, INavigationTokenService navigationTokenService)
        {
            _documentFactory = documentFactory;
            _navigationTokenService = navigationTokenService;
        }

        public async Task<object> GetColorizedDescriptionAsync(IReadOnlyList<NavigationToken> tokens, CancellationToken cancellationToken)
        {
            if (tokens == null || tokens.Count == 0) return null;
            else if (tokens.Count == 1) return await GetColorizedDescriptionAsync(tokens[0], cancellationToken);
            else return GetColorizedDescriptions(tokens, cancellationToken);
        }

        private object GetColorizedDescriptions(IReadOnlyList<NavigationToken> tokens, CancellationToken cancellationToken)
        {
            var builder = new ClassifiedTextBuilder();
            foreach (var tokenGroup in tokens.GroupBy(t => t.Path))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = tokenGroup.All(t => t.Type == RadAsmTokenType.Instruction)
                    ? Path.GetFileNameWithoutExtension(tokenGroup.Key)
                    : tokenGroup.Key;

                builder.AddClassifiedText(filePath).SetAsElement();
                foreach (var token in tokenGroup)
                {
                    var typeName = token.Type.GetName();
                    var textBeforeToken = token.LineText.Substring(0, token.LineTokenStart);
                    var textAfterToken = token.LineText.Substring(token.LineTokenEnd);

                    builder.AddClassifiedText($"({typeName}) ")
                        .AddClassifiedText(textBeforeToken)
                        .AddClassifiedText(token)
                        .AddClassifiedText(textAfterToken)
                        .SetAsElement();
                }
            }

            return builder.Build();
        }

        public async Task<object> GetColorizedDescriptionAsync(NavigationToken token, CancellationToken cancellationToken)
        {
            if (token == NavigationToken.Empty) return null;
            cancellationToken.ThrowIfCancellationRequested();

            var typeName = token.Type.GetName();
            var typeImage = token.Type.GetImageElement();
            var document = _documentFactory.GetOrCreateDocument(token.AnalysisToken.Snapshot.TextBuffer);

            var builder = new ClassifiedTextBuilder();
            builder.AddElement(typeImage);

            if (token.Type == RadAsmTokenType.Instruction)
            {
                builder
                    .AddClassifiedText($"({typeName} ")
                    .AddClassifiedText(RadAsmTokenType.Instruction, Path.GetFileNameWithoutExtension(token.Path))
                    .AddClassifiedText(") ");
            }
            else
            {
                builder.AddClassifiedText($"({typeName}) ");
            }

            builder.AddClassifiedText(token);
            if (token.Type == RadAsmTokenType.FunctionName)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var documentAnalysis = await document.DocumentAnalysis.GetAnalysisResultAsync(token.AnalysisToken.Snapshot);
                var block = documentAnalysis.GetBlock(token.GetEnd());

                cancellationToken.ThrowIfCancellationRequested();

                if (block is FunctionBlock functionBlock)
                {
                    for (var i = 0; i < functionBlock.Parameters.Count; i++)
                    {
                        builder.AddClassifiedText(" ")
                            .AddClassifiedText(_navigationTokenService.CreateToken(functionBlock.Parameters[i], document));
                        if (i != functionBlock.Parameters.Count - 1)
                            builder.AddClassifiedText(",");
                    }
                }
            }
            else if (token.Type == RadAsmTokenType.GlobalVariable || token.Type == RadAsmTokenType.LocalVariable)
            {
                var variableToken = (VariableToken)token.AnalysisToken;
                if (variableToken.DefaultValue != default)
                {
                    var defaultValue = variableToken.DefaultValue.GetText(variableToken.Snapshot);
                    builder.AddClassifiedText(" = ")
                        .AddClassifiedText(RadAsmTokenType.Number, defaultValue);
                }
            }

            builder.SetAsElement();
            if (TryGetCommentDescription(document.DocumentTokenizer, token.GetEnd(), cancellationToken, out var message))
                builder.AddClassifiedText(message).SetAsElement();
            return builder.Build();
        }

        private bool TryGetCommentDescription(IDocumentTokenizer documentTokenizer, SnapshotPoint tokenEnd, CancellationToken cancellationToken, out string message)
        {
            var snapshot = tokenEnd.Snapshot;
            var currentLine = tokenEnd.GetContainingLine();
            var tokenizerResult = documentTokenizer.CurrentResult;

            var tokenLineComment = tokenizerResult
                .GetTokens(new Span(tokenEnd, currentLine.End - tokenEnd))
                .Where(t => documentTokenizer.GetTokenType(t.Type) == RadAsmTokenType.Comment);

            if (tokenLineComment.Any())
            {
                var text = tokenLineComment.First().GetText(snapshot);
                message = GetCommentText(text);
                return true;
            }

            var lines = new LinkedList<string>();
            var currentLineNumber = currentLine.LineNumber - 1;
            while (currentLineNumber >= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                currentLine = snapshot.GetLineFromLineNumber(currentLineNumber);
                var tokensAtLine = tokenizerResult
                    .GetTokens(new Span(currentLine.Start, currentLine.Length))
                    .Where(t => documentTokenizer.GetTokenType(t.Type) != RadAsmTokenType.Whitespace);

                if (tokensAtLine.Count() == 1)
                {
                    var trackingToken = tokensAtLine.First();
                    if (documentTokenizer.GetTokenType(trackingToken.Type) == RadAsmTokenType.Comment)
                    {
                        var tokenSpan = new SnapshotSpan(snapshot, trackingToken.GetSpan(snapshot));

                        lines.AddFirst(tokenSpan.GetText());
                        currentLineNumber = tokenSpan.Start.GetContainingLine().LineNumber - 1;
                        continue;
                    }
                }

                break;
            }

            if (lines.Count != 0)
            {
                message = GetCommentText(string.Join(System.Environment.NewLine, lines));
                return true;
            }

            message = null;
            return false;
        }

        private static string GetCommentText(string text)
        {
            var comment = text.Trim('/', '*', ' ', '\t', '\r', '\n', '\f');
            return Regex.Replace(comment, @"(?<=\n)\s*(\*|\/\/)", "", RegexOptions.Compiled);
        }

        public class ClassifiedTextBuilder
        {
            private readonly LinkedList<ClassifiedTextRun> _classifiedTextRuns;
            private readonly LinkedList<ContainerElement> _containers;
            private readonly LinkedList<object> _elements;

            public ClassifiedTextBuilder()
            {
                _classifiedTextRuns = new LinkedList<ClassifiedTextRun>();
                _containers = new LinkedList<ContainerElement>();
                _elements = new LinkedList<object>();
            }

            public ContainerElement Build() =>
                new ContainerElement(ContainerElementStyle.Stacked, _containers);

            public ClassifiedTextBuilder SetAsElement()
            {
                if (_classifiedTextRuns.Count > 0)
                {
                    var textElement = new ClassifiedTextElement(_classifiedTextRuns);
                    _elements.AddLast(textElement);
                    _classifiedTextRuns.Clear();
                }

                var container = new ContainerElement(ContainerElementStyle.Wrapped, _elements);
                _containers.AddLast(container);
                _elements.Clear();

                return this;
            }

            public ClassifiedTextBuilder AddElement(object element)
            {
                _elements.AddLast(element);
                return this;
            }

            public ClassifiedTextBuilder AddClassifiedText(string classificationName, string text)
            {
                _classifiedTextRuns.AddLast(new ClassifiedTextRun(classificationName, text));
                return this;
            }

            public ClassifiedTextBuilder AddClassifiedText(string classificationName, string text, System.Action navigation)
            {
                _classifiedTextRuns.AddLast(new ClassifiedTextRun(classificationName, text, navigation));
                return this;
            }

            public ClassifiedTextBuilder AddClassifiedText(string text) =>
                AddClassifiedText(PredefinedClassificationTypeNames.Other, text);

            public ClassifiedTextBuilder AddClassifiedText(NavigationToken navigationToken) =>
                AddClassifiedText(navigationToken.Type.GetClassificationTypeName(), navigationToken.GetText(), navigationToken.Navigate);

            public ClassifiedTextBuilder AddClassifiedText(RadAsmTokenType tokenType, string text) =>
                AddClassifiedText(tokenType.GetClassificationTypeName(), text);
        }
    }
}
