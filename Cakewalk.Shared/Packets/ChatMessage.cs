using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct ChatMessage : IPacketBase
    {
        private PacketHeader m_header;
        public int m_recipientID;
        public fixed byte Message[256];

        public PacketHeader Header
        {
            get { return m_header; }
        }

        public void SetupHeader()
        {
            m_header = new PacketHeader()
            {
                OpCode = PacketCode.ChatMessage,
                SizeInBytes = (short)(Marshal.SizeOf(this) - 256)
            };
        }

        public void SetText(string message)
        {
            fixed (byte* messageBuffer = Message)
            {
                TextHelpers.StringToBuffer(message, messageBuffer, 256);
            }
            m_header.SizeInBytes = (short)(Marshal.SizeOf(this) - 256 + message.Length + 1);
        }
    }
}
