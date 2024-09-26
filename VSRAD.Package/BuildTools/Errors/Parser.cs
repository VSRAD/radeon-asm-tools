using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using static VSRAD.BuildTools.IPCBuildResult;

namespace VSRAD.Package.BuildTools.Errors
{
    public static class Parser
    {
        public static ICollection<Message> ParseStderr(IEnumerable<string> outputs)
        {
            var messages = new List<Message>();
            foreach (var output in outputs)
            {
                using (var reader = new StringReader(output))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var message = 
                            ParseAsmMessage(line) ??
                            ParseScriptMessage(line) ??
                            ParseClangMessage(line);
                        if (message != null)
                            messages.Add(message);
                        else if (messages.Count > 0)
                            messages.Last().Text += Environment.NewLine + line;
                    }
                }
            }
            return messages;
        }

        private static readonly Regex ClangErrorRegex = new Regex(
            @"(?<file>.+):(?<line>\d+):(?<col>\d+):\s*(?<kind>error|warning|note|fatal error):\s(?<text>.+)", RegexOptions.Compiled);

        private static Message ParseClangMessage(string header)
        {
            // Fast check to see if the string may contain a clang error, necessary because the full error regex takes too much time to match long lines
            if (!(header.Contains("error") || header.Contains("warning") || header.Contains("note")))
                return null;

            var match = ClangErrorRegex.Match(header);
            if (!match.Success) return null;
            return new Message
            {
                Kind = ParseMessageKind(match.Groups["kind"].Value),
                Text = match.Groups["text"].Value,
                SourceFile = match.Groups["file"].Value,
                Line = int.Parse(match.Groups["line"].Value),
                Column = int.Parse(match.Groups["col"].Value)
            };
        }

        private static readonly Regex AsmErrorRegex = new Regex(
            @"\*(?<kind>[EW]),(?<code>[^:(]+)(?>\s\((?<file>.+):(?<line>\d+)\))?:\s(?<text>.+)", RegexOptions.Compiled);

        private static Message ParseAsmMessage(string header)
        {
            var match = AsmErrorRegex.Match(header);
            if (!match.Success) return null;

            var code = match.Groups["code"].Value;
            var textAndMaybeLocation = match.Groups["text"].Value;

            var kind = ParseMessageKind(match.Groups["kind"].Value);

            if (match.Success)
            {
                var source = match.Groups["file"].Success
                                ? match.Groups["file"].Value.Trim()
                                : "";
                var line = match.Groups["line"].Success
                                ? int.Parse(match.Groups["line"].Value)
                                : 0;
                return new Message {
                    Kind = kind,
                    SourceFile = source,
                    Line = line,
                    Text = code + ": " + match.Groups["text"].Value
                };
            }
            else
            {
                return new Message { Kind = kind, Text = code + ": " + textAndMaybeLocation };
            }
        }

        private static readonly Regex ScriptErrorRegex = new Regex(
            @"(?<kind>ERROR|WARNING):\s*(?<text>.+)", RegexOptions.Compiled);

        private static Message ParseScriptMessage(string header)
        {
            var match = ScriptErrorRegex.Match(header);
            if (!match.Success) return null;

            return new Message
            {
                Kind = ParseMessageKind(match.Groups["kind"].Value),
                Text = match.Groups["text"].Value
            };
        }

        private static MessageKind ParseMessageKind(string kind)
        {
            switch (kind.ToUpperInvariant())
            {
                case "E": case "ERROR": case "FATAL ERROR": return MessageKind.Error;
                case "W": case "WARNING": return MessageKind.Warning;
                case "NOTE": return MessageKind.Note;
                default: throw new ArgumentException(kind, nameof(kind));
            }
        }
    }
}
