﻿using Antlr4.Runtime;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Core.Lexer
{
    class AsmDocLexer : ILexer
    {
        public static ILexer Instance = new AsmDocLexer();

        public IEnumerable<TokenSpan> Run(IEnumerable<string> textSegments, int offset)
        {
            var lexer = new RadAsmDocLexer(new UnbufferedCharStream(new TextSegmentsCharStream(textSegments)));
            while (true)
            {
                IToken current = lexer.NextToken();
                if (current.Type == RadAsmDocLexer.Eof)
                    break;
                yield return new TokenSpan(current.Type, new Span(current.StartIndex + offset, current.StopIndex - current.StartIndex + 1));
            }
        }

        public RadAsmTokenType LexerTokenToRadAsmToken(int type) => _tt[type];

        private static readonly Dictionary<int, RadAsmTokenType> _tt = new Dictionary<int, RadAsmTokenType>()
        {
            { RadAsmDocLexer.LET, RadAsmTokenType.Keyword },
            { RadAsmDocLexer.TARGETS, RadAsmTokenType.Keyword },
            { RadAsmDocLexer.COMMA, RadAsmTokenType.Comma },
            { RadAsmDocLexer.LCURVEBRACKET, RadAsmTokenType.LcurveBracket },
            { RadAsmDocLexer.RCURVEBRACKET, RadAsmTokenType.RcurveBracket },
            { RadAsmDocLexer.IDENTIFIER, RadAsmTokenType.Identifier },
            { RadAsmDocLexer.IDENTIFIER_LIST, RadAsmTokenType.Identifier },
            { RadAsmDocLexer.BLOCK_COMMENT, RadAsmTokenType.Comment },
            { RadAsmDocLexer.WHITESPACE, RadAsmTokenType.Whitespace },
            { RadAsmDocLexer.EOL, RadAsmTokenType.Whitespace },
            { RadAsmDocLexer.UNKNOWN, RadAsmTokenType.Unknown },
        };
    }
}
