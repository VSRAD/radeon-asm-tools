﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public class GroupIndexChangedEventArgs : EventArgs
    {
        public string Coordinates { get; }
        public uint GroupIndex { get; }
        public uint GroupSize { get; }
        public bool IsGroupIndexValid { get; }

        public GroupIndexChangedEventArgs(string coordinates, uint groupIndex, uint groupSize, bool isGroupIndexValid)
        {
            Coordinates = coordinates;
            GroupIndex = groupIndex;
            GroupSize = groupSize;
            IsGroupIndexValid = isGroupIndexValid;
        }
    }

    public sealed class GroupIndexSelector : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public event EventHandler<GroupIndexChangedEventArgs> IndexChanged;

        private uint _x;
        public uint X { get => _x; set => SetField(ref _x, value); }

        private uint _y;
        public uint Y { get => _y; set => SetField(ref _y, value); }

        private uint _z;
        public uint Z { get => _z; set => SetField(ref _z, value); }

        private uint _dimX = 1;
        public uint DimX { get => _dimX; set { SetField(ref _dimX, value); RaisePropertyChanged(nameof(MaximumX)); } }

        private uint _dimY = 1;
        public uint DimY { get => _dimY; set { SetField(ref _dimY, value); RaisePropertyChanged(nameof(MaximumY)); } }

        private uint _dimZ = 1;
        public uint DimZ { get => _dimZ; set { SetField(ref _dimZ, value); RaisePropertyChanged(nameof(MaximumZ)); } }

        // OneWay bindings in XAML do not work on these properties for some reason, hence the empty setters
        public uint MaximumX { get => _projectOptions.VisualizerOptions.NDRange3D ? DimX - 1 : uint.MaxValue; set { } }
        public uint MaximumY { get => DimY - 1; set { } }
        public uint MaximumZ { get => DimZ - 1; set { } }

        private string _error;
        public bool HasErrors => _error != null;

        private bool _updateOptions = true;

        private uint _numThreadsInProgram;

        private readonly Options.ProjectOptions _projectOptions;

        public GroupIndexSelector(Options.ProjectOptions options)
        {
            _projectOptions = options;
            _projectOptions.VisualizerOptions.PropertyChanged += OptionsChanged;
            _projectOptions.DebuggerOptions.PropertyChanged += OptionsChanged;
        }

        private void OptionsChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.VisualizerOptions.NDRange3D):
                    RaisePropertyChanged(nameof(MaximumX));
                    if (_updateOptions) Update();
                    break;
                case nameof(Options.DebuggerOptions.WaveSize):
                case nameof(Options.DebuggerOptions.GroupSize):
                case nameof(Options.DebuggerOptions.NGroups):
                    if (_updateOptions) Update();
                    break;
            }
        }

        public void UpdateOnBreak(uint numThreadsInProgram, BreakStateDispatchParameters dispatchParams)
        {
            _numThreadsInProgram = numThreadsInProgram;
            SetOptionsFromDispatchParams(dispatchParams);
        }

        private void SetOptionsFromDispatchParams(BreakStateDispatchParameters dispatchParams)
        {
            _updateOptions = false;

            _projectOptions.VisualizerOptions.NDRange3D = dispatchParams.NDRange3D;
            _projectOptions.DebuggerOptions.WaveSize = dispatchParams.WaveSize;

            DimX = dispatchParams.DimX;
            DimY = dispatchParams.DimY;
            DimZ = dispatchParams.DimZ;

            _projectOptions.DebuggerOptions.NGroups = dispatchParams.NDRange3D
                ? dispatchParams.DimX * dispatchParams.DimY * dispatchParams.DimZ
                : dispatchParams.DimX;
            _projectOptions.DebuggerOptions.GroupSize = dispatchParams.NDRange3D
                ? dispatchParams.GroupSizeX * dispatchParams.GroupSizeY * dispatchParams.GroupSizeZ
                : dispatchParams.GroupSizeX;

            _updateOptions = true;
            Update();
        }

        public void GoToGroup(uint groupIdx)
        {
            if (_projectOptions.VisualizerOptions.NDRange3D)
            {
                _updateOptions = false;
                X = groupIdx % DimX;
                groupIdx /= DimX;
                Y = groupIdx % DimY;
                groupIdx /= DimY;
                Z = groupIdx;
                _updateOptions = true;
                Update();
            }
            else
            {
                X = groupIdx;
            }
        }

        public void Update()
        {
            var index = _projectOptions.VisualizerOptions.NDRange3D ? (X + Y * DimX + Z * DimX * DimY) : X;
            var coordinates = _projectOptions.VisualizerOptions.NDRange3D ? $"({X}; {Y}; {Z})" : $"({X})";

            var groupSize = _projectOptions.DebuggerOptions.GroupSize;
            var waveSize = _projectOptions.DebuggerOptions.WaveSize;
            var nGroups = _projectOptions.DebuggerOptions.NGroups;

            GroupIndexChangedEventArgs args;
            if (_numThreadsInProgram != 0)
            {
                var dataGroupCount = _numThreadsInProgram / MathUtils.RoundUpToMultiple(groupSize, waveSize);
                var groupIndexValid = index < dataGroupCount;
                _error = groupIndexValid ? null : $"Invalid group index: {index} >= {dataGroupCount}";
                args = new GroupIndexChangedEventArgs(coordinates, index, groupSize, groupIndexValid);
            }
            else
            {
                _error = null;
                args = new GroupIndexChangedEventArgs(coordinates, index, groupSize, isGroupIndexValid: true);
            }
            IndexChanged?.Invoke(this, args);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(X)));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Y)));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Z)));
        }

        public IEnumerable GetErrors(string propertyName)
        {
            if ((propertyName != "X" && propertyName != "Y" && propertyName != "Z")
                || _error == null)
                return Enumerable.Empty<object>();
            return new[] { _error };
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;

            if (_updateOptions) Update();
            RaisePropertyChanged(propertyName);

            return true;
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
