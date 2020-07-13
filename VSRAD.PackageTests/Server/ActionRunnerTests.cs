﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Options;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.Server
{
    public class ActionRunnerTests
    {
        [Fact]
        public async Task SucessfulRunTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, SourcePath = "tweened.tvpp", TargetPath = Path.GetTempFileName() }
            };
            var localTempFile = Path.GetRandomFileName();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));

            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) } }, (commands) =>
            {
                // init timestamp fetch
                var command = (FetchMetadata)commands[0];
                Assert.Equal(new[] { "/home/mizu/machete", "tweened.tvpp" }, command.FilePath);
            });
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "", Stderr = "" });
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(101), Data = Encoding.UTF8.GetBytes("file-contents") });
            var result = await runner.RunAsync("HTMT", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.True(result.Successful);
            Assert.True(result.StepResults[0].Successful);
            Assert.Equal("", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (exit code 0)\r\n", result.StepResults[0].Log);
            Assert.True(result.StepResults[1].Successful);
            Assert.Equal("", result.StepResults[1].Warning);
            Assert.Equal("", result.StepResults[1].Log);
            Assert.Equal("file-contents", File.ReadAllText(((CopyFileStep)steps[1]).TargetPath));
            File.Delete(((CopyFileStep)steps[1]).TargetPath);
        }

        #region CopyFileStep
        [Fact]
        public async Task CopyRLRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));
            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, SourcePath = "/home/mizu/machete/key3_49" },
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" } // should not be run
            };

            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.FileNotFound } }); // init timestamp fetch
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.FileNotFound });
            var result = await runner.RunAsync("HTMT", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("File is not found on the remote machine at /home/mizu/machete/key3_49", result.StepResults[0].Warning);

            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) } }); // init timestamp fetch
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) });
            result = await runner.RunAsync("HTMT", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("File is not changed on the remote machine at /home/mizu/machete/key3_49", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyRLMissingParentDirectoryTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));

            var parentDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Assert.False(Directory.Exists(parentDir));

            var file = Path.Combine(parentDir, "local-copy");
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "raw3", TargetPath = file } };

            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });
            var result = await runner.RunAsync("HTMT", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.True(result.Successful);
            Assert.Equal("file-contents", File.ReadAllText(file));
            File.Delete(file);
        }

        [Fact]
        public async Task CopyRLLocalErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));

            var file = Path.GetTempFileName();
            File.SetAttributes(file, FileAttributes.ReadOnly);
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, SourcePath = "raw3", TargetPath = file } };
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Data = Encoding.UTF8.GetBytes("file-contents") });

            var result = await runner.RunAsync("HTMT", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.Equal($"Access to path {file} on the local machine is denied", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyLRRemoteErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = Path.GetTempFileName(), TargetPath = "raw3" } };

            channel.ThenRespond(new PutFileResponse { Status = PutFileStatus.PermissionDenied });
            var result = await runner.RunAsync("HTMT", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.Equal("Access to path raw3 on the remote machine is denied", result.StepResults[0].Warning);

            channel.ThenRespond(new PutFileResponse { Status = PutFileStatus.OtherIOError });
            result = await runner.RunAsync("HTMT", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.Equal("File raw3 could not be created on the remote machine", result.StepResults[0].Warning);
        }

        [Fact]
        public async Task CopyLRLocalErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));

            var localPath = @"C:\Non\Existent\Path\To\Users\mizu\raw3";
            Assert.False(File.Exists(localPath));
            var steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = localPath, TargetPath = "" } };

            var result = await runner.RunAsync("HTMT", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.Equal(@"File C:\Non\Existent\Path\To\Users\mizu\raw3 is not found on the local machine", result.StepResults[0].Warning);

            var lockedPath = Path.GetTempFileName();
            var acl = File.GetAccessControl(lockedPath);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.Read, AccessControlType.Deny));
            File.SetAccessControl(lockedPath, acl);

            steps = new List<IActionStep> { new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, SourcePath = lockedPath, TargetPath = "" } };
            result = await runner.RunAsync("HTMT", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.Equal($"Access to path {lockedPath} on the local machine is denied", result.StepResults[0].Warning);
            File.Delete(lockedPath);
        }
        #endregion

        [Fact]
        public async Task ExecuteStepErrorTestAsync()
        {
            var channel = new MockCommunicationChannel();
            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "dvd-prepare" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = false, TargetPath = "/home/parker/audio/unchecked", SourcePath = "" }, // should not be run
            };
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/parker/audio"));

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.CouldNotLaunch, Stdout = "", Stderr = "" });
            var result = await runner.RunAsync("UFOW", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("dvd-prepare process could not be started on the remote machine. Make sure the path to the executable is specified correctly.", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (could not launch)\r\n", result.StepResults[0].Log);

            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.TimedOut, Stdout = "...\n", Stderr = "Could not prepare master DVD, deadline exceeded.\n\n" });
            result = await runner.RunAsync("UFOW", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.False(result.Successful);
            Assert.False(result.StepResults[0].Successful);
            Assert.Equal("Execution timeout is exceeded. dvd-prepare process on the remote machine is terminated.", result.StepResults[0].Warning);
            Assert.Equal("Captured stdout (timed out):\r\n...\r\nCaptured stderr (timed out):\r\nCould not prepare master DVD, deadline exceeded.\r\n", result.StepResults[0].Log);

            /* Non-zero exit code results in a successful run with a warning */
            steps = new List<IActionStep> { new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "dvd-prepare" } };
            channel.ThenRespond(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 1, Stdout = "", Stderr = "Looks like you fell asleep ¯\\_(ツ)_/¯\n\n" });
            result = await runner.RunAsync("UFOW", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.True(result.Successful);
            Assert.True(result.StepResults[0].Successful);
            Assert.Equal("dvd-prepare process exited with a non-zero code (1). Check your application or debug script output in Output -> RAD Debug.", result.StepResults[0].Warning);
            Assert.Equal("Captured stderr (exit code 1):\r\nLooks like you fell asleep ¯\\_(ツ)_/¯\r\n", result.StepResults[0].Log);
        }

        [Fact]
        public async Task LocalExecuteTestAsync()
        {
            var file = Path.GetTempFileName();

            var steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Local, Executable = "python.exe", Arguments = $"-c \"print('success', file=open(r'{file}', 'w'))\"" }
            };
            var runner = new ActionRunner(channel: null, serviceProvider: null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: ""));
            var result = await runner.RunAsync("", steps, Enumerable.Empty<BuiltinActionFile>());
            Assert.True(result.Successful);
            Assert.Equal("", result.StepResults[0].Warning);
            Assert.Equal("No stdout/stderr captured (exit code 0)\r\n", result.StepResults[0].Log);

            var output = File.ReadAllText(file);
            File.Delete(file);
            Assert.Equal("success\r\n", output);
        }

        [Fact]
        public async Task RunActionStepTestAsync()
        {
            var channel = new MockCommunicationChannel();

            var level3Steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "cleanup", Arguments = "--skip" },
            };
            var level2Steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "autotween" },
                new RunActionStep(level3Steps) { Name = "level3" }
            };
            var level1Steps = new List<IActionStep>
            {
                new RunActionStep(level2Steps) { Name = "level2" },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, SourcePath = "/home/mizu/machete/tweened.tvpp", TargetPath = Path.GetTempFileName() }
            };
            // 1. Initial timestamp fetch
            channel.ThenRespond(new[] { new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(100) } });
            // 2. Level 2 Execute
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "level2", Stderr = "" }, (command) =>
            {
                Assert.Equal("autotween", command.Executable);
            });
            // 3. Level 3 Execute
            channel.ThenRespond<Execute, ExecutionCompleted>(new ExecutionCompleted { Status = ExecutionStatus.Completed, ExitCode = 0, Stdout = "level3", Stderr = "" }, (command) =>
            {
                Assert.Equal("cleanup", command.Executable);
            });
            // 4. Level 1 Copy File
            channel.ThenRespond(new ResultRangeFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromBinary(101), Data = Encoding.UTF8.GetBytes("file-contents") });
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/mizu/machete"));
            var result = await runner.RunAsync("HTMT", level1Steps, Enumerable.Empty<BuiltinActionFile>());

            Assert.True(result.Successful);
            Assert.Equal("level2", result.StepResults[0].SubAction.ActionName);
            Assert.Null(result.StepResults[0].SubAction.StepResults[0].SubAction);
            Assert.Equal("Captured stdout (exit code 0):\r\nlevel2\r\n", result.StepResults[0].SubAction.StepResults[0].Log);
            Assert.Equal("level3", result.StepResults[0].SubAction.StepResults[1].SubAction.ActionName);
            Assert.Equal("Captured stdout (exit code 0):\r\nlevel3\r\n", result.StepResults[0].SubAction.StepResults[1].SubAction.StepResults[0].Log);
            Assert.Null(result.StepResults[1].SubAction);
        }

        [Fact]
        public async Task VerifiesTimestampsTestAsync()
        {
            var channel = new MockCommunicationChannel();

            var steps = new List<IActionStep>
            {
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = true, SourcePath = "/home/parker/audio/checked", TargetPath = Path.GetTempFileName() },
                new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = false, SourcePath = "/home/parker/audio/unchecked", TargetPath = Path.GetTempFileName() },
            };
            var auxFiles = new List<BuiltinActionFile>
            {
                new BuiltinActionFile { Location = StepEnvironment.Remote, CheckTimestamp = true, Path = "/home/parker/audio/master" },
                new BuiltinActionFile { Location = StepEnvironment.Remote, CheckTimestamp = false, Path = "/home/parker/audio/copy" },
                new BuiltinActionFile { Location = StepEnvironment.Local, CheckTimestamp = true, Path = ((CopyFileStep)steps[0]).TargetPath },
                new BuiltinActionFile { Location = StepEnvironment.Local, CheckTimestamp = false, Path = "non-existent-local-path" }
            };
            var runner = new ActionRunner(channel.Object, null, new ActionEnvironment(localWorkDir: Path.GetTempPath(), remoteWorkDir: "/home/parker"));

            channel.ThenRespond(new IResponse[]
            {
                new MetadataFetched { Status = FetchStatus.Successful, Timestamp = DateTime.FromFileTime(100) },
                new MetadataFetched { Status = FetchStatus.FileNotFound }
            }, (commands) =>
            {
                Assert.Equal(2, commands.Count);
                Assert.Equal(new[] { "/home/parker", "/home/parker/audio/checked" }, ((FetchMetadata)commands[0]).FilePath);
                Assert.Equal(new[] { "/home/parker", "/home/parker/audio/master" }, ((FetchMetadata)commands[1]).FilePath);
            });
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(
                new ResultRangeFetched { Data = Encoding.UTF8.GetBytes("TestCopyStepChecked") },
                (command) => Assert.Equal(new[] { "/home/parker", "/home/parker/audio/checked" }, command.FilePath));
            channel.ThenRespond<FetchResultRange, ResultRangeFetched>(
                new ResultRangeFetched { Data = Encoding.UTF8.GetBytes("TestCopyStepUnchecked") },
                (command) => Assert.Equal(new[] { "/home/parker", "/home/parker/audio/unchecked" }, command.FilePath));
            await runner.RunAsync("UFOW", steps, auxFiles);
            Assert.True(channel.AllInteractionsHandled);

            Assert.Equal(DateTime.FromFileTime(100), runner.GetInitialFileTimestamp("/home/parker/audio/checked"));
            Assert.Equal(default, runner.GetInitialFileTimestamp("/home/parker/audio/master"));
            Assert.Equal(File.GetCreationTime(((CopyFileStep)steps[0]).TargetPath), runner.GetInitialFileTimestamp(((CopyFileStep)steps[0]).TargetPath));

            Assert.Equal("TestCopyStepChecked", File.ReadAllText(((CopyFileStep)steps[0]).TargetPath));
            File.Delete(((CopyFileStep)steps[0]).TargetPath);
            Assert.Equal("TestCopyStepUnchecked", File.ReadAllText(((CopyFileStep)steps[1]).TargetPath));
            File.Delete(((CopyFileStep)steps[1]).TargetPath);
        }
    }
}