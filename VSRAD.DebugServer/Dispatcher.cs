﻿using System;
using System.Threading.Tasks;
using VSRAD.DebugServer.Handlers;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServer
{
    public static class Dispatcher
    {
        public static Task<IResponse> DispatchAsync(ICommand command, ClientLogger clientLog, NetworkClient client) => command switch
        {
            Execute e => new ExecuteHandler(e, clientLog).RunAsync(),
            FetchMetadata fm => new FetchMetadataHandler(fm).RunAsync(),
            FetchResultRange frr => new FetchResultRangeHandler(frr).RunAsync(),
            PutFileCommand pf => new PutFileHandler(pf).RunAsync(),
            CheckOutdatedFiles co => new CheckOutdatedFilesHandler(co).RunAsync(),
            ListFilesCommand lf => new ListFilesHandler(lf).RunAsync(),
            Deploy d => new DeployHandler(d, clientLog).RunAsync(),
            ListEnvironmentVariables lev => new ListEnvironmentVariablesHandler(lev).RunAsync(),
            SendFileCommand sf => new SendFileHandler(sf, client).RunAsync(),
            GetFileCommand gf => new GetFileHandler(gf, client, clientLog).RunAsync(),
            PutDirectoryCommand pd => new PutDirectoryHandler(pd).RunAsync(),
            _ => throw new ArgumentException($"Unknown command type {command.GetType()}"),
        };
    }
}
