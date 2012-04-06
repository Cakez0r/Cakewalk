using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PushState : IPacketBase
    {
        private PacketHeader m_header;
        private int m_worldID;
        private EntityState m_state;

        public PacketHeader Header
        {
            get { return m_header; }
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

        public void SetupHeader()
        {
            m_header = new PacketHeader()
            {
                OpCode = PacketCode.PushState,
                SizeInBytes = (short)Marshal.SizeOf(this)
            };
        }
    }
}
