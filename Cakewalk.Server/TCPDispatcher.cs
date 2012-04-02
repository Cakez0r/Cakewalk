using System;
using System.Net;
using System.Net.Sockets;

namespace Cakewalk
{
    public enum DispatcherState
    {
        Stopped,
        Listening,
        AcceptingSocket
    }

    /// <summary>
    /// TCP Listener to accept people to the world
    /// </summary>
    public class TCPDispatcher
    {
        /// <summary>
        /// Handles user connections
        /// </summary>
        public event Action<Socket> SocketConnected;

        /// <summary>
        /// The current state of this dispatcher
        /// </summary>
        public DispatcherState State
        {
            get;
            private set;
        }

        /// <summary>
        /// TCP listener
        /// </summary>
        private TcpListener m_listener;

        /// <summary>
        /// Create a tcp listener bound to the specified address and port
        /// </summary>
        public TCPDispatcher(IPAddress bindAddress, ushort port)
        {
            State = DispatcherState.Stopped;
            m_listener = new TcpListener(bindAddress, port);
        }

        /// <summary>
        /// Start listening for connections
        /// </summary>
        public void Start()
        {
            m_listener.Start();
            BeginAcceptingSockets();
        }

        /// <summary>
        /// Handle a socket accept
        /// </summary>
        private void AcceptSocket(IAsyncResult result)
        {
            State = DispatcherState.AcceptingSocket;
            Socket sock = m_listener.EndAcceptSocket(result);

            //Queue the next accept
            BeginAcceptingSockets();

            //Fire the event handler for sockets
            if (SocketConnected != null)
            {
                SocketConnected(sock);
            }
            else
            {
                //boot the user if nobody is listening
                sock.Disconnect(false);
            }
        }

        /// <summary>
        /// Start an async socket accept
        /// </summary>
        private void BeginAcceptingSockets()
        {
            State = DispatcherState.Listening;
            m_listener.BeginAcceptSocket(AcceptSocket, null);
        }

        /// <summary>
        /// Stop listening for connections
        /// </summary>
        public void Stop()
        {
            m_listener.Stop();
            State = DispatcherState.Stopped;
        }
    }
}
