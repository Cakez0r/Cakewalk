using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PushState : IPacketBase
    {
        private short m_opCode;
        private int m_worldID;
        private EntityState m_state;

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }

        public int SizeInBytes
        {
            get { return Marshal.SizeOf(this); }
        }

        public int WorldID
        {
            get { return m_worldID; }
            set { m_worldID = value; }
        }

        public EntityState State
        {
            get { return m_state; }
            set { m_state = value; }
        }
    }
}
