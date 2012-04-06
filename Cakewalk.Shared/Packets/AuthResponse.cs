using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AuthResponse : IPacketBase
    {
        private PacketHeader m_header;
        private int m_worldID;

        public PacketHeader Header
        {
            get { return m_header; }
        }

        public int WorldID
        {
            get { return m_worldID; }
            set { m_worldID = value; }
        }

        public void SetupHeader()
        {
            m_header = new PacketHeader()
            {
                OpCode = PacketCode.AuthResponse,
                SizeInBytes = (short)Marshal.SizeOf(this)
            };
        }
    }
}
