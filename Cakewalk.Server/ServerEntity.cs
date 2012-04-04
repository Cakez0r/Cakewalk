using System.Net.Sockets;
using Cakewalk.Server.Zones;
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

        /// <summary>
        /// Reference to the world's zone manager
        /// </summary>
        private ZoneManager m_zoneManager;
        
        public ServerEntity(Socket socket, int worldID, ZoneManager zoneManager) : base(socket, worldID)
        {
            m_zoneManager = zoneManager;
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

            //Move the user in to a new zone
            else if (packet is RequestZoneTransfer)
            {
                RequestZoneTransfer request = (RequestZoneTransfer)packet;

                m_zoneManager.RequestZoneTransfer(this, request.ZoneID);
            }
        }
    }
}
