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
        CoalescedData           = 4
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
        };

        private static Dictionary<Type, PacketCode> s_codeMap = new Dictionary<Type, PacketCode>()
        {
            { typeof(AuthRequest),              PacketCode.AuthRequest },
            { typeof(AuthResponse),             PacketCode.AuthResponse },
            { typeof(PushState),                PacketCode.PushState },
            { typeof(CoalescedData),            PacketCode.CoalescedData },
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

        /// <summary>
        /// Get the packed code for a given type
        /// </summary>
        public static PacketCode GetPacketCodeForType(Type t)
        {
            PacketCode code = PacketCode.BadType;

            s_codeMap.TryGetValue(t, out code);

            return code;
        }
    }
}

