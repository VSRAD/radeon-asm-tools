﻿using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests
{
    class MockCommunicationChannel
    {
        public ICommunicationChannel Object => _mock.Object;

        private readonly Mock<ICommunicationChannel> _mock;

        private readonly Queue<(IResponse response, Action<ICommand> callback)> _replyInteractions =
            new Queue<(IResponse response, Action<ICommand> callback)>();

        private readonly Queue<Action<ICommand>> _nonReplyInteractions =
            new Queue<Action<ICommand>>();

        private readonly Queue<(IResponse[] response, Action<List<ICommand>>)> _bundledInteractions =
            new Queue<(IResponse[] response, Action<List<ICommand>>)>();

        public bool AllInteractionsHandled => _replyInteractions.Count == 0 && _nonReplyInteractions.Count == 0 && _bundledInteractions.Count == 0;

        public void RaiseConnectionStateChanged() => _mock.Raise((m) => m.ConnectionStateChanged += null);

        public MockCommunicationChannel(ServerPlatform platform = ServerPlatform.Windows)
        {
            _mock = new Mock<ICommunicationChannel>();
            _mock
                .Setup(c => c.GetServerCapabilityInfoAsync())
                .ReturnsAsync(new CapabilityInfo("", platform, CapabilityInfo.LatestServerCapabilities));
            _mock
                .Setup((c) => c.SendWithReplyAsync<ExecutionCompleted>(It.IsAny<Execute>()))
                .Returns<ICommand>((c) => Task.FromResult((ExecutionCompleted)HandleCommand(c)));
            _mock
                .Setup((c) => c.SendWithReplyAsync<MetadataFetched>(It.IsAny<FetchMetadata>()))
                .Returns<ICommand>((c) => Task.FromResult((MetadataFetched)HandleCommand(c)));
            _mock
                .Setup((c) => c.SendWithReplyAsync<ResultRangeFetched>(It.IsAny<FetchResultRange>()))
                .Returns<ICommand>((c) => Task.FromResult((ResultRangeFetched)HandleCommand(c)));
            _mock
                .Setup((c) => c.SendWithReplyAsync<PutFileResponse>(It.IsAny<PutFileCommand>()))
                .Returns<ICommand>((c) => Task.FromResult((PutFileResponse)HandleCommand(c)));
            _mock
                .Setup((c) => c.SendWithReplyAsync<PutDirectoryResponse>(It.IsAny<PutDirectoryCommand>()))
                .Returns<ICommand>((c) => Task.FromResult((PutDirectoryResponse)HandleCommand(c)));
            _mock
                .Setup((c) => c.SendWithReplyAsync<ListFilesResponse>(It.IsAny<ListFilesCommand>()))
                .Returns<ICommand>((c) => Task.FromResult((ListFilesResponse)HandleCommand(c)));
            _mock
                .Setup((c) => c.SendWithReplyAsync<GetFilesResponse>(It.IsAny<GetFilesCommand>()))
                .Returns<ICommand>((c) => Task.FromResult((GetFilesResponse)HandleCommand(c)));
        }

        public void ThenRespond<TCommand, TResponse>(TResponse response, Action<TCommand> processCallback)
            where TCommand : ICommand where TResponse : IResponse =>
            _replyInteractions.Enqueue((response, (c) => processCallback((TCommand)c)));

        public void ThenRespond<TResponse>(TResponse response)
            where TResponse : IResponse =>
            _replyInteractions.Enqueue((response, null));

        public void ThenRespond(IResponse[] response, Action<List<ICommand>> callback = null) =>
            _bundledInteractions.Enqueue((response, callback));

        public void ThenExpect<TCommand>(Action<TCommand> processCallback)
            where TCommand : ICommand =>
            _nonReplyInteractions.Enqueue((c) => processCallback((TCommand)c));

        public void ThenExpect<TCommand>()
            where TCommand : ICommand =>
            _nonReplyInteractions.Enqueue((c) => Assert.IsType<TCommand>(c));

        private IResponse HandleCommand(ICommand command)
        {
            if (_replyInteractions.Count == 0)
            {
                throw new Xunit.Sdk.XunitException("The test method has sent a request (and is waiting for a reply) when none was expected.");
            }
            var (response, callback) = _replyInteractions.Dequeue();
            callback?.Invoke(command);
            return response;
        }
    }
}
