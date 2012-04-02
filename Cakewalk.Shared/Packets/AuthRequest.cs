﻿using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AuthRequest : IPacketBase
    {
        private short m_opCode;

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }

        public int SizeInBytes
        {
            get { return Marshal.SizeOf(this); }
        }
    }
}
