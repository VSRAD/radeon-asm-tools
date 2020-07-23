﻿using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Package.BuildTools;
using static VSRAD.BuildTools.IPCBuildResult;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    public interface IErrorListManager
    {
        Task AddToErrorListAsync(string contents);
    }

    [Export(typeof(IErrorListManager))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ErrorListManager : IErrorListManager
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly ErrorListProvider _errorListProvider;
        private readonly IBuildErrorProcessor _buildErrorProcessor;
        private readonly UnconfiguredProject _unconfiguredProject;
        private readonly IProject _project;

        [ImportingConstructor]
        public ErrorListManager(
            SVsServiceProvider serviceProvider,
            IBuildErrorProcessor buildErrorProcessor,
            IProject project,
            UnconfiguredProject unconfiguredProject)
        {
            _serviceProvider = serviceProvider;
            _errorListProvider = new ErrorListProvider(_serviceProvider);
            _buildErrorProcessor = buildErrorProcessor;
            _project = project;
            _unconfiguredProject = unconfiguredProject;
        }

        public async Task AddToErrorListAsync(string stderr)
        {
            if (stderr == null) return;

            _errorListProvider.Tasks.Clear();

            var errors = new List<ErrorTask>();
            var messages = await _buildErrorProcessor.ExtractMessagesAsync(stderr, null);
            foreach (var message in messages)
            {
                var task = new ErrorTask
                {
                    Text = message.Text,
                    Document = Path.Combine(_project.RootPath, message.SourceFile),
                    Line = message.Line - 1,
                    Column = message.Column,
                    ErrorCategory = ParseKind(message.Kind),
                    Category = TaskCategory.BuildCompile,
                };
                task.Navigate += (sender, e) =>
                {
                    task.Line++; // just because vs error list is dumb. inside it dec 1 and jumps to this line
                    _errorListProvider.Navigate(task, Guid.Parse(/*EnvDTE.Constants.vsViewKindCode*/"{7651A701-06E5-11D1-8EBD-00A0C90F26EA}"));
                    task.Line--;
                };
                _errorListProvider.Tasks.Add(task);
                errors.Add(task);
            }

            ErrorTagger?.ErrorListUpdated(errors);
        }

        private bool _errorTaggerInitialized;
        private dynamic _errorTagger;
        private dynamic ErrorTagger
        {
            get
            {
                if (!_errorTaggerInitialized)
                {
                    var taggers = _unconfiguredProject.Services.ExportProvider.GetExportedValues<IViewTaggerProvider>();
                    _errorTagger = taggers.FirstOrDefault(t => t.GetType().FullName == "VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter.ErrorHighlighterTaggerProvider");
                    _errorTaggerInitialized = true;
                }
                return _errorTagger;
            }
        }

        private static TaskErrorCategory ParseKind(MessageKind kind)
        {
            switch (kind)
            {
                case MessageKind.Error: return TaskErrorCategory.Error;
                case MessageKind.Warning: return TaskErrorCategory.Warning;
                case MessageKind.Note: return TaskErrorCategory.Message;
                default: return TaskErrorCategory.Message;
            }
        }
    }
}
