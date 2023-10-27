﻿using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Tagging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Deborgar;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.EditorExtensions;
using VSRAD.Package.Server;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.PackageTests.ProjectSystem
{
    [Collection(MockedVS.Collection)]
    public class DebuggerIntegrationTests
    {
        [Fact]
        public async Task SuccessfulRunTestAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var packageErrors = TestHelper.CapturePackageMessageBoxErrors();

            /* Create a test project */

            var projectMock = new Mock<IProject>();
            var options = new ProjectOptions();
            options.SetProfiles(new Dictionary<string, ProfileOptions> { { "Default", new ProfileOptions() } }, activeProfile: "Default");
            projectMock.Setup((p) => p.Options).Returns(options);
            var breakLineTagger = new Mock<BreakLineGlyphTaggerProvider>();
            projectMock.Setup((p) => p.GetExportByMetadataAndType(It.IsAny<Predicate<IAppliesToMetadataView>>(), It.IsAny<Predicate<IViewTaggerProvider>>()))
                .Returns(breakLineTagger.Object);
            var project = projectMock.Object;
            project.Options.Profile.MenuCommands.DebugAction = "Debug";
            project.Options.Profile.General.LocalWorkDir = "local/dir";
            project.Options.Profile.General.RemoteWorkDir = "/periphery/votw";
            project.Options.Profile.Actions.Add(new ActionProfileOptions { Name = "Debug" });
            project.Options.DebuggerOptions.Watches.Add(new Watch("a", new VariableType(VariableCategory.Hex, 32)));
            project.Options.DebuggerOptions.Watches.Add(new Watch("c", new VariableType(VariableCategory.Hex, 32)));
            project.Options.DebuggerOptions.Watches.Add(new Watch("tide", new VariableType(VariableCategory.Hex, 32)));

            var readDebugDataStep = new ReadDebugDataStep { BinaryOutput = true, OutputOffset = 0 };
            readDebugDataStep.OutputFile.CheckTimestamp = true;
            readDebugDataStep.OutputFile.Path = "output-path";
            readDebugDataStep.WatchesFile.CheckTimestamp = false;
            readDebugDataStep.WatchesFile.Path = "watches-path";
            readDebugDataStep.DispatchParamsFile.CheckTimestamp = false;
            readDebugDataStep.DispatchParamsFile.Path = "dispatch-params-path";

            project.Options.Profile.Actions[0].Steps.Add(new ExecuteStep
            { Executable = "ohmu", Arguments = "-break-line $(RadBreakLines) -source $(RadActiveSourceFile) -source-line $(RadActiveSourceFileLine) -watch $(RadWatches)" });
            project.Options.Profile.Actions[0].Steps.Add(readDebugDataStep);

            var codeEditor = new Mock<IActiveCodeEditor>();
            codeEditor.Setup(e => e.GetAbsoluteSourcePath()).Returns(@"C:\MEHVE\JATO.s");
            codeEditor.Setup(e => e.GetCurrentLine()).Returns(13);
            var breakpointTracker = new Mock<IBreakpointTracker>();
            breakpointTracker.Setup(t => t.GoToBreakTarget(@"C:\MEHVE\JATO.s", BreakTargetSelector.NextBreakpoint)).Returns(new[] { 666u });

            var serviceProvider = new Mock<SVsServiceProvider>();
            serviceProvider.Setup(p => p.GetService(typeof(SVsStatusbar))).Returns(new Mock<IVsStatusbar>().Object);

            var channel = new MockCommunicationChannel();
            var sourceManager = new Mock<IProjectSourceManager>();
            var actionLauncher = new ActionLauncher(project, new Mock<IActionLogger>().Object, channel.Object, sourceManager.Object,
                codeEditor.Object, breakpointTracker.Object, serviceProvider.Object);
            var debuggerIntegration = new DebuggerIntegration(project, actionLauncher, codeEditor.Object, breakpointTracker.Object, sourceManager.Object);

            /* Set up server responses */

            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.FileNotFound }, (FetchMetadata timestampFetch) =>
                Assert.Equal(new[] { "/periphery/votw", "output-path" }, timestampFetch.FilePath));
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0 }, (Execute execute) =>
            {
                Assert.Equal("ohmu", execute.Executable);
                Assert.Equal(@"-break-line 666:resume -source JATO.s -source-line 13 -watch a;c;tide", execute.Arguments);
            });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("ValidWatches.txt") }, (FetchResultRange watchesFetch) =>
                Assert.Equal(new[] { "/periphery/votw", "watches-path" }, watchesFetch.FilePath));
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = TestHelper.ReadFixtureBytes("DispatchParams.txt") }, (FetchResultRange dispatchParamsFetch) =>
                Assert.Equal(new[] { "/periphery/votw", "dispatch-params-path" }, dispatchParamsFetch.FilePath));
            channel.ThenRespond(new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.Now, ByteCount = TestHelper.GetFixtureSize("DebugBuffer.bin") });

            /* Start debugging */

            var tcs = new TaskCompletionSource<ExecutionCompletedEventArgs>();
            BreakState breakState = null;

            debuggerIntegration.ExecutionCompleted += (s, e) => tcs.SetResult(e);
            debuggerIntegration.BreakEntered += (s, e) => breakState = e;

            var engine = debuggerIntegration.RegisterEngine();
            engine.Execute(false);

            var execCompletedEvent = await tcs.Task;

            Assert.Empty(packageErrors);
            Assert.NotNull(execCompletedEvent);
            Assert.Single(execCompletedEvent.BreakInstances);
            Assert.Equal(@"C:\MEHVE\JATO.s", execCompletedEvent.BreakInstances[0].CallStack[0].SourcePath);
            Assert.Equal(666u, execCompletedEvent.BreakInstances[0].CallStack[0].SourceLine);

            sourceManager.Verify(s => s.SaveProjectState(), Times.Once);

            Assert.NotNull(breakState);
            Assert.Equal(16384u, breakState.DispatchParameters.GridSizeX);
            Assert.Equal(512u, breakState.DispatchParameters.GroupSizeX);
            Assert.Equal(64u, breakState.DispatchParameters.WaveSize);
            Assert.Equal(new[] { "a", "c", "tide", "tid", "lst", "lst[1]" }, breakState.Data.Watches.Keys);

            breakLineTagger.Verify(t => t.OnExecutionCompleted(sourceManager.Object, execCompletedEvent));
        }
    }
}
