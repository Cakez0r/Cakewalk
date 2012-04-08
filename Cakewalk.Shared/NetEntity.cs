using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        /// The current network time
        /// </summary>
        public int NetworkTime
        {
            get { return Environment.TickCount + m_clockOffset; }
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
        /// Async send context object
        /// </summary>
        protected SocketAsyncEventArgs m_sendArgs;

        /// <summary>
        /// Async receive context object
        /// </summary>
        protected SocketAsyncEventArgs m_receiveArgs;

        /// <summary>
        /// Pointer to the send buffer, used to serialize packets directly to the buffer
        /// </summary>
        private IntPtr m_sendBufferPtr;

        /// <summary>
        /// Incoming IO queue
        /// </summary>
        private ConcurrentQueue<IPacketBase> m_incomingQueue = new ConcurrentQueue<IPacketBase>();

        /// <summary>
        /// Outgoing IO queue
        /// </summary>
        private ConcurrentQueue<IPacketBase> m_outgoingQueue = new ConcurrentQueue<IPacketBase>();

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
        /// GC handle for pinning the working buffer
        /// </summary>
        private GCHandle m_workingPacketBufferHandle;

        /// <summary>
        /// Cancellation token for net IO
        /// </summary>
        private CancellationTokenSource m_ioStopToken;

        /// <summary>
        /// Are we waiting to get a clock sync response back?
        /// </summary>
        private bool m_awaitingClockSyncResponse = false;

        /// <summary>
        /// The time that the last clock sync request was sent
        /// </summary>
        private int m_clockSyncSendTime = 0;

        /// <summary>
        /// A list of the delta history
        /// </summary>
        private Queue<int> m_roundTripTimes = new Queue<int>();

        /// <summary>
        /// Flag to indicate whether a send is currently in progress.
        /// </summary>
        private long sending = 0;

        /// <summary>
        /// The offset to apply to the local clock to get the remote time
        /// </summary>
        private int m_clockOffset = 0;

        #region TEMPORARY COUNTERS! Clean this!
        public static int IN = 0;
        public static int OUT = 0;
        public static int BOUT = 0;
        public static int BIN = 0;
        #endregion

        /// <summary>
        /// Create a new entity from a socket and immediately assign it a world id.
        /// Requires async socket context objects with buffers pre-assigned
        /// </summary>
        public NetEntity(Socket socket, int worldID, SocketAsyncEventArgs sendEventArgs, SocketAsyncEventArgs receiveEventArgs)
        {
            WorldID = worldID;

            m_sendArgs = sendEventArgs;
            m_sendArgs.Completed += SendCompleted;
            m_receiveArgs = receiveEventArgs;
            m_receiveArgs.Completed += ReceiveCompleted;

            unsafe
            {
                //Get the pointer for the send buffer
                byte[] buffer = m_sendArgs.Buffer;
                fixed (byte* bufPtr = buffer)
                {
                    m_sendBufferPtr = (IntPtr)(bufPtr + m_sendArgs.Offset);
                }
            }

            //Setup socket
            m_socket = socket;
            socket.NoDelay = true; //No nagling
            socket.ReceiveBufferSize = BUFFER_SIZE;
            socket.SendBufferSize = BUFFER_SIZE;

            AuthState = EntityAuthState.Unauthorised;

            //Pin buffers so that we can block copy structs to / from them
            m_workingPacketBufferHandle = GCHandle.Alloc(m_workingPacketBuffer, GCHandleType.Pinned);

            m_ioStopToken = new CancellationTokenSource();

            //Kick off net IO tasks
            //QueueSend();
            QueueReceive();
        }

        /// <summary>
        /// Begins data send task on the thread pool
        /// </summary>
        private void QueueSend()
        {
            //Update sending flag
            Interlocked.Exchange(ref sending, 1);

            //Start sending on the task pool
            Task sendTask = new Task(Send);
            sendTask.ContinueWith((t) => t.Dispose());
            sendTask.Start();
        }

        /// <summary>
        /// Begins data receive task on the thread pool
        /// </summary>
        private void QueueReceive()
        {
            //Start receiving on the task pool
            Task receiveTask = new Task(Receive);
            receiveTask.ContinueWith((t) => t.Dispose());
            receiveTask.Start();
        }

        /// <summary>
        /// Poll the outgoing queue and send any packets
        /// </summary>
        private void Send()
        {
            IPacketBase packet = null;

            //Grab a packet to send
            while (!m_outgoingQueue.TryDequeue(out packet))
            {
                Thread.Sleep(0);
            }

            //Bail out if we lost connection
            if (!m_socket.Connected)
            {
                return;
            }

            //Serialize the packet into the send buffer
            SerializePacket(packet);

            try
            {
                //Send packet bytes over the wire
                m_sendArgs.SetBuffer(m_sendArgs.Offset, packet.Header.SizeInBytes);
                if (!m_socket.SendAsync(m_sendArgs))
                {
                    SendCompleted(m_socket, m_sendArgs);
                }
            }
            catch (SocketException)
            {
                //Client disconnect
            }
            catch (Exception ex)
            {
                Console.WriteLine("Send error! " + ex);
            }
        }

        /// <summary>
        /// Serialize the given packet into the send buffer.
        /// </summary>
        private void SerializePacket(IPacketBase packet)
        {
            try
            {
                Marshal.StructureToPtr(packet, m_sendBufferPtr, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Serialization error! " + ex);
            }
        }

        /// <summary>
        /// Event handler for async send completion
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void SendCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                BOUT += e.BytesTransferred;
                OUT++;
                
                //Send again if there are more packets in the queue
                if (m_outgoingQueue.Count > 0)
                {
                    QueueSend();
                }
                else
                {
                    //Unset the sending flag if there is nothing more to send
                    Interlocked.Exchange(ref sending, 0);
                }
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
                    if (!m_socket.ReceiveAsync(m_receiveArgs))
                    {
                        ReceiveCompleted(m_socket, m_receiveArgs);
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
        /// Handle a received packet
        /// </summary>
        private void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                BIN += e.BytesTransferred;

                if (e.BytesTransferred > 0)
                {
                    int bytesConsumed = 0;

                    //While there are bytes to be consumed in the receive buffer...
                    while (bytesConsumed < e.BytesTransferred)
                    {
                        //Work out how many bytes (if any) we need to complete the packet in the working buffer
                        int bytesNeededForPacketCompletion = m_workingPacketSize - m_workingPacketReceivedBytes;

                        //If we're awaiting a new packet...
                        if (bytesNeededForPacketCompletion == 0)
                        {
                            if (e.BytesTransferred - bytesConsumed < 2)
                            {
                                //Haven't seen this happen since the refactor...
                                Console.WriteLine("PROBLEM!");
                            }

                            //Get the size we're expecting from the receive buffer
                            m_workingPacketSize = BitConverter.ToInt16(e.Buffer, e.Offset + bytesConsumed);

                            //Update the number of bytes we need to construct the whole packet
                            bytesNeededForPacketCompletion = m_workingPacketSize;
                        }

                        //Copy as much of the current working packet from the receive buffer as possible
                        int bytesToCopy = Math.Min(bytesNeededForPacketCompletion, e.BytesTransferred - bytesConsumed);
                        Buffer.BlockCopy(e.Buffer, e.Offset + bytesConsumed, m_workingPacketBuffer, m_workingPacketReceivedBytes, bytesToCopy);

                        //If we have enough data to complete the packet, we should deserialize it from the working buffer
                        if (e.BytesTransferred - bytesConsumed >= bytesNeededForPacketCompletion)
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


                    //m_receivePool.ReturnArgs(e);
                    QueueReceive();
                }
                else
                {
                    //We were sent junk. Disconnect...
                    m_socket.Disconnect(false);
                }
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

            //If we're not already sending, queue up a send on the task pool
            if (Interlocked.Read(ref sending) == 0)
            {
                QueueSend();
            }
        }

        /// <summary>
        /// Updates this entity, flushes and handles any pending packets in the incoming queue.
        /// </summary>
        public void Update(TimeSpan dt)
        {
            //Handle all packets in the incoming queue
            IPacketBase packet = null;
            while (m_incomingQueue.TryDequeue(out packet))
            {
                HandlePacket(packet);
            }

            //Send any packets that have been deferred
            foreach (CoalescedData p in m_deferredSendList)
            {
                SendPacket(p);
            }
            m_deferredSendList.Clear();

            if (m_currentDeferredPacket.PacketCount > 0)
            {
                SendPacket(m_currentDeferredPacket);
            }

            //Reset the current deferred packet for next update
            m_currentDeferredPacket = PacketFactory.CreatePacket<CoalescedData>();

            //Warn if any of the queues are getting swamped
            if (m_outgoingQueue.Count > 25)
            {
                Console.WriteLine("Outgoing queue swamped: " + m_outgoingQueue.Count);
            }
            if (m_incomingQueue.Count > 25)
            {
                Console.WriteLine("Incoming queue swamped: " + m_incomingQueue.Count);
            }
        }

        /// <summary>
        /// Clean up this entity
        /// </summary>
        public virtual void Dispose()
        {
            m_ioStopToken.Cancel();

            m_socket.Close();
            m_socket.Dispose();

            m_workingPacketBufferHandle.Free();
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

            //Synchronise clocks
            else if (packet is ClockSyncResponse)
            {
                ClockSyncResponse response = (ClockSyncResponse)packet;
                int rtt = Environment.TickCount - m_clockSyncSendTime;
                m_roundTripTimes.Enqueue(rtt);
                if (m_roundTripTimes.Count > 10)
                {
                    m_roundTripTimes.Dequeue();
                }
                SyncClock(response.Time);
                m_awaitingClockSyncResponse = false;
            }
            else if (packet is ClockSyncRequest)
            {
                ClockSyncResponse response = PacketFactory.CreatePacket<ClockSyncResponse>();
                response.Time = Environment.TickCount;
                SendPacket(response);
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

        /// <summary>
        /// Send a clock sync request, if there is not already one pending.
        /// </summary>
        public void SendClockSync()
        {
            if (!m_awaitingClockSyncResponse)
            {
                m_clockSyncSendTime = Environment.TickCount;
                m_awaitingClockSyncResponse = true;
                SendPacket(PacketFactory.CreatePacket<ClockSyncRequest>());
            }
        }

        /// <summary>
        /// Synchronises the network clock using any round trip time data available.
        /// </summary>
        private void SyncClock(int remoteTime)
        {
            //Calculate the offset between the remote and local time
            m_clockOffset = remoteTime - Environment.TickCount;

            //Calculate the average of any round-trip times we have
            int averageRTT = m_roundTripTimes.Sum() / m_roundTripTimes.Count;

            //Calculate the standard deviation of the RTTs
            int stddev = (int)Math.Sqrt(m_roundTripTimes.Select(n => (n - averageRTT)*(n - averageRTT)).Sum() / m_roundTripTimes.Count);

            //Calculate the median RTT
            int median = m_roundTripTimes.OrderBy(i => i).ElementAt(m_roundTripTimes.Count / 2);

            //Remove any RTTs that are > 1 standard deviation away from the median and calculate the average again
            int unskewedAverage = m_roundTripTimes.Where(i => Math.Abs(i - median) < stddev).Sum() / m_roundTripTimes.Count;

            //Apply half of this average to the clock offset to account for latency
            m_clockOffset += unskewedAverage / 2;
        }
    }
}
