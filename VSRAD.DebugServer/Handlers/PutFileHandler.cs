﻿using System;
using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer.Handlers
{
    public sealed class PutFileHandler : IHandler
    {
        private readonly PutFileCommand _command;

        public PutFileHandler(PutFileCommand command)
        {
            _command = command;
        }

        public async Task<IResponse> RunAsync()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_command.FilePath));
                await File.WriteAllBytesAsync(_command.FilePath, _command.Data);
                return new PutFileResponse { Status = PutFileStatus.Successful };
            }
            catch (UnauthorizedAccessException)
            {
                return new PutFileResponse { Status = PutFileStatus.PermissionDenied };
            }
            catch (IOException)
            {
                return new PutFileResponse { Status = PutFileStatus.OtherIOError };
            }
        }
    }
}
