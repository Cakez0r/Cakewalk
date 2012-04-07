using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ClockSyncRequest : IPacketBase
    {
        private PacketHeader m_header;

        public PacketHeader Header
        {
            get { return m_header; }
        }

        public void SetupHeader()
        {
            m_header = new PacketHeader()
            {
                OpCode = PacketCode.ClockSyncRequest,
                SizeInBytes = (short)Marshal.SizeOf(this)
            };
        }
    }
}
