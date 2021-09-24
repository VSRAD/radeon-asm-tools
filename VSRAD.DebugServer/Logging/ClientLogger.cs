﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace VSRAD.DebugServer
{
    public class ClientLogger
    {
        private readonly uint _clientId;
        private readonly bool _verbose;
        private readonly Stopwatch _timer;

        public ClientLogger(uint clientId, bool verbose)
        {
            _clientId = clientId;
            _verbose = verbose;
            _timer = new Stopwatch();
        }

        public void ConnectionEstablished(EndPoint clientEndpoint) =>
            Print($"Connection with {clientEndpoint} has been established.");

        public void CommandReceived(IPC.Commands.ICommand c, int bytesReceived)
        {
            Print($"Command received ({bytesReceived} bytes): {c}");
            if (_verbose)
                _timer.Restart();
        }

        public void ResponseSent(IPC.Responses.IResponse r, int bytesSent) =>
            Print($"Sent response ({bytesSent} bytes): {r}");

        public void FatalClientException(Exception e) =>
            Print("An exception has occurred while processing the command. Connection has been terminated." + Environment.NewLine + e.ToString());

        public void CliendDisconnected() =>
            Print("client has been disconnected");

        public void ExecutionStarted()
        {
            if (_verbose) Console.WriteLine("===");
        }

        public void StdoutReceived(string output)
        {
            if (_verbose)
                Console.WriteLine($"#{_clientId} stdout> " + output);
        }

        public void StderrReceived(string output)
        {
            if (_verbose)
                Console.WriteLine($"#{_clientId} stderr> " + output);
        }

        public void DeployItemsReceived(IEnumerable<string> outputPaths)
        {
            if (!_verbose) return;

            Console.WriteLine("Deploy Items:");
            foreach (var path in outputPaths)
                Console.WriteLine("-- " + path);
        }

        public void CommandProcessed()
        {
            if (!_verbose) return;

            _timer.Stop();
            Console.WriteLine($"{Environment.NewLine}Time Elapsed: {_timer.ElapsedMilliseconds}ms");
        }

        private void Print(string message) =>
            Console.WriteLine("===" + Environment.NewLine + $"[Client #{_clientId}] {message}");
    }
}
