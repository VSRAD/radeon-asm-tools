﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    // Note: when adding a new step, don't forget to add the new type to ActionStepJsonConverter.

    public interface IActionStep : INotifyPropertyChanged
    {
        Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile);
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum StepEnvironment
    {
        Remote, Local
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum FileCopyDirection
    {
        RemoteToLocal, LocalToRemote
    }

    public sealed class CopyFileStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private FileCopyDirection _direction;
        public FileCopyDirection Direction { get => _direction; set => SetField(ref _direction, value); }

        private string _localPath = "";
        public string LocalPath { get => _localPath; set => SetField(ref _localPath, value); }

        private string _remotePath = "";
        public string RemotePath { get => _remotePath; set => SetField(ref _remotePath, value); }

        private bool _checkTimestamp;
        public bool CheckTimestamp { get => _checkTimestamp; set => SetField(ref _checkTimestamp, value); }

        public override string ToString() => "Copy File";

        public override bool Equals(object obj) =>
            obj is CopyFileStep step &&
            LocalPath == step.LocalPath &&
            RemotePath == step.RemotePath &&
            CheckTimestamp == step.CheckTimestamp;

        public override int GetHashCode() =>
            (LocalPath, RemotePath, CheckTimestamp).GetHashCode();

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile) =>
            new CopyFileStep
            {
                Direction = Direction,
                LocalPath = await evaluator.EvaluateAsync(LocalPath),
                RemotePath = await evaluator.EvaluateAsync(RemotePath),
                CheckTimestamp = CheckTimestamp
            };
    }

    public sealed class ExecuteStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private StepEnvironment _environment;
        public StepEnvironment Environment { get => _environment; set => SetField(ref _environment, value); }

        private string _executable = "";
        public string Executable { get => _executable; set => SetField(ref _executable, value); }

        private string _arguments = "";
        public string Arguments { get => _arguments; set => SetField(ref _arguments, value); }

        private string _workingDirectory = "";
        public string WorkingDirectory { get => _workingDirectory; set => SetField(ref _workingDirectory, value); }

        private bool _runAsAdmin;
        public bool RunAsAdmin { get => _runAsAdmin; set => SetField(ref _runAsAdmin, value); }

        private bool _waitForCompletion = true;
        public bool WaitForCompletion { get => _waitForCompletion; set => SetField(ref _waitForCompletion, value); }

        private int _timeoutSecs = 0;
        public int TimeoutSecs { get => _timeoutSecs; set => SetField(ref _timeoutSecs, value); }

        public override string ToString() => "Execute";

        public override bool Equals(object obj) =>
            obj is ExecuteStep step &&
            Environment == step.Environment &&
            Executable == step.Executable &&
            Arguments == step.Arguments &&
            WorkingDirectory == step.WorkingDirectory &&
            RunAsAdmin == step.RunAsAdmin &&
            WaitForCompletion == step.WaitForCompletion &&
            TimeoutSecs == step.TimeoutSecs;

        public override int GetHashCode() =>
            (Environment, Executable, Arguments, WorkingDirectory, RunAsAdmin, WaitForCompletion, TimeoutSecs).GetHashCode();

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile) =>
            new ExecuteStep
            {
                Environment = Environment,
                Executable = await evaluator.EvaluateAsync(Executable),
                Arguments = await evaluator.EvaluateAsync(Arguments),
                WorkingDirectory = await evaluator.EvaluateAsync(WorkingDirectory),
                RunAsAdmin = RunAsAdmin,
                WaitForCompletion = WaitForCompletion,
                TimeoutSecs = TimeoutSecs
            };
    }

    public sealed class OpenInEditorStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private string _path = "";
        public string Path { get => _path; set => SetField(ref _path, value); }

        private string _lineMarker = "";
        public string LineMarker { get => _lineMarker; set => SetField(ref _lineMarker, value); }

        public override string ToString() => "Open in Editor";

        public override bool Equals(object obj) =>
            obj is OpenInEditorStep step && Path == step.Path && LineMarker == step.LineMarker;

        public override int GetHashCode() =>
            (Path, LineMarker).GetHashCode();

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile) =>
            new OpenInEditorStep
            {
                Path = await evaluator.EvaluateAsync(Path),
                LineMarker = LineMarker
            };
    }

    public sealed class RunActionStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private string _name = "";
        public string Name { get => _name; set { if (value != null) SetField(ref _name, value); } }

        [JsonIgnore]
        public List<IActionStep> EvaluatedSteps { get; }

        public RunActionStep(List<IActionStep> evaluatedSteps = null)
        {
            EvaluatedSteps = evaluatedSteps;
        }

        public override string ToString() => string.IsNullOrEmpty(Name) ? "Run Action" : "Run " + Name;

        public override bool Equals(object obj) => obj is RunActionStep step && Name == step.Name;

        public override int GetHashCode() => Name.GetHashCode();

        public Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile) =>
            EvaluateAsync(evaluator, profile, new Stack<string>());

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile, Stack<string> callers)
        {
            if (callers.Contains(Name))
                throw new Exception("Encountered a circular action: " + string.Join(" -> ", callers.Reverse()) + " -> " + Name);

            var action = profile.General.Actions.FirstOrDefault(a => a.Name == Name);
            if (action == null)
                throw new Exception("Action " + Name + " not found" + (callers.Count == 0 ? "" : ", required by " + string.Join(" -> ", callers.Reverse()) + " -> " + Name));

            callers.Push(Name);

            var evaluatedSteps = new List<IActionStep>();
            foreach (var step in action.Steps)
            {
                IActionStep evaluated;
                if (step is RunActionStep runAction)
                    evaluated = await runAction.EvaluateAsync(evaluator, profile, callers);
                else
                    evaluated = await step.EvaluateAsync(evaluator, profile);
                evaluatedSteps.Add(evaluated);
            }

            callers.Pop();

            return new RunActionStep(evaluatedSteps);
        }
    }

    public sealed class ActionStepJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            typeof(IActionStep).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var step = InstantiateStepFromTypeField((string)obj["Type"]);
            serializer.Populate(obj.CreateReader(), step);
            return step;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var serialized = JObject.FromObject(value);
            serialized["Type"] = GetStepType(value);
            serialized.WriteTo(writer);
        }

        private static IActionStep InstantiateStepFromTypeField(string type)
        {
            switch (type)
            {
                case "Execute": return new ExecuteStep();
                case "CopyFile": return new CopyFileStep();
                case "OpenInEditor": return new OpenInEditorStep();
                case "RunAction": return new RunActionStep();
            }
            throw new ArgumentException($"Unknown step type identifer {type}", nameof(type));
        }

        private static string GetStepType(object step)
        {
            switch (step)
            {
                case ExecuteStep _: return "Execute";
                case CopyFileStep _: return "CopyFile";
                case OpenInEditorStep _: return "OpenInEditor";
                case RunActionStep _: return "RunAction";
            }
            throw new ArgumentException($"Step type identifier is not defined for {step.GetType()}", nameof(step));
        }
    }
}