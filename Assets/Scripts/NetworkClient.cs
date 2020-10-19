using UnityEngine;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;


public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string ID;
    public string serverIP;
    public ushort serverPort;
    
    //assign playercube prefab in inspector
    [SerializeField]
    GameObject PlayerPrefab;
    //assign playercube prefab in inspector
    [SerializeField]
    Transform PlayerTransform;

    //new player update messge to send playerinfo to the server.
    PlayerUpdateMsg MsgPlayerInfo = new PlayerUpdateMsg();

    //Dictionary of ConnectedPlayers.
    private Dictionary<string, GameObject> ConnectedPlayers = new Dictionary<string, GameObject>();

    void Start()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        serverIP = "3.12.76.48"//"127.0.0.1";
        var endpoint = NetworkEndPoint.Parse(serverIP, serverPort);
        m_Connection = m_Driver.Connect(endpoint);

    }

    void SendToServer(string Message)
    {
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(Message), Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect()
    {
        Debug.Log("Connected to server");
        //Send the info to server every sec 
        InvokeRepeating("SendInfoToServer", 0.1f, 0.1f);
    }

    void OnDisconnect()
    {
        Debug.Log("Client/Server connection lost");
        m_Connection = default(NetworkConnection);
    }


    void OnData(DataStreamReader stream)
    {
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length, Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch (header.cmd)
        {
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received!");
                break;
            case Commands._ID:
                //new message to update player ID
                PlayerUpdateMsg puIDMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Player connected with ID " + puIDMsg.player.id);
                MsgPlayerInfo.player.id = puIDMsg.player.id;
                ID = MsgPlayerInfo.player.id;
                break;
            case Commands.PLAYER_UPDATE:
                //new message to to update player position from server
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Player position: " + puMsg.player.pos);
                break;
            case Commands.SERVER_UPDATE:
                //message to manage server updates.
                ServerUpdateMsg ServerUpdateMessage = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server Update sent.");
                for (int i = 0; i < ServerUpdateMessage.players.Count; ++i)
                {
                    if (ConnectedPlayers.ContainsKey(ServerUpdateMessage.players[i].id))
                    {
                        //assign respective player position  as per the server
                        ConnectedPlayers[ServerUpdateMessage.players[i].id].transform.position = ServerUpdateMessage.players[i].pos;
                        //handles dropped clients on server.
                        if (ServerUpdateMessage.players[i].isDropped)
                        {
                            ConnectedPlayers[ServerUpdateMessage.players[i].id].GetComponent<PlayerInput>().isDropped = true;
                        }
                    }       

                }
                break;
            case Commands.CURRENT_PLAYERS:
                //messsage to manage current players in server
                //instantiates a cube & sets the ID and position
                ServerUpdateMsg MsgCurrentPlayers = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("server received Current Players info.");
                for (int i = 0; i < MsgCurrentPlayers.players.Count; ++i)
                {
                    GameObject PlayerCube = Instantiate(PlayerPrefab);
                    ConnectedPlayers[MsgCurrentPlayers.players[i].id] = PlayerCube;
                    PlayerCube.transform.position = MsgCurrentPlayers.players[i].pos;
                }
                break;
            case Commands.ADD_PLAYER:
                //new message to manage add player
                //instantiate a cube & added set the ID.
                PlayerUpdateMsg MsgAddPlayer = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                GameObject NewPlayerCube = Instantiate(PlayerPrefab);
                ConnectedPlayers[MsgAddPlayer.player.id] = NewPlayerCube;
             
                break;
            case Commands.DROPPED_PLAYERS:
                //new message to manage dropped clients
                //if dropped client list has ids in it destroy and remove it.
                DroppedPlayersMsg DroppedMessage = JsonUtility.FromJson<DroppedPlayersMsg>(recMsg);
                Debug.Log("Dropped Client");
                for (int i = 0; i < DroppedMessage.DROPPEDPLAYERSLIST.Count; ++i)
                {
                    if (ConnectedPlayers.ContainsKey(DroppedMessage.DROPPEDPLAYERSLIST[i]))
                    {
                        Destroy(ConnectedPlayers[DroppedMessage.DROPPEDPLAYERSLIST[i]]);
                        ConnectedPlayers.Remove(DroppedMessage.DROPPEDPLAYERSLIST[i]);
                    }
                }
                break;
            default:
                Debug.Log("Unrecognized message received!");
                break;
        }
    }
    void Disconnect()
    {
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }
    void Update()
    {

        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;

        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
                
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }

    void SendInfoToServer()
    {
        //a parameter less sendtoserver function to be able to use invokerepeating
        MsgPlayerInfo.player.pos = PlayerTransform.position;
        MsgPlayerInfo.player.isDropped = PlayerTransform.gameObject.GetComponent<PlayerInput>().isDropped;
        SendToServer(JsonUtility.ToJson(MsgPlayerInfo));
    }
}