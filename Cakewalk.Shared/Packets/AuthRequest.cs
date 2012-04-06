using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AuthRequest : IPacketBase
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
                OpCode = PacketCode.AuthRequest,
                SizeInBytes = (short)Marshal.SizeOf(this)
            };
        }
    }
}
