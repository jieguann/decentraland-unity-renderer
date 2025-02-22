using DCl.Social.Friends;
using RPC.Services;
using RPC.Transports;
using rpc_csharp;
using rpc_csharp.transport;
using System;
using System.Collections.Generic;

namespace RPC
{
    public static class RPCServerBuilder
    {
        public static RpcServer<RPCContext> BuildDefaultServer(RPCContext context)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var transport = new WebGLTransport();
#else
            var transport = new WebSocketTransport();
#endif
            return BuildDefaultServer(context, transport);
        }

        public static RpcServer<RPCContext> BuildDefaultServer(RPCContext context, ITransport transport)
        {
            return BuildServer(context, transport)
                   .RegisterService(CRDTServiceImpl.RegisterService)
                   .RegisterService(TransportServiceImpl.RegisterService)
                   .RegisterService(EmotesRendererServiceImpl.RegisterService)
                   .RegisterService(RPCFriendsApiBridge.RegisterService)
                   .RegisterService(SceneControllerServiceImpl.RegisterService)
                   .Build();
        }

        public static RPCServerBuilder<RPCContext> BuildServer(RPCContext context, ITransport transport)
        {
            return new RPCServerBuilder<RPCContext>(transport, context);
        }
    }

    public class RPCServerBuilder<T>
    {
        private readonly ITransport transport;
        private readonly T context;
        private readonly List<Action<RpcServerPort<T>>> servicesRegisterer = new List<Action<RpcServerPort<T>>>();

        internal RPCServerBuilder(ITransport transport, T context)
        {
            this.transport = transport;
            this.context = context;
        }

        public RPCServerBuilder<T> RegisterService(Action<RpcServerPort<T>> serviceRegisterer)
        {
            servicesRegisterer.Add(serviceRegisterer);
            return this;
        }

        public RpcServer<T> Build()
        {
            var services = servicesRegisterer.ToArray();
            var rpcServer = new RpcServer<T>();
            rpcServer.AttachTransport(transport, context);

            rpcServer.SetHandler((port, t, c) =>
            {
                for (int i = 0; i < services.Length; i++)
                {
                    services[i].Invoke(port);
                }
            });
            return rpcServer;
        }
    }
}
