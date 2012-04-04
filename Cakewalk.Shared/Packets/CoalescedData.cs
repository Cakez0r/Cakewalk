using System;
using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    /// <summary>
    /// Represents a collection of other packets, packed in to one buffer
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct CoalescedData : IPacketBase
    {
        //Should fit within standard MTU of 1500
        private const int BUFFER_SIZE = 1400;

        private short m_opCode;
        private byte m_packetCount;
        private short m_usedBytes;

        /// <summary>
        /// Buffer for all other packets
        /// </summary>
        public fixed byte DataBuffer[BUFFER_SIZE];

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }

        /// <summary>
        /// Amount of packets contained within the buffer
        /// </summary>
        public byte PacketCount
        {
            get { return m_packetCount; }
            set { m_packetCount = value; }
        }

        /// <summary>
        /// Total size in bytes of this packet
        /// </summary>
        public int SizeInBytes
        {
            get { return 5 + m_usedBytes; }
        }

        /// <summary>
        /// Try to add a packet into the buffer. Returns true if the packet was successfully copied.
        /// </summary>
        public bool TryAddPacket(IPacketBase packet)
        {
            fixed (byte* buf = DataBuffer)
            {
                int packetSize = packet.SizeInBytes;

                if (m_usedBytes + packetSize < BUFFER_SIZE)
                {
                    //Copy packet into buffer
                    Marshal.StructureToPtr(packet, (IntPtr)(buf + m_usedBytes), false);

                    //Update used bytes and packet count
                    m_usedBytes += (short)packetSize;
                    m_packetCount++;

                    return true;
                }
            }

            return false;
        }
    }
}
