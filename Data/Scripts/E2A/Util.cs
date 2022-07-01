using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E2A___Ammo_From_Energy.E2A
{
    class Util
    {
        public static bool IsClient
        {
            get
            {
                if (MyAPIGateway.Multiplayer != null && MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Utilities.IsDedicated)
                {
                    return !MyAPIGateway.Multiplayer.IsServer;
                }

                return true;
            }
        }
        public static bool IsDedicated
        {
            get
            {
                return IsServer && !IsClient;
            }
        }

        public static bool IsMultiplayer
        {
            get
            {
                return MyAPIGateway.Multiplayer != null && MyAPIGateway.Multiplayer.MultiplayerActive;
            }
        }

        public static bool IsServer
        {
            get
            {
                if (MyAPIGateway.Multiplayer != null && MyAPIGateway.Multiplayer.MultiplayerActive)
                    return MyAPIGateway.Multiplayer.IsServer;

                return true;
            }
        }
    }
}
