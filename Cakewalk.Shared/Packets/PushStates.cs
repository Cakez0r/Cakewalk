using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Cakewalk.Shared.Packets
{
    //Copy and pasted code below...
    //REASONING: Packets MUST be structs to use 'fixed'. This is necessary to block copy memory when (de)serializing.
    //Structs can't have a base class.
    //It's a waste to send 1 state in an array of 100 (bandwidth) and it's ineffecient to send 100 users in 100 seperate packets.
    //Hence 5 differently sized packets that are all effectively the same! Sorry. If it can be done a better way, please pull request.

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct PushStates5 : IPacketBase
    {
        const int MAX_STATES = 5;

        private short m_opCode;
        private int m_stateCount;

        public fixed int WorldIDs[MAX_STATES];

        public fixed short X[MAX_STATES];
        public fixed short Y[MAX_STATES];
        public fixed byte Rot[MAX_STATES];

        public fixed int Time[MAX_STATES];

        public fixed byte Flags[MAX_STATES];

        public fixed int RESERVED_A[MAX_STATES];
        public fixed short RESERVED_B[MAX_STATES];

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }

        public int StateCount
        {
            get { return m_stateCount; }
            set { m_stateCount = value; }
        }

        /// <summary>
        /// Pulls a state out of the packet at the given index
        /// </summary>
        public EntityState GetState(int index)
        {
            //Pin, pin pin
            fixed (int* times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        //Copy to new struct
                        return new EntityState()
                        {
                            X = xarray[index],
                            Y = yarray[index],
                            Flags = flagsarray[index],
                            Time = times[index],
                            Rot = rotarray[index]
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Adds a state into this packet
        /// </summary>
        public void AddState(int worldID, short x, short y, byte rot, int time, byte flags)
        {
            //Pin, pin pin!
            fixed (int* worldIDS = WorldIDs, times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        //Fill array slots with data
                        worldIDS[m_stateCount] = worldID;
                        xarray[m_stateCount] = x;
                        yarray[m_stateCount] = y;
                        rotarray[m_stateCount] = rot;
                        times[m_stateCount] = time;
                        flagsarray[m_stateCount] = flags;

                        //Move "pointer" to next slot
                        m_stateCount++;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the world ID at a given index
        /// </summary>
        public int GetWorldID(int index)
        {
            fixed (int* worldIDs = WorldIDs)
            {
                return worldIDs[index];
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct PushStates10 : IPacketBase
    {
        const int MAX_STATES = 10;

        private short m_opCode;
        private int m_stateCount;

        public fixed int WorldIDs[MAX_STATES];

        public fixed short X[MAX_STATES];
        public fixed short Y[MAX_STATES];
        public fixed byte Rot[MAX_STATES];

        public fixed int Time[MAX_STATES];

        public fixed byte Flags[MAX_STATES];

        public fixed int RESERVED_A[MAX_STATES];
        public fixed short RESERVED_B[MAX_STATES];

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }

        public int StateCount
        {
            get { return m_stateCount; }
            set { m_stateCount = value; }
        }

        public EntityState GetState(int index)
        {
            fixed (int* times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        return new EntityState()
                        {
                            X = xarray[index],
                            Y = yarray[index],
                            Flags = flagsarray[index],
                            Time = times[index],
                            Rot = rotarray[index]
                        };
                    }
                }
            }
        }

        public void AddState(int worldID, short x, short y, byte rot, int time, byte flags)
        {
            fixed (int* worldIDS = WorldIDs, times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        worldIDS[m_stateCount] = worldID;
                        xarray[m_stateCount] = x;
                        yarray[m_stateCount] = y;
                        rotarray[m_stateCount] = rot;
                        times[m_stateCount] = time;
                        flagsarray[m_stateCount] = flags;

                        m_stateCount++;
                    }
                }
            }
        }

        public int GetWorldID(int index)
        {
            fixed (int* worldIDs = WorldIDs)
            {
                return worldIDs[index];
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct PushStates25 : IPacketBase
    {
        const int MAX_STATES = 25;

        private short m_opCode;
        private int m_stateCount;

        public fixed int WorldIDs[MAX_STATES];

        public fixed short X[MAX_STATES];
        public fixed short Y[MAX_STATES];
        public fixed byte Rot[MAX_STATES];

        public fixed int Time[MAX_STATES];

        public fixed byte Flags[MAX_STATES];

        public fixed int RESERVED_A[MAX_STATES];
        public fixed short RESERVED_B[MAX_STATES];

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }

        public int StateCount
        {
            get { return m_stateCount; }
            set { m_stateCount = value; }
        }

        public EntityState GetState(int index)
        {
            fixed (int* times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        return new EntityState()
                        {
                            X = xarray[index],
                            Y = yarray[index],
                            Flags = flagsarray[index],
                            Time = times[index],
                            Rot = rotarray[index]
                        };
                    }
                }
            }
        }

        public void AddState(int worldID, short x, short y, byte rot, int time, byte flags)
        {
            fixed (int* worldIDS = WorldIDs, times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        worldIDS[m_stateCount] = worldID;
                        xarray[m_stateCount] = x;
                        yarray[m_stateCount] = y;
                        rotarray[m_stateCount] = rot;
                        times[m_stateCount] = time;
                        flagsarray[m_stateCount] = flags;

                        m_stateCount++;
                    }
                }
            }
        }

        public int GetWorldID(int index)
        {
            fixed (int* worldIDs = WorldIDs)
            {
                return worldIDs[index];
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct PushStates50 : IPacketBase
    {
        const int MAX_STATES = 50;

        private short m_opCode;
        private int m_stateCount;

        public fixed int WorldIDs[MAX_STATES];

        public fixed short X[MAX_STATES];
        public fixed short Y[MAX_STATES];
        public fixed byte Rot[MAX_STATES];

        public fixed int Time[MAX_STATES];

        public fixed byte Flags[MAX_STATES];

        public fixed int RESERVED_A[MAX_STATES];
        public fixed short RESERVED_B[MAX_STATES];

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }

        public int StateCount
        {
            get { return m_stateCount; }
            set { m_stateCount = value; }
        }

        public EntityState GetState(int index)
        {
            fixed (int* times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        return new EntityState()
                        {
                            X = xarray[index],
                            Y = yarray[index],
                            Flags = flagsarray[index],
                            Time = times[index],
                            Rot = rotarray[index]
                        };
                    }
                }
            }
        }

        public void AddState(int worldID, short x, short y, byte rot, int time, byte flags)
        {
            fixed (int* worldIDS = WorldIDs, times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        worldIDS[m_stateCount] = worldID;
                        xarray[m_stateCount] = x;
                        yarray[m_stateCount] = y;
                        rotarray[m_stateCount] = rot;
                        times[m_stateCount] = time;
                        flagsarray[m_stateCount] = flags;

                        m_stateCount++;
                    }
                }
            }
        }

        public int GetWorldID(int index)
        {
            fixed (int* worldIDs = WorldIDs)
            {
                return worldIDs[index];
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct PushStates100 : IPacketBase
    {
        const int MAX_STATES = 100;

        private short m_opCode;
        private int m_stateCount;

        public fixed int WorldIDs[MAX_STATES];
        
        public fixed short X[MAX_STATES];
        public fixed short Y[MAX_STATES];
        public fixed byte Rot[MAX_STATES];

        public fixed int Time[MAX_STATES];

        public fixed byte Flags[MAX_STATES];

        public fixed int RESERVED_A[MAX_STATES];
        public fixed short RESERVED_B[MAX_STATES];

        public PacketCode OpCode
        {
            get { return (PacketCode)m_opCode; }
            set { m_opCode = (short)value; }
        }

        public int StateCount
        {
            get { return m_stateCount; }
            set { m_stateCount = value; }
        }

        public EntityState GetState(int index)
        {
            fixed (int* times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        return new EntityState()
                        {
                            X = xarray[index],
                            Y = yarray[index],
                            Flags = flagsarray[index],
                            Time = times[index],
                            Rot = rotarray[index]
                        };
                    }
                }
            }
        }

        public void AddState(int worldID, short x, short y, byte rot, int time, byte flags)
        {
            fixed (int* worldIDS = WorldIDs, times = Time)
            {
                fixed (short* xarray = X, yarray = Y)
                {
                    fixed (byte* rotarray = Rot, flagsarray = Flags)
                    {
                        worldIDS[m_stateCount] = worldID;
                        xarray[m_stateCount] = x;
                        yarray[m_stateCount] = y;
                        rotarray[m_stateCount] = rot;
                        times[m_stateCount] = time;
                        flagsarray[m_stateCount] = flags;

                        m_stateCount++;
                    }
                }
            }
        }

        public int GetWorldID(int index)
        {
            fixed (int* worldIDs = WorldIDs)
            {
                return worldIDs[index];
            }
        }
    }
}
