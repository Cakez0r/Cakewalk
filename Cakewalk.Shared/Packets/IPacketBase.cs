namespace Cakewalk.Shared.Packets
{
    /// <summary>
    /// Base interface of all packets
    /// </summary>
    public interface IPacketBase
    {
        PacketHeader Header { get; }

        void SetupHeader();
    }
}
