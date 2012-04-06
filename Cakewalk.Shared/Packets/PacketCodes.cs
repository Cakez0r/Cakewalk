using System;
using System.Collections.Generic;

namespace Cakewalk.Shared.Packets
{
    /// <summary>
    /// All packet types. Used for serialization.
    /// </summary>
    public enum PacketCode : short
    {
        BadType                 = 0,
        AuthRequest             = 1,
        AuthResponse            = 2,
        PushState               = 3,
        CoalescedData           = 4,
        RequestZoneTransfer     = 5,
        WhoisRequest            = 6,
        WhoisResponse           = 7
    }

    /// <summary>
    /// Maps packet types to packet codes and vice versa. Used for serialization.
    /// </summary>
    public static class PacketMap
    {
        private static Dictionary<PacketCode, Type> s_typeMap = new Dictionary<PacketCode, Type>()
        {
            { PacketCode.AuthRequest,          typeof(AuthRequest) },
            { PacketCode.AuthResponse,         typeof(AuthResponse) },
            { PacketCode.PushState,            typeof(PushState) },
            { PacketCode.CoalescedData,        typeof(CoalescedData) },
            { PacketCode.RequestZoneTransfer,  typeof(RequestZoneTransfer) },
            { PacketCode.WhoisRequest,         typeof(WhoisRequest) },
            { PacketCode.WhoisResponse,        typeof(WhoisResponse) },
        };

        /// <summary>
        /// Get the type for a given packet code.
        /// </summary>
        public static Type GetTypeForPacketCode(PacketCode c)
        {
            Type t = null;

            s_typeMap.TryGetValue(c, out t);

            return t;
        }
    }
}

