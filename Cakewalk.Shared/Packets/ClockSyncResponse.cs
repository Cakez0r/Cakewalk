using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ClockSyncResponse : IPacketBase
    {
        private PacketHeader m_header;
        private int m_time;

        public PacketHeader Header
        {
            get { return m_header; }
        }

        public int Time
        {
            get { return m_time; }
            set { m_time = value; }
        }

        public void SetupHeader()
        {
            m_header = new PacketHeader()
            {
                OpCode = PacketCode.ClockSyncResponse,
                SizeInBytes = (short)Marshal.SizeOf(this)
            };
        }
    }
}
