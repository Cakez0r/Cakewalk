using System.Net.Sockets;
using Cakewalk.Shared;
using Cakewalk.Shared.Packets;

namespace Cakewalk.Server
{
    /// <summary>
    /// Represents a player connected to the server.
    /// </summary>
    public class ServerEntity : NetEntity
    {
        /// <summary>
        /// The last state received from this entity
        /// </summary>
        public EntityState LastState
        {
            get;
            private set;
        }
        
        public ServerEntity(Socket socket, int worldID) : base(socket, worldID)
        {
        }

        /// <summary>
        /// Incoming packets are pushed here for handling.
        /// </summary>
        protected override void HandlePacket(IPacketBase packet)
        {
            base.HandlePacket(packet);

            //Update state from the client
            if (packet is PushState)
            {
                PushState state = (PushState)packet;
                //Push their state if it's the correct world ID
                if (state.WorldID == WorldID)
                {
                    LastState = state.State;
                }
            }
        }
    }
}
