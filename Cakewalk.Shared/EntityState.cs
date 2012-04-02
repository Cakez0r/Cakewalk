using System.Runtime.InteropServices;

namespace Cakewalk.Shared
{
    /// <summary>
    /// Represents one entity's state
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack=1)]
    public struct EntityState
    {
        public short X;
        public short Y;
        public byte Rot;
        public byte Flags;
        public int Time;
    }
}
