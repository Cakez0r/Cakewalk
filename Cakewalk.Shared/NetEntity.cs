using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Cakewalk.Shared;
using Cakewalk.Shared.Packets;
using System.Collections.Generic;

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
        private const int BUFFER_SIZE = 1460;

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
        /// List of packets that have accumulated to be sent at the next update
        /// </summary>
        private List<CoalescedData> m_deferredSendList = new List<CoalescedData>();

        /// <summary>
        /// The current working deferred packet
        /// </summary>
        private CoalescedData m_currentDeferredPacket = PacketFactory.CreatePacket<CoalescedData>();

        /// <summary>
        /// Incoming IO queue
        /// </summary>
        private ConcurrentQueue<IPacketBase> m_incomingQueue = new ConcurrentQueue<IPacketBase>();

        /// <summary>
        /// Outgoing IO queue
        /// </summary>
        private ConcurrentQueue<IPacketBase> m_outgoingQueue = new ConcurrentQueue<IPacketBase>();

        /// <summary>
        /// Receive buffer for the socket
        /// </summary>
        private byte[] m_receiveBuffer = new byte[BUFFER_SIZE];

        /// <summary>
        /// Receive buffer for the socket
        /// </summary>
        private byte[] m_sendBuffer = new byte[BUFFER_SIZE];

        /// <summary>
        /// Working buffer for constructing packets from the tcp stream
        /// </summary>
        private byte[] m_workingPacketBuffer = new byte[BUFFER_SIZE];

        /// <summary>
        /// The expected size of the working packet
        /// </summary>
        private int m_workingPacketSize = 0;

        /// <summary>
        /// The amount of bytes received for the current working packet
        /// </summary>
        private int m_workingPacketReceivedBytes = 0;

        /// <summary>
        /// GC handle for pinning the receive buffer
        /// </summary>
        private GCHandle m_receiveBufferHandle;

        /// <summary>
        /// GC handle for pinning the send buffer
        /// </summary>
        private GCHandle m_sendBufferHandle;

        /// <summary>
        /// GC handle for pinning the working buffer
        /// </summary>
        private GCHandle m_workingPacketBufferHandle;

        /// <summary>
        /// Context object for async receives
        /// </summary>
        private SocketAsyncEventArgs m_receiveAsyncArgs;

        /// <summary>
        /// Task for running network sends
        /// </summary>
        private Task m_sendTask;

        /// <summary>
        /// Task for running network receives.
        /// </summary>
        private Task m_receiveTask;

        /// <summary>
        /// Cancellation token for net IO
        /// </summary>
        private CancellationTokenSource m_ioStopToken;

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
            socket.SendBufferSize = BUFFER_SIZE;

            AuthState = EntityAuthState.Unauthorised;

            //Pin buffers so that we can block copy structs to / from them
            m_sendBufferHandle = GCHandle.Alloc(m_sendBuffer, GCHandleType.Pinned);
            m_receiveBufferHandle = GCHandle.Alloc(m_receiveBuffer, GCHandleType.Pinned);
            m_workingPacketBufferHandle = GCHandle.Alloc(m_workingPacketBuffer, GCHandleType.Pinned);

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
            m_receiveAsyncArgs = new SocketAsyncEventArgs();
            m_receiveAsyncArgs.SetBuffer(m_receiveBuffer, 0, BUFFER_SIZE);
            m_receiveAsyncArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveCompleted);

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

                //Get size (in bytes) of the packet
                int packetSize = packet.Header.SizeInBytes;

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
        /// Starts an async receive from the socket
        /// </summary>
        private void Receive()
        {
            try
            {
                if (m_socket.Connected)
                {
                    //Receive data from the wire
                    if (!m_socket.ReceiveAsync(m_receiveAsyncArgs))
                    {
                        ReceiveCompleted(m_socket, m_receiveAsyncArgs);
                    }
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
        }

        /// <summary>
        /// Event handler for an async receive completion
        /// </summary>
        private void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                BIN += e.BytesTransferred;
                HandleReceive(e.BytesTransferred);
                Receive();
            }
        }

        /// <summary>
        /// Handle a received packet
        /// </summary>
        private void HandleReceive(int bytesReceived)
        {
            if (bytesReceived > 1)
            {
                int bytesConsumed = 0;
                //While there are bytes to be consumed in the receive buffer...
                while (bytesConsumed < bytesReceived)
                {
                    //Work out how many bytes (if any) we need to complete the packet in the working buffer
                    int bytesNeededForPacketCompletion = m_workingPacketSize - m_workingPacketReceivedBytes;

                    //If we're awaiting a new packet...
                    if (bytesNeededForPacketCompletion == 0)
                    {
                        //Get the size we're expecting from the receive buffer
                        m_workingPacketSize = BitConverter.ToInt16(m_receiveBuffer, bytesConsumed);

                        //Update the number of bytes we need to construct the whole packet
                        bytesNeededForPacketCompletion = m_workingPacketSize;
                    }

                    //Copy as much of the current working packet from the receive buffer as possible
                    int bytesToCopy = Math.Min(bytesNeededForPacketCompletion, bytesReceived - bytesConsumed);
                    Buffer.BlockCopy(m_receiveBuffer, bytesConsumed, m_workingPacketBuffer, m_workingPacketReceivedBytes, bytesToCopy);

                    //If we have enough data to complete the packet, we should deserialize it from the working buffer
                    if (bytesReceived - bytesConsumed >= bytesNeededForPacketCompletion)
                    {
                        //Deserialize from the working buffer
                        DeserializePacket(m_workingPacketBufferHandle.AddrOfPinnedObject());

                        //Expect a new packet from the stream
                        m_workingPacketReceivedBytes = 0;
                        m_workingPacketSize = 0;

                        IN++;
                    }
                    else
                    {
                        //If we didn't receive enough bytes to complete the packet, update the counters so that we can continue constructing it next receive
                        m_workingPacketReceivedBytes += bytesToCopy;
                    }

                    //Count how many bytes we've used from the buffer (there may be multiple packets per receive)
                    bytesConsumed += bytesToCopy;
                }
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
        private int DeserializePacket(IntPtr buffer)
        {
            PacketHeader packetHeader = (PacketHeader)Marshal.PtrToStructure(buffer, typeof(PacketHeader));

            //Get the type for the packet sitting in the receive buffer
            Type packetType = PacketMap.GetTypeForPacketCode(packetHeader.OpCode);

            //If we can deserialize it...
            if (packetType != null)
            {
                try
                {
                    //Block copy the buffer to a struct of the correct type
                    IPacketBase packet = (IPacketBase)Marshal.PtrToStructure(buffer, packetType);

                    //Throw the strongly-typed packet into the incoming queue
                    m_incomingQueue.Enqueue(packet);

                    return packet.Header.SizeInBytes;
                }
                catch (Exception ex)
                {
                    Console.Write("Deserialization error! " + ex);
                }
            }
            else
            {
                Console.WriteLine("Bad packet code: " + packetHeader.OpCode);
            }

            return 0;
        }

        /// <summary>
        /// Queues a packet to be send on the next update. Will coalesce these packets together.
        /// </summary>
        public void DeferredSendPacket(IPacketBase packet)
        {
            if (!m_currentDeferredPacket.TryAddPacket(packet))
            {
                m_deferredSendList.Add(m_currentDeferredPacket);
                m_currentDeferredPacket = PacketFactory.CreatePacket<CoalescedData>();
                m_currentDeferredPacket.TryAddPacket(packet);
            }
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
                Marshal.StructureToPtr(packet, m_sendBufferHandle.AddrOfPinnedObject(), false);
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
                //Try handling all packets that were in the queue at the start of this upda
                IPacketBase packet = null;
                m_incomingQueue.TryDequeue(out packet);
                HandlePacket(packet);
            }

            //Send any packets that have been deferred
            foreach (CoalescedData packet in m_deferredSendList)
            {
                SendPacket(packet);
            }
            m_deferredSendList.Clear();

            if (m_currentDeferredPacket.PacketCount > 0)
            {
                SendPacket(m_currentDeferredPacket);
            }

            m_currentDeferredPacket = PacketFactory.CreatePacket<CoalescedData>();
        }

        /// <summary>
        /// Clean up this entity
        /// </summary>
        public virtual void Dispose()
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
                DeferredSendPacket(response);
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
                        //Deserialize and advance pointer to next packet in the buffer
                        ptr += DeserializePacket((IntPtr)ptr);
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
                DeferredSendPacket(PacketFactory.CreatePacket<AuthRequest>());
            }
        }
    }
}
