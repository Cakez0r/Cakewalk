using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Cakewalk.Shared
{
    public unsafe static class TextHelpers
    {
        //.NET has no version of memcpy???
        [DllImport("msvcrt.dll", EntryPoint = "memcpy")]
        public unsafe static extern void memcpy(IntPtr pDest, IntPtr pSrc, int length);

        public static void StringToBuffer(string s, byte* b, int maxLength)
        {
            IntPtr sptr = Marshal.StringToHGlobalAnsi(s);
            memcpy((IntPtr)b, sptr, Math.Min(s.Length, maxLength));
            Marshal.FreeHGlobal(sptr);
        }

        public static string BufferToString(byte* b, int length)
        {
            return new string((sbyte*)b, 0, length, Encoding.ASCII).TrimEnd('\0');
        }
    }
}
