using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using DotNetty.Transport.Channels.Sockets;

namespace DotNetty.KCP
{
    public class ServerEndPointChannelManager : IChannelManager
    {
        private readonly ConcurrentDictionary<EndPoint, Ukcp> _ukcps = new ConcurrentDictionary<EndPoint, Ukcp>();

        public Ukcp get(DatagramPacket msg)
        {
            _ukcps.TryGetValue(msg.Sender, out var ukcp);
            return ukcp;
        }

        public void New(EndPoint endPoint, Ukcp ukcp, DatagramPacket msg)
        {
            _ukcps[endPoint] = ukcp;
        }

        public void del(Ukcp ukcp)
        { 
            if (!_ukcps.TryRemove(ukcp.user().RemoteAddress, out var temp))
            {
                Console.WriteLine("ukcp session is not exist RemoteAddress: " + ukcp.user().RemoteAddress);
            }
        }

        public ICollection<Ukcp> getAll()
        {
            return _ukcps.Values;
        }
    }
}