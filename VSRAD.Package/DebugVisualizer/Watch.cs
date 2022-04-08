﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VSRAD.Package.DebugVisualizer
{
    public readonly struct Watch : System.IEquatable<Watch>
    {
        public string Name { get; }

        //[JsonConverter(typeof(StringEnumConverter))]
        public VariableInfo Info { get; }

        public bool IsAVGPR { get; }

        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrWhiteSpace(Name);

        [JsonConstructor]
        public Watch(string name, VariableInfo type, bool isAVGPR)
        {
            Name = name;
            Info = type;
            IsAVGPR = isAVGPR;
        }

        public bool Equals(Watch w) => Name == w.Name && Info == w.Info && IsAVGPR == w.IsAVGPR;
        public override bool Equals(object o) => o is Watch w && Equals(w);
        public override int GetHashCode() => (Name, Info, IsAVGPR).GetHashCode();
        public static bool operator ==(Watch left, Watch right) => left.Equals(right);
        public static bool operator !=(Watch left, Watch right) => !(left == right);
    }
}
