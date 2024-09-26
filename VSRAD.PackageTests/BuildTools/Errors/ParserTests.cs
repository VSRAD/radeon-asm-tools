using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static VSRAD.BuildTools.IPCBuildResult;
using static VSRAD.Package.BuildTools.Errors.Parser;

namespace VSRAD.PackageTests.BuildTools.Errors
{
    public class ParserTests
    {
        public const string ClangStderr = @"
input.s:267:27: error: expected absolute expression
      s_sub_u32         s[loop_xss], s[loop_x], 1
                          ^
<stdin>:392:25: warning: not a valid operand.
      s_add_u32         s[loop_xss], s[loop_x], 1
                        ^
Relative\path\host.c:4:2: warning: implicitly declaring library function 'printf' with type 'int (const char *, ...)'
        printf(""h"");
        ^
C:\Absolute\Path\host.c:4:2: note: include the header<stdio.h> or explicitly provide a declaration for 'printf'
input.s:16:10: fatal error: 'abcde.s' file not found
#include ""abcde.s""
         ^~~~~~~~~";

        public static readonly Message[] ClangExpectedMessages = new Message[]
        {
            new Message { Kind = MessageKind.Error, Line = 267, Column = 27, SourceFile = "input.s", Text =
@"expected absolute expression
      s_sub_u32         s[loop_xss], s[loop_x], 1
                          ^" },
            new Message { Kind = MessageKind.Warning, Line = 392, Column = 25, SourceFile = "<stdin>", Text =
@"not a valid operand.
      s_add_u32         s[loop_xss], s[loop_x], 1
                        ^" },
            new Message { Kind = MessageKind.Warning, Line = 4, Column = 2, SourceFile = @"Relative\path\host.c", Text =
@"implicitly declaring library function 'printf' with type 'int (const char *, ...)'
        printf(""h"");
        ^"},
            new Message { Kind = MessageKind.Note, Line = 4, Column = 2, SourceFile = @"C:\Absolute\Path\host.c", Text =
                "include the header<stdio.h> or explicitly provide a declaration for 'printf'" },
            new Message { Kind = MessageKind.Error, Line = 16, Column = 10, SourceFile = "input.s", Text =
@"'abcde.s' file not found
#include ""abcde.s""
         ^~~~~~~~~" }
        };

        [Fact]
        public void ClangErrorTest()
        {
            var messages = ParseStderr(new string[] { ClangStderr }).ToArray();
            Assert.Equal(ClangExpectedMessages, messages);
        }

        public const string AsmStderr =
@"*W,undefined: 1 undefined references found
*E,syntax error (<stdin>:12): at symbol 'printf'
    parse error: syntax error, unexpected T_PAAMAYIM_NEKUDOTAYIM
    did you really mean to use the scope resolution op here?
*E,fatal (C:\Absolute\Path\source.c:35): Uncaught error: Undefined variable: user
*W,undefined: undefined reference to 'printf'";

        public static readonly Message[] AsmExpectedMessages = new Message[]
        {
            new Message { Kind = MessageKind.Warning, Line = 0, SourceFile = "", Text = "undefined: 1 undefined references found" },
            new Message { Kind = MessageKind.Error, Line = 12, SourceFile = "<stdin>", Text =
@"syntax error: at symbol 'printf'
    parse error: syntax error, unexpected T_PAAMAYIM_NEKUDOTAYIM
    did you really mean to use the scope resolution op here?" },
            new Message { Kind = MessageKind.Error, Line = 35, SourceFile = @"C:\Absolute\Path\source.c", Text = "fatal: Uncaught error: Undefined variable: user" },
            new Message { Kind = MessageKind.Warning, Line = 0, SourceFile = "", Text = "undefined: undefined reference to 'printf'" }
        };

        [Fact]
        public void AsmErrorTest()
        {
            var messages = ParseStderr(new string[] { AsmStderr }).ToArray();
            Assert.Equal(AsmExpectedMessages, messages);
        }

        public const string ScriptStderr =
@"*E,fatal (<stdin>:3): undefined reference to 'printf'
ERROR: check if app exists and can be executed 'C:\NEVER\GONNA\GIVE\YOU\UP.exe'
WARNING: you are incredibly beautiful!
*E,fatal (auth.c:35): Uncaught error: Undefined variable: user";

        public static readonly Message[] ScriptErrorExpectedMessages = new Message[]
        {
            new Message { Kind = MessageKind.Error, Line = 3, SourceFile = "<stdin>", Text = "fatal: undefined reference to 'printf'" },
            new Message { Kind = MessageKind.Error, Line = 0, SourceFile = "", Text = @"check if app exists and can be executed 'C:\NEVER\GONNA\GIVE\YOU\UP.exe'" },
            new Message { Kind = MessageKind.Warning, Line = 0, SourceFile = "", Text = @"you are incredibly beautiful!" },
            new Message { Kind = MessageKind.Error, Line = 35, SourceFile = "auth.c", Text = "fatal: Uncaught error: Undefined variable: user" },
        };

        [Fact]
        public void ScriptErrorTest()
        {
            var messages = ParseStderr(new string[] { ScriptStderr }).ToArray();
            Assert.Equal(ScriptErrorExpectedMessages, messages);
        }

        [Fact]
        public void MixedErrorFormatsTest()
        {
            var expectedMessages = ClangExpectedMessages.Concat(ScriptErrorExpectedMessages).Concat(AsmExpectedMessages).ToArray();

            var separateOutputs = new[] { ClangStderr, ScriptStderr, AsmStderr };
            var separateOutputsMessages = ParseStderr(separateOutputs).ToArray();
            Assert.Equal(expectedMessages, separateOutputsMessages);

            var combinedOutput = string.Join("\r\n", separateOutputs.Select(o => o.Trim()));
            var combinedOutputMessages = ParseStderr(new[] { combinedOutput }).ToArray();
            Assert.Equal(expectedMessages, combinedOutputMessages);
        }

        [Fact]
        public async Task LongErrorLinesTestAsync()
        {
            var longLine = string.Concat(Enumerable.Repeat("[#(a: b, c=d, e-f, g>h)],", 1000));
            var stderr = Enumerable.Repeat(longLine, 100);

            var task = Task.Run(() => ParseStderr(stderr).ToArray());
            var result = await Task.WhenAny(task, Task.Delay(1000));
            if (result == task)
                Assert.Empty(await task);
            else
                throw new TimeoutException("ParseStderr should not take longer than 1 second");
        }
    }
}
