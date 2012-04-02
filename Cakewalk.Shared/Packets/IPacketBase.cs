namespace Cakewalk.Shared.Packets
{
    /// <summary>
    /// Base interface of all packets
    /// </summary>
    public interface IPacketBase
    {
        int SizeInBytes { get; }
        PacketCode OpCode { get; set; }
    }
}
