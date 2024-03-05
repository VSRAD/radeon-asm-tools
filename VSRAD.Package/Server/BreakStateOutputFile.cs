﻿using System;

namespace VSRAD.Package.Server
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct BreakStateOutputFile
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public string Path { get; }
        public bool BinaryOutput { get; }
        public int Offset { get; }
        public DateTime Timestamp { get; }
        public int DwordCount { get; }

        public BreakStateOutputFile(string path, bool binaryOutput, int offset, DateTime timestamp, int dwordCount)
        {
            Path = path;
            BinaryOutput = binaryOutput;
            Offset = offset;
            Timestamp = timestamp;
            DwordCount = dwordCount;
        }
    }
}
