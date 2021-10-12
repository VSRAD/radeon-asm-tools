﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class FetchResultRangeHandlerTest
    {
        [Fact]
        public async void FetchResultRangeBinaryTest()
        {
            var tmpFile = Path.GetTempFileName();
            var data = Encoding.UTF8.GetBytes("Real Data");
            File.WriteAllBytes(tmpFile, data);
            var timestamp = File.GetLastWriteTime(tmpFile).ToUniversalTime();

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 0,
                    ByteCount = data.Length + 1,
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(timestamp, response.Timestamp);
            Assert.Equal(data, response.Data);

            int offset = 2;
            int count = 3;

            response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = offset,
                    ByteCount = count,
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(data.Skip(offset).Take(count), response.Data);
        }

        [Theory]
        [InlineData(0, 0, new uint[] { 0, 1, 2, 3, 4, 313_313_313, 0 })]
        [InlineData(4, 666, new uint[] { 1, 2, 3, 4, 313_313_313, 0 })]
        [InlineData(6, 8, new uint[] { 1, 2 })]
        [InlineData(10, 4, new uint[] { 2 })]
        public async void FetchResultRangeTextTest(int byteOffset, int byteCount, uint[] expectedData)
        {
            var tmpFile = Path.GetTempFileName();
            var data = new string[]
            {
                "Metadata",
                "0x0",
                "0x00000001",
                "00000002",    // 0x prefix is not required
                "00000003h",   // h suffix is accepted
                "   ",         // should be ignored
                "0x00000004",
                "",            // should be ignored
                @"¯\_(ツ)_/¯", // should be ignored
                "12 AC c8 21", // spaces between digits are allowed
                "",
                "0x00000000"
            };
            File.WriteAllLines(tmpFile, data);
            var timestamp = File.GetLastWriteTimeUtc(tmpFile);
            var byteData = expectedData.SelectMany(BitConverter.GetBytes).ToArray();

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = byteOffset,
                    ByteCount = byteCount,
                    OutputOffset = 1,
                    BinaryOutput = false
                }); ;
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(timestamp, response.Timestamp);
            Assert.Equal(byteData, response.Data);

            File.Delete(tmpFile);
        }

        [Theory]
        [InlineData("0x1\r\n0x2\n0x3\n0x4\r0x5", 0, new uint[] { 1, 2, 3, 4, 5 })]
        [InlineData("Meta\n\r\n0x0\n0x1\r\n", 3, new uint[] { 1 })]
        public async void FetchResultRangeTextLineEndingsTest(string contents, int skipLines, uint[] expectedData)
        {
            var tmpFile = Path.GetTempFileName();
            File.WriteAllText(tmpFile, contents);
            var timestamp = File.GetLastWriteTimeUtc(tmpFile);
            var byteData = expectedData.SelectMany(BitConverter.GetBytes).ToArray();

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 0,
                    ByteCount = 0,
                    OutputOffset = skipLines,
                    BinaryOutput = false
                }); ;
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(timestamp, response.Timestamp);
            Assert.Equal(byteData, response.Data);

            File.Delete(tmpFile);
        }

        [Fact]
        public async void FetchAllFileBinaryTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            var data = Encoding.UTF8.GetBytes("Real Data Here");
            File.WriteAllBytes(tmpFile, data);

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 0,
                    ByteCount = 0,
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(data, response.Data);
        }

        [Fact]
        public async void FetchAllFileBinaryWithOffsetTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            File.WriteAllBytes(tmpFile, Encoding.UTF8.GetBytes("XXXReal Data Here"));

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    OutputOffset = 3,
                    ByteOffset = 0,
                    ByteCount = 0,
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(Encoding.UTF8.GetBytes("Real Data Here"), response.Data);
        }

        [Fact]
        public async void FetchAllFileTextTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            var data = new[] { "0x313", "0x42", "0x69", "0x1", "0x0" };
            await File.WriteAllLinesAsync(tmpFile, data);
            var byteData = new byte[20]
            {
                19, 3, 0, 0,    // 0x313
                66, 0, 0, 0,    // 0x42
                105, 0, 0, 0,   // 0x69
                1, 0, 0, 0,     // 0x1
                0, 0, 0, 0      // 0x0
            };

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 0,
                    ByteCount = 0,
                    OutputOffset = 0,
                    BinaryOutput = false
                }); ;
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(byteData, response.Data);
        }

        [Fact]
        public async void FetchEmptyTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            using (var stream = File.Create(tmpFile))
            {
                stream.Close();
                stream.Dispose();
                File.SetLastWriteTime(tmpFile, DateTime.Now);
            };

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = int.Parse("DEAD", System.Globalization.NumberStyles.HexNumber),
                    ByteCount = 666,
                    BinaryOutput = true
                });
            File.Delete(tmpFile);
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Empty(response.Data);
        }

        [Fact]
        public async void FetchFileNotFoundTestAsync()
        {
            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { @"Do:\You", "Exist?" },
                    ByteOffset = int.Parse("DEAD", System.Globalization.NumberStyles.HexNumber),
                    ByteCount = 666,
                    BinaryOutput = true
                });
            Assert.Equal(FetchStatus.FileNotFound, response.Status);
            Assert.Equal(Array.Empty<byte>(), response.Data);
        }

        [Fact]
        public async void OutputOffsetTestAsync()
        {
            var tmpFile = Path.GetTempFileName();
            var byteData = new byte[8] { 90, 85, 80, 70, 40, 10, 0, 0 };

            await File.WriteAllBytesAsync(tmpFile, byteData);

            var response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
                new FetchResultRange
                {
                    FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                    ByteOffset = 1,
                    ByteCount = 666,
                    BinaryOutput = true,
                    OutputOffset = 4
                });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(new byte[3] { 10, 0, 0 }, response.Data);

            var stringData = new string[]
            {
                "<...-Restricted accesы: do not proceed without special permission-...>",
                "<...-Property of NERV corporation. All rights reserved-...>",
                "<...-EVA00 logfile-...>",
                "<...-System state snapshot 11/01/1996 00:00:15-...>",
                "0x00000016",
                "0x00000022",
                "0x00000064",
                "0x00000044",
                "0x00000000",
                "0x00000055",
                "0x00000077",
                "0x00000014",
            };

            await File.WriteAllLinesAsync(tmpFile, stringData);

            response = await Helper.DispatchCommandAsync<FetchResultRange, ResultRangeFetched>(
               new FetchResultRange
               {
                   FilePath = new string[] { Path.GetDirectoryName(tmpFile), Path.GetFileName(tmpFile) },
                   ByteOffset = 4,
                   ByteCount = 8,
                   BinaryOutput = false,
                   OutputOffset = 4
               });
            Assert.Equal(FetchStatus.Successful, response.Status);
            Assert.Equal(new byte[8] { 34, 0, 0, 0, 100, 0, 0, 0 }, response.Data);
        }
    }
}
