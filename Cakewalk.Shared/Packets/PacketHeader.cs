using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader
    {
        private short m_sizeInBytes;
        private short m_opCode;

        public short SizeInBytes
        {
            get { return m_sizeInBytes; }
            set { m_sizeInBytes = value; }
        }

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }
    }
}
