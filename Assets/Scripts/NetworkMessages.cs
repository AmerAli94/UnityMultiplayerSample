using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkMessages
{
    public enum Commands
    {
        PLAYER_UPDATE,

        SERVER_UPDATE,

        HANDSHAKE,

        _ID,

        ADD_PLAYER,

        CURRENT_PLAYERS,

        DROPPED_PLAYERS
    }

  

    [System.Serializable]
    public class ServerUpdateMsg : NetworkHeader
    {
        public List<NetworkObjects.NetworkPlayer> players;
        public ServerUpdateMsg()
        {
            cmd = Commands.SERVER_UPDATE;
            players = new List<NetworkObjects.NetworkPlayer>();
        }
    }


    [System.Serializable]
    public class NetworkHeader
    {
        public Commands cmd;
    }

    [System.Serializable]
    public class HandshakeMsg : NetworkHeader
    {
        public NetworkObjects.NetworkPlayer player;

        public HandshakeMsg()
        {
            cmd = Commands.HANDSHAKE;
            player = new NetworkObjects.NetworkPlayer();
        }
    }



    [System.Serializable]
    public class PlayerUpdateMsg : NetworkHeader
    {
        public NetworkObjects.NetworkPlayer player;
        public PlayerUpdateMsg()
        {
            cmd = Commands.PLAYER_UPDATE;
            player = new NetworkObjects.NetworkPlayer();
        }
    };

    //to update the dropped clients on server
    [System.Serializable]
    public class DroppedPlayersMsg : NetworkHeader
    {
        public List<string> DROPPEDPLAYERSLIST;
        public DroppedPlayersMsg()
        {
            cmd = Commands.DROPPED_PLAYERS;
            DROPPEDPLAYERSLIST = new List<string>();
        }
    }
}

namespace NetworkObjects
{
    [System.Serializable]
    public class NetworkObject
    {
        public string id;
    }

    [System.Serializable]
    public class NetworkPlayer : NetworkObject
    {
        public Color color;
        public Vector3 pos;
        public bool isDropped;

        public NetworkPlayer()
        {
            color =  new Color();
            pos = Vector3.zero;
            isDropped = false;
        }
    }
}