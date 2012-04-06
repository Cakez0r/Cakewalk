using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cakewalk.Shared.Packets;

namespace Cakewalk.Shared
{
    /// <summary>
    /// Helper for creating packets
    /// </summary>
    public static class PacketFactory
    {
        /// <summary>
        /// New up a packet and fill it with mandatory data
        /// </summary>
        public static T CreatePacket<T>() where T : IPacketBase, new()
        {
            T packet = new T();
            packet.SetupHeader();

            return packet;
        }
    }
}
