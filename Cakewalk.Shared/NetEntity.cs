using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Cakewalk.Shared;
using Cakewalk.Shared.Packets;

namespace Cakewalk
{
    public enum EntityAuthState
    {
        Authorising,
        Authorised,
        Unauthorised
    }

    /// <summary>
    /// Networking core of the server
    /// </summary>
    public abstract class NetEntity : IDisposable
    {
        /// <summary>
        /// Send and receive buffer size
        /// </summary>
        private const int BUFFER_SIZE = 2048;

        /// <summary>
        /// This entity's current authorisation state
        /// </summary>
        public EntityAuthState AuthState
        {
            get;
            private set;
        }

        /// <summary>
        /// Is this entity's socket connected?
        /// </summary>
        public bool IsConnected
        {
            get { return m_socket.Connected; }
        }

        /// <summary>
        /// The world ID of this entity
        /// </summary>
        private int m_worldID = -1;
        public int WorldID
        {
            get { return m_worldID; }
            set
            {
                if (m_worldID == -1)
                {
                    m_worldID = value;
                }
                else
                {
                    //Don't let the world ID be set more than once... This will just cause headaches and bugs with indexing players
                    throw new Exception("Resetting world ID is not allowed!");
                }
            }
        }

        /// <summary>
        /// The socket for net IO with this entity
        /// </summary>
        private Socket m_socket;

        /// <summary>
        /// Incoming IO queue
        /// </summary>
        ConcurrentQueue<IPacketBase> m_incomingQueue = new ConcurrentQueue<IPacketBase>();

        /// <summary>
        /// Outgoing IO queue
        /// </summary>
        ConcurrentQueue<IPacketBase> m_outgoingQueue = new ConcurrentQueue<IPacketBase>();

        /// <summary>
        /// Receive buffer for the socket
        /// </summary>
        byte[] m_receiveBuffer = new byte[BUFFER_SIZE];

        /// <summary>
        /// Receive buffer for the socket
        /// </summary>
        byte[] m_sendBuffer = new byte[BUFFER_SIZE];

        /// <summary>
        /// GC handle for pinning the receive buffer
        /// </summary>
        GCHandle m_receiveBufferHandle;

        /// <summary>
        /// GC handle for pinning the send buffer
        /// </summary>
        GCHandle m_sendBufferHandle;

        /// <summary>
        /// Task for running network sends
        /// </summary>
        Task m_sendTask;

        /// <summary>
        /// Task for running network receives.
        /// </summary>
        Task m_receiveTask;

        /// <summary>
        /// Cancellation token for net IO
        /// </summary>
        CancellationTokenSource m_ioStopToken;

        /// <summary>
        /// Blank buffer
        /// </summary>
        private static readonly byte[] ZERO_BUFFER = new byte[BUFFER_SIZE];

        #region TEMPORARY COUNTERS! Clean this!
        public static int IN = 0;
        public static int OUT = 0;
        public static int BOUT = 0;
        public static int BIN = 0;
        #endregion

        /// <summary>
        /// Create a new entity from a socket
        /// </summary>
        public NetEntity(Socket socket) : this(socket, -1)
        {
        }

        /// <summary>
        /// Create a new entity from a socket and immediately assign it a world id
        /// </summary>
        public NetEntity(Socket socket, int worldID)
        {
            WorldID = worldID;

            //Setup socket
            m_socket = socket;
            socket.NoDelay = true; //No nagling
            socket.ReceiveBufferSize = BUFFER_SIZE;
            socket.DontFragment = true; //No annoying bugs kthx

            AuthState = EntityAuthState.Unauthorised;

            //Pin buffers so that we can block copy structs to / from them
            m_sendBufferHandle = GCHandle.Alloc(m_sendBuffer, GCHandleType.Pinned);
            m_receiveBufferHandle = GCHandle.Alloc(m_receiveBuffer, GCHandleType.Pinned);

            m_ioStopToken = new CancellationTokenSource();

            //Kick off net IO tasks
            QueueSend();
            QueueReceive();
        }

        /// <summary>
        /// Begins data send task on the thread pool
        /// </summary>
        private void QueueSend()
        {
            m_sendTask = new Task(Send, m_ioStopToken.Token, TaskCreationOptions.LongRunning);
            m_sendTask.ContinueWith((t) => t.Dispose());
            m_sendTask.Start();
        }

        /// <summary>
        /// Begins data receive task on the thread pool
        /// </summary>
        private void QueueReceive()
        {
            m_receiveTask = new Task(Receive, m_ioStopToken.Token, TaskCreationOptions.LongRunning);
            m_receiveTask.ContinueWith((t) => t.Dispose());
            m_receiveTask.Start();
        }

        /// <summary>
        /// Poll the outgoing queue and send any packets
        /// </summary>
        private void Send()
        {
            while (true)
            {
                IPacketBase packet = null;
                while (!m_outgoingQueue.TryDequeue(out packet))
                {
                    //Spin until we get something to send
                    Thread.Sleep(5);
                }

                //Bail out if we lost connection
                if (!m_socket.Connected)
                {
                    return;
                }

                //Clear the send buffer
                Buffer.BlockCopy(ZERO_BUFFER, 0, m_sendBuffer, 0, BUFFER_SIZE);

                //Get size (in bytes) of the packet
                int packetSize = packet.SizeInBytes;

                //Fill the send buffer with the packet bytes
                SerializePacket(packet);

                try
                {
                    //Send packet bytes over the wire
                    m_socket.Send(m_sendBuffer, packetSize, SocketFlags.None);

                    BOUT += packetSize;
                    OUT++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Send error! " + ex);
                }

                //Yield
                Thread.Sleep(0);
            }
        }

        /// <summary>
        /// Receive and dispatch packets from the socket
        /// </summary>
        private void Receive()
        {
            //Receive loop
            while (true)
            {
                try
                {
                    if (m_socket.Connected)
                    {
                        //Receive data from the wire
                        int bytesReceived = m_socket.Receive(m_receiveBuffer);

                        BIN += bytesReceived;

                        //Pass it off to the handler
                        HandleReceive(bytesReceived);
                    }
                    else
                    {
                        m_socket.Dispose();
                        return;
                    }

                }
                catch (SocketException)
                {
                    //Socket disconnected
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Receive error! " + ex);
                }

                //Yield
                Thread.Sleep(0);
            }
        }

        /// <summary>
        /// Handle a received packet
        /// </summary>
        private void HandleReceive(int bytesReceived)
        {
            if (bytesReceived > 1)
            {
                //Pull the packet code from the buffer and cast it to enum
                short opCode = BitConverter.ToInt16(m_receiveBuffer, 0);
                PacketCode packetCode = (PacketCode)opCode;

                DeserializePacket(packetCode, m_receiveBufferHandle.AddrOfPinnedObject());

                IN++;
            }
            else
            {
                //We were sent junk. Disconnect...
                m_socket.Disconnect(false);
            }
        }

        /// <summary>
        /// Deserializes a packet from the given buffer. Returns the amount of bytes consumed.
        /// </summary>
        private int DeserializePacket(PacketCode packetCode, IntPtr buffer)
        {
            //Get the type for the packet sitting in the receive buffer
            Type packetType = PacketMap.GetTypeForPacketCode(packetCode);

            //If we can deserialize it...
            if (packetType != null)
            {
                try
                {
                    //Block copy the buffer to a struct of the correct type
                    IPacketBase packet = (IPacketBase)Marshal.PtrToStructure(buffer, packetType);

                    //Throw the strongly-typed packet into the incoming queue
                    m_incomingQueue.Enqueue(packet);

                    return packet.SizeInBytes;
                }
                catch (Exception ex)
                {
                    Console.Write("Deserialization error! " + ex);
                }
            }

            return 0;
        }

        /// <summary>
        /// Enqueue a packet for sending over the wire
        /// </summary>
        public void SendPacket(IPacketBase packet)
        {
            m_outgoingQueue.Enqueue(packet);
        }

        /// <summary>
        /// Serialize the given packet into the send buffer.
        /// </summary>
        private void SerializePacket(IPacketBase packet)
        {
            try
            {
                Marshal.StructureToPtr(packet, m_sendBufferHandle.AddrOfPinnedObject(), true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Serialization error! " + ex);
            }
        }

        /// <summary>
        /// Updates this entity, flushes and handles any pending packets in the incoming queue.
        /// </summary>
        public void Update(TimeSpan dt)
        {
            //Take a snapshot of how many packets are in the queue
            int dequeueCount = m_incomingQueue.Count;

            for (int i = 0; i < dequeueCount; i++)
            {
                //Try handling all packets that were in the queue at the start of this update
                IPacketBase packet = null;
                m_incomingQueue.TryDequeue(out packet);
                HandlePacket(packet);
            }
        }

        /// <summary>
        /// Clean up this entity
        /// </summary>
        public void Dispose()
        {
            m_ioStopToken.Cancel();

            m_socket.Close();
            m_socket.Dispose();

            m_sendBufferHandle.Free();
            m_receiveBufferHandle.Free();
        }

        /// <summary>
        /// Packet handler logic
        /// </summary>
        protected virtual void HandlePacket(IPacketBase packet)
        {
            //Send auth responses for an auth request
            if (packet is AuthRequest)
            {
                AuthResponse response = PacketFactory.CreatePacket<AuthResponse>();
                //Tell them their world ID
                response.WorldID = WorldID;
                SendPacket(response);
                AuthState = EntityAuthState.Authorised;
            }

            //Update auth state and world ID
            else if (packet is AuthResponse)
            {
                AuthResponse response = (AuthResponse)packet;
                WorldID = response.WorldID;
                AuthState = EntityAuthState.Authorised;
            }

            //Unpack coalesced packets
            else if (packet is CoalescedData)
            {
                CoalescedData data = (CoalescedData)packet;

                unsafe
                {
                    byte* ptr = data.DataBuffer;
                    //Start deserializing packets from the buffer
                    for (int i = 0; i < data.PacketCount; i++)
                    {
                        //Pull the packet code from the buffer and cast it to enum
                        short opCode = *(short*)ptr;
                        PacketCode packetCode = (PacketCode)opCode;
                        
                        //Deserialize and advance pointer to next packet in the buffer
                        ptr += DeserializePacket(packetCode, (IntPtr)ptr);
                    }
                }
            }
        }

        /// <summary>
        /// Send an authorisation request.
        /// </summary>
        public virtual void Authorise()
        {
            //Don't send multiple auths
            if (AuthState == EntityAuthState.Unauthorised)
            {
                AuthState = EntityAuthState.Authorising;
                SendPacket(PacketFactory.CreatePacket<AuthRequest>());
            }
        }
    }
}
