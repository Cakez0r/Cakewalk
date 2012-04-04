using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RequestZoneTransfer : IPacketBase
    {
        private short m_opCode;

        private int m_zoneID;

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }

        public int SizeInBytes
        {
            get { return Marshal.SizeOf(this); }
        }

        public int ZoneID
        {
            get { return m_zoneID; }
            set { m_zoneID = value; }
        }
    }
}
