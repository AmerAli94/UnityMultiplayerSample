using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using NetworkMessages;
using System.Text;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;

    //Hearbeat Dict
    private Dictionary<string, float> heartBeat = new Dictionary<string, float>();

    //Players Dictionary 
    private Dictionary<string, NetworkObjects.NetworkPlayer> ConnectedPlayers = new Dictionary<string, NetworkObjects.NetworkPlayer>(); //Dictionary for all clients

    void Start()
    {

        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);

        // updates every sec 10 times
        InvokeRepeating("UpdatePlayerInfo", 0.1f, 0.1f);
    }
    void OnDisconnect(int i)
    {
        Debug.Log("Client " + m_Connections[i].InternalId.ToString() + " disconnected from server");
        m_Connections[i] = default(NetworkConnection);
    }
    void SendToClient(string message, NetworkConnection c)
    {
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message), Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(NetworkConnection c)
    {

        //Create a new message when we connect to get our ID
        PlayerUpdateMsg MsgID = new PlayerUpdateMsg();
        MsgID.cmd = Commands._ID;
        //Convert to string and send to client c
        MsgID.player.id = c.InternalId.ToString();
        Debug.Log("Connection Successful with ID: " + MsgID.player.id);

        Assert.IsTrue(c.IsCreated);
        SendToClient(JsonUtility.ToJson(MsgID), c);

        //new server update message for current players in the game
        ServerUpdateMsg MsgCurrPlayers = new ServerUpdateMsg();
        MsgCurrPlayers.cmd = Commands.CURRENT_PLAYERS;
        //iterating through the ConnectedPlayers dictionary
        foreach (KeyValuePair<string, NetworkObjects.NetworkPlayer> item in ConnectedPlayers)
        {
            //add current players to the msg
            MsgCurrPlayers.players.Add(item.Value);
        }

        Assert.IsTrue(c.IsCreated);
        SendToClient(JsonUtility.ToJson(MsgCurrPlayers), c);

        //new player update message to add new player to the current players.
        PlayerUpdateMsg MsgAddPlayer = new PlayerUpdateMsg();
        MsgAddPlayer.cmd = Commands.ADD_PLAYER;
        MsgAddPlayer.player.id = c.InternalId.ToString();

        for (int i = 0; i < m_Connections.Length; i++)
        {
            //send messege to client to add player
            Assert.IsTrue(m_Connections[i].IsCreated);
            SendToClient(JsonUtility.ToJson(MsgAddPlayer), m_Connections[i]);
        }

        m_Connections.Add(c);

    }

    void OnData(DataStreamReader stream, int i, NetworkConnection client)
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
            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Player position " + puMsg.player.pos);
                UpdatePlayerInfo(puMsg);
                break;
            case Commands.SERVER_UPDATE:
                ServerUpdateMsg ServerUpdateMessage = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server Update.");
                break;
            default:
                Debug.Log("ServerError (Default Log)");
                break;
        }
    }




    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

 
    void Update()
    {
        //list to maintain dropped clients
        List<string> DroppedClientsList = new List<string>();

        m_Driver.ScheduleUpdate().Complete();
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }
        NetworkConnection c = m_Driver.Accept();
        while (c != default(NetworkConnection))
        {
            OnConnect(c);
            c = m_Driver.Accept();
        }

        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);
            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            while (cmd != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i, m_Connections[i]);

                    heartBeat[m_Connections[i].InternalId.ToString()] = Time.time;
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }

        foreach (KeyValuePair<string, float> item in heartBeat)
        {

            if (Time.time - item.Value >= 5f)
            {
                //add the clent to the dropped client list
                DroppedClientsList.Add(item.Key);
            }
        }

        //check for droppedclientlist for being empty, if not..
        if (DroppedClientsList.Count != 0)
        {
            
            for (int i = 0; i < DroppedClientsList.Count; ++i)
            {
                //remove from the dictionaries.
                ConnectedPlayers.Remove(DroppedClientsList[i]);
                heartBeat.Remove(DroppedClientsList[i]);
            }

            DroppedPlayersMsg MsgDroppedPlayers = new DroppedPlayersMsg();
            MsgDroppedPlayers.DROPPEDPLAYERSLIST = DroppedClientsList;

          
            for (int i = 0; i < m_Connections.Length; i++)
            {
                if (DroppedClientsList.Contains(m_Connections[i].InternalId.ToString()) == true)
                {
                    continue;
                }
                //send dropped players message to client 
                Assert.IsTrue(m_Connections[i].IsCreated);
                SendToClient(JsonUtility.ToJson(MsgDroppedPlayers), m_Connections[i]);
            }
        }

    }

    void UpdatePlayerInfo(PlayerUpdateMsg puMsg)
    {
        if (ConnectedPlayers.ContainsKey(puMsg.player.id))
        {
            ConnectedPlayers[puMsg.player.id].id = puMsg.player.id;
            ConnectedPlayers[puMsg.player.id].pos = puMsg.player.pos;
            ConnectedPlayers[puMsg.player.id].isDropped = puMsg.player.isDropped;
        }

    }
}