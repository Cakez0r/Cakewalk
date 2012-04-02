using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cakewalk.Shared.Packets;

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
        PushStates5             = 4,
        PushStates10            = 5,
        PushStates25            = 6,
        PushStates50            = 7,
        PushStates100           = 8,
        PushStates150           = 9,
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
            { PacketCode.PushStates5,          typeof(PushStates5) },
            { PacketCode.PushStates10,         typeof(PushStates10) },
            { PacketCode.PushStates25,         typeof(PushStates25) },
            { PacketCode.PushStates50,         typeof(PushStates50) },
            { PacketCode.PushStates100,        typeof(PushStates100) },
        };

        private static Dictionary<Type, PacketCode> s_codeMap = new Dictionary<Type, PacketCode>()
        {
            { typeof(AuthRequest),              PacketCode.AuthRequest },
            { typeof(AuthResponse),             PacketCode.AuthResponse },
            { typeof(PushState),                PacketCode.PushState },
            { typeof(PushStates5),              PacketCode.PushStates5 },
            { typeof(PushStates10),             PacketCode.PushStates10 },
            { typeof(PushStates25),             PacketCode.PushStates25 },
            { typeof(PushStates50),             PacketCode.PushStates50 },
            { typeof(PushStates100),            PacketCode.PushStates100 },
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

