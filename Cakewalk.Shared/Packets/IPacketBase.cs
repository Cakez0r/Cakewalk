using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    /// <summary>
    /// Base interface of all packets
    /// </summary>
    public interface IPacketBase
    {
        PacketCode OpCode { get; set; }
    }
}
