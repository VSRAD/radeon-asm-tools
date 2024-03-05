﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;

namespace VSRAD.DebugServer
{
    public sealed class Server
    {
        public static readonly Version MinimumClientVersion = new Version("2024.3.3");

        private readonly SemaphoreSlim _commandExecutionLock = new SemaphoreSlim(1, 1);
        private readonly TcpListener _listener;
        private readonly Logging.GlobalLogger _globalLog;

        public Server(IPAddress ip, int port, Logging.GlobalLogger globalLog)
        {
            _listener = new TcpListener(ip, port);
            _globalLog = globalLog;
        }

        public async Task LoopAsync()
        {
            _listener.Start();
            uint clientsCount = 0;
            while (true)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                var clientId = clientsCount;
                clientsCount++;
                _ = Task.Run(() => BeginClientLoopAsync(clientId, tcpClient));
            }
        }

        private async Task BeginClientLoopAsync(uint clientId, TcpClient tcpClient)
        {
            using (tcpClient)
            {
                var clientLog = _globalLog.CreateClientLogger(clientId);
                clientLog.ConnectionEstablished(tcpClient.Client.RemoteEndPoint);
                while (true)
                {
                    var lockAcquired = false;
                    try
                    {
                        var (command, bytesReceived) = await tcpClient.GetStream().ReadSerializedCommandAsync<ICommand>().ConfigureAwait(false);
                        clientLog.CommandReceived(command, bytesReceived);

                        await _commandExecutionLock.WaitAsync();
                        lockAcquired = true;

                        var response = await Dispatcher.DispatchAsync(command, clientLog).ConfigureAwait(false);
                        if (response != null) // commands like Deploy do not return a response
                        {
                            var bytesSent = await tcpClient.GetStream().WriteSerializedMessageAsync(response).ConfigureAwait(false);
                            clientLog.ResponseSent(response, bytesSent);
                        }
                        clientLog.CommandProcessed();
                    }
                    catch (Exception e) when (IsConnectionResetException(e))
                    {
                        clientLog.CliendDisconnected();
                        break;
                    }
                    catch (Exception e)
                    {
                        clientLog.FatalClientException(e);
                        break;
                    }
                    finally
                    {
                        if (lockAcquired)
                            _commandExecutionLock.Release();
                    }
                }
            }
        }

        private static bool IsConnectionResetException(Exception e) => e switch
        {
            OperationCanceledException _ => true,
            EndOfStreamException _ => true,
            _ when e.InnerException is SocketException se => se.SocketErrorCode == SocketError.ConnectionReset || se.SocketErrorCode == SocketError.ConnectionAborted,
            _ => false
        };
    }
}
