﻿using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.BuildTools;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;
using static VSRAD.BuildTools.IPCBuildResult;

namespace VSRAD.Package.BuildTools
{
    [Flags]
    public enum BuildSteps
    {
        Skip = 1,
        Preprocessor = 2,
        Disassembler = 4,
        FinalStep = 8
    }

    [Export]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BuildToolsServer
    {
        public const string ErrorPreprocessorFileNotCreated = "Preprocessor output file is missing.";
        public const string ErrorPreprocessorFileUnchanged = "Preprocessor output file is unchanged after running the command.";

        public string PipeName { get; }

        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;
        private readonly IOutputWindowManager _outputWindow;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly IBuildErrorProcessor _errorProcessor;
        private readonly CancellationTokenSource _serverLoopCts = new CancellationTokenSource();

        private bool _serverStarted;
        private BuildSteps? _buildStepsOverride;

        [ImportingConstructor]
        public BuildToolsServer(
            IProject project,
            ICommunicationChannel channel,
            IOutputWindowManager outputWindow,
            IBuildErrorProcessor errorProcessor,
            IFileSynchronizationManager deployManager,
            UnconfiguredProject unconfiguredProject)
        {
            _project = project;
            _channel = channel;
            _outputWindow = outputWindow;
            _errorProcessor = errorProcessor;
            _deployManager = deployManager;

            // Build integration is not implemented for VisualC projects
            if (unconfiguredProject.Capabilities?.Contains("VisualC") != true)
            {
                unconfiguredProject.Services.ActiveConfiguredProjectProvider.Changed += SetupPipe;
                project.Unloaded += _serverLoopCts.Cancel;
            }

            PipeName = "vsrad-" + Guid.NewGuid();
        }

        public void OverrideStepsForNextBuild(BuildSteps steps)
        {
            _buildStepsOverride = steps;
        }

        private void SetupPipe(object sender, ActiveConfigurationChangedEventArgs e)
        {
            if (e.NowActive == null)
                return;

            var projectProperties = e.NowActive.Services.ProjectPropertiesProvider.GetCommonProperties();
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
            {
                await projectProperties.SetPropertyValueAsync("RadBuildToolsPipe", PipeName);
                if (!_serverStarted)
                    await RunServerLoopAsync();
            });
        }

        public async Task RunServerLoopAsync()
        {
            _serverStarted = true;
            while (!_serverLoopCts.Token.IsCancellationRequested)
                try
                {
                    using (var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        await server.WaitForConnectionAsync(_serverLoopCts.Token).ConfigureAwait(false);

                        byte[] message;
                        try
                        {
                            var buildResult = await BuildAsync().ConfigureAwait(false);
                            if (buildResult.TryGetResult(out var result, out var error))
                            {
                                result.ServerMessage = $"Using profile {_project.Options.ActiveProfile}. Note that opening the same project in multiple Visual Studio instances may lead to incorrect build results.";
                                message = result.ToArray();
                            }
                            else
                            {
                                message = new IPCBuildResult { ServerError = error.Message }.ToArray();
                            }
                        }
                        catch (Exception e)
                        {
                            message = new IPCBuildResult { ServerError = e.Message }.ToArray();
                        }

                        await server.WriteAsync(message, 0, message.Length);
                    }
                }
                catch (Exception e)
                {
                    Package.Errors.ShowException(e);
                }
        }

        private BuildSteps GetBuildSteps(Options.BuildProfileOptions buildOptions)
        {
            if (_buildStepsOverride is BuildSteps overriddenSteps)
            {
                _buildStepsOverride = null;
                return overriddenSteps;
            }
            var steps = BuildSteps.Skip;
            if (buildOptions.RunPreprocessor)
                steps |= BuildSteps.Preprocessor;
            if (buildOptions.RunDisassembler)
                steps |= BuildSteps.Disassembler;
            if (!string.IsNullOrEmpty(buildOptions.Executable))
                steps |= BuildSteps.FinalStep;
            return steps;
        }

        private async Task<Result<IPCBuildResult>> BuildAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var buildOptions = await _project.Options.Profile.Build.EvaluateAsync(evaluator);
            var preprocessorOptions = await Options.PreprocessorProfileOptions.EvaluateAsync(evaluator);
            var disassemblerOptions = await _project.Options.Profile.Disassembler.EvaluateAsync(evaluator);
            var buildSteps = GetBuildSteps(buildOptions);

            if (buildSteps == BuildSteps.Skip)
                return new IPCBuildResult { Skipped = true };

            await _deployManager.SynchronizeRemoteAsync().ConfigureAwait(false);
            var executor = new RemoteCommandExecutor("Build", _channel, _outputWindow);

            string preprocessedSource = null;
            if ((buildSteps & BuildSteps.Preprocessor) == BuildSteps.Preprocessor)
            {
                var ppCommand = new Execute
                {
                    Executable = preprocessorOptions.Executable,
                    Arguments = preprocessorOptions.Arguments,
                    WorkingDirectory = preprocessorOptions.WorkingDirectory
                };
                var ppResult = await executor.ExecuteWithResultAsync(ppCommand, preprocessorOptions.RemoteOutputFile, checkExitCode: false);
                if (!ppResult.TryGetResult(out var ppResponse, out var error))
                    return new Error("Preprocessor: " + error.Message);
                var (ppStatus, ppData) = ppResponse;
                if (ppData != null)
                    preprocessedSource = Encoding.UTF8.GetString(ppData);
                var ppMessages = await _errorProcessor.ExtractMessagesAsync(ppStatus.Stderr, preprocessedSource);
                if (ppStatus.ExitCode != 0 || ppMessages.Any())
                    return new IPCBuildResult { ExitCode = ppStatus.ExitCode, ErrorMessages = ppMessages.ToArray() };
                if (!string.IsNullOrEmpty(preprocessorOptions.LocalOutputCopyPath))
                    File.WriteAllText(preprocessorOptions.LocalOutputCopyPath, preprocessedSource);
            }
            if ((buildSteps & BuildSteps.Disassembler) == BuildSteps.Disassembler)
            {
                var disasmCommand = new Execute
                {
                    Executable = disassemblerOptions.Executable,
                    Arguments = disassemblerOptions.Arguments,
                    WorkingDirectory = disassemblerOptions.WorkingDirectory
                };
                var disasmResult = await executor.ExecuteWithResultAsync(disasmCommand, disassemblerOptions.RemoteOutputFile, checkExitCode: false);
                if (!disasmResult.TryGetResult(out var disasmResponse, out var error))
                    return new Error("Disassembler: " + error.Message);
                var (disasmStatus, disasmData) = disasmResponse;
                var disasmMessages = await _errorProcessor.ExtractMessagesAsync(disasmStatus.Stderr, preprocessedSource);
                if (disasmResult == null || disasmMessages.Any())
                    return new IPCBuildResult { ExitCode = disasmStatus.ExitCode, ErrorMessages = disasmMessages.ToArray() };
                if (!string.IsNullOrEmpty(disassemblerOptions.LocalOutputCopyPath) && disasmData != null)
                    File.WriteAllBytes(disassemblerOptions.LocalOutputCopyPath, disasmData);
            }
            if ((buildSteps & BuildSteps.FinalStep) == BuildSteps.FinalStep)
            {
                var finalStepCommand = new Execute
                {
                    Executable = buildOptions.Executable,
                    Arguments = buildOptions.Arguments,
                    WorkingDirectory = buildOptions.WorkingDirectory
                };
                var finalStepResult = await RunStepAsync(executor, finalStepCommand, preprocessedSource);
                if (!finalStepResult.TryGetResult(out var finalStepData, out var error))
                    return new Error("Final step: " + error.Message);
                var (finalStepExitCode, finalStepMessages) = finalStepData;
                return new IPCBuildResult { ExitCode = finalStepExitCode, ErrorMessages = finalStepMessages.ToArray() };
            }
            return new IPCBuildResult();
        }

        private async Task<Result<(int, IEnumerable<Message>)>> RunStepAsync(RemoteCommandExecutor executor, Execute command, string preprocessed)
        {
            var response = await executor.ExecuteAsync(command, checkExitCode: false);
            if (!response.TryGetResult(out var result, out var error))
                return error;

            var messages = await _errorProcessor.ExtractMessagesAsync(result.Stderr, preprocessed);
            return (result.ExitCode, messages);
        }
    }
}
