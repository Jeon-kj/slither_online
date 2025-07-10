using System;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine.UI;

public class NetworkManager : MonoBehaviour
{
    TcpClient client;
    NetworkStream stream;

    public InputField inputField;
    public Button sendButton;

    volatile bool connected = false;
    volatile bool isRunning = false;
    const int MaxBodyLength = 10000;

    public GameObject playerPrefab; // 유니티에서 프리팹으로 연결

    ConcurrentQueue<(int type, string body)> recvQueue = new();
    Dictionary<int, GameObject> players = new Dictionary<int, GameObject>();

    private int playerId=-1;

    [Serializable]
    public struct MovePayload
    {
        public int id;
        public float x;
        public float y;
    }

    [Serializable]
    public struct MovePayloadList
    {
        public MovePayload[] payloads;
    }

    [Serializable]
    public struct ChatPayload
    {
        public int id;
        public string msg;
    }

    [Serializable]
    public struct PlayerLeftPayload
    {
        public int id;
    }

    enum MessageType
    {
        MSG_PLAYER_JOIN = 1,
        MSG_PLAYER_LEFT = 2,
        MSG_POS = 3,
        MSG_DIR = 4,    // MSG_ANGLE 로 수정예정
        MSG_CHAT = 5
    };

    #region Unity Lifecycle
    void Start()
    {
        if (TryConnect())
        {
            Thread thread = new Thread(RunReceiver);
            thread.IsBackground = true;
            thread.Start();

            SendPlayerJoin();
            InitUI();
        }
    }

    void Update()
    {
        while (recvQueue.TryDequeue(out var msg))
        {
            ProcessMessage(msg.type, msg.body); // 메인 스레드에서 처리
        }
    }

    void OnApplicationQuit()
    {
        DisposeClient();
    }

    void OnDestroy()
    {
        DisposeClient();
    }
    #endregion


    #region Init UI
    void InitUI()   // [수정 필요] 아직 사용안함
    {
        sendButton.onClick.AddListener(() => {
            if (connected && stream != null && stream.CanWrite)
            {
                string msg = inputField.text;
                SendChat(msg);
            }
        });
    }
    #endregion

    #region External Interface
    
    public void SendPlayerJoin()
    {
        int type = IPAddress.HostToNetworkOrder(((int)MessageType.MSG_PLAYER_JOIN));
        byte[] headerType = BitConverter.GetBytes(type);
        int len = IPAddress.HostToNetworkOrder(0);
        byte[] headerLen = BitConverter.GetBytes(len);

        stream.Write(headerType, 0, headerType.Length);
        stream.Write(headerLen, 0, headerLen.Length);
    }

    public void SendPlayerLeft()
    {
        // 서버에게 자신의 id를 전송
        var payload = playerId;
        byte[] body = Encoding.UTF8.GetBytes(payload.ToString());
        int type = IPAddress.HostToNetworkOrder(((int)MessageType.MSG_PLAYER_LEFT));
        byte[] headerType = BitConverter.GetBytes(type);
        int len = IPAddress.HostToNetworkOrder(body.Length);
        byte[] headerLen = BitConverter.GetBytes(len);

        stream.Write(headerType, 0, headerType.Length);
        stream.Write(headerLen, 0, headerLen.Length);
        stream.Write(body, 0, body.Length);
    }   // [수정 필요] 연결할 곳을 찾아야 함

    public void SendAngle(char moveDir) // [수정 필요]
    {
        if (!connected || stream == null || !stream.CanWrite)
            return;

        var payload = moveDir;
        byte[] body = Encoding.UTF8.GetBytes(payload.ToString());
        int type = IPAddress.HostToNetworkOrder(((int)MessageType.MSG_POS));
        byte[] headerType = BitConverter.GetBytes(type);
        int len = IPAddress.HostToNetworkOrder(body.Length);
        byte[] headerLen = BitConverter.GetBytes(len);

        stream.Write(headerType, 0, headerType.Length);
        stream.Write(headerLen, 0, headerLen.Length);
        stream.Write(body, 0, body.Length);
    } 

    void SendChat(string msg)   
    {
        byte[] body = Encoding.UTF8.GetBytes(msg);  // byte[](바이트배열)로 직렬화 해서 전송해야 함.
        int type = IPAddress.HostToNetworkOrder(((int)MessageType.MSG_CHAT));
        byte[] headerType = BitConverter.GetBytes(type);
        int len = IPAddress.HostToNetworkOrder(body.Length);
        byte[] headerLen = BitConverter.GetBytes(len);

        stream.Write(headerType, 0, headerType.Length);
        stream.Write(headerLen, 0, headerLen.Length);
        stream.Write(body, 0, body.Length);
    }
    #endregion

    #region RecvThread And Logic
    void RunReceiver()  // Thread Main
    {
        // 수신 루프
        try
        {
            while (isRunning)
            {
                if (!ReadMessage(out var type, out var body)) continue;
                recvQueue.Enqueue((type, body));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Receiver 오류] {ex}");
        }
        finally
        {
            DisposeClient();
        }
    }

    bool ReadMessage(out int type, out string body) // [수정 필요] 개선 필요? Context 생성 및 관리
    {
        type = -1;
        body = null;

        try
        {
            byte[] header = new byte[8];
            int total = 0;
            int read;
            // 반복문을 통해 끝까지 받거나, 받았던 내용을 저장해둘 Context가 있어야함.
            // 그렇지 않으면, 8바이트 미만 읽은 경우, 데이터를 저장하지 않고 버린 후 다음 바이트를 같은 형식으로
            // 읽기 때문에, 전송 받은 데이터의 의미를 잃음.
            while (total < 8)
            {
                read = stream.Read(header, total, 8 - total);
                if (read <= 0)
                {
                    Debug.LogWarning($"[ReadMessage] Header read failed. Total read: {total}");
                    return false;
                }
                total += read;
            }


            type = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
            int len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 4));

            if (len < 0 || len > MaxBodyLength) // 너무 큰 값 방지
            {
                Debug.LogError($"[ReadMessage] Invalid body length: {len}");
                return false;
            }

            if (len == 0)
            {
                body = "";
                return true;
            }

            byte[] bodyByte = new byte[len];
            total = 0;

            while (total < len)
            {
                read = stream.Read(bodyByte, total, len - total);
                if (read <= 0)
                {
                    Debug.LogError("body read error or closed");
                    break;
                }
                total += read;
            }
            body = Encoding.UTF8.GetString(bodyByte);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ReadMessage Exception] {ex}");
            return false;
        }
    }

    bool ProcessMessage(int type, string body)
    {
        // 메시지 처리 ...
        switch ((MessageType)type)
        {
            case MessageType.MSG_PLAYER_JOIN:
                HandlePlayerJoin(body);
                break;
            case MessageType.MSG_PLAYER_LEFT:
                HandlePlayerLeft(body);
                break;
            case MessageType.MSG_POS:
                HandlePos(body);              
                break;
            case MessageType.MSG_DIR:
                break;
            case MessageType.MSG_CHAT:
                HandleChat(body);                
                break;
        }
        return true;
    }

    void HandlePos(string body)
    {
        var payload = JsonUtility.FromJson<MovePayload>(body);
        //Debug.Log($"[Type : MSG_MOVE] id:{payload.id}, x:{payload.x}, y:{payload.y}");

        if (players.ContainsKey(payload.id))
        {
            GameObject player = players[payload.id];
            MainThreadDispatcher.Enqueue(() => {
                player.transform.position = new Vector2(payload.x, payload.y);
            });
        }
    }

    void HandleChat(string body)    // [수정 필요] 채팅창 구현
    {
        //{"id":id, "msg":msg}
        var payload = JsonUtility.FromJson<ChatPayload>(body);
        Debug.Log($"[Player_{payload.id}] : {payload.msg}");
    }

    void HandlePlayerJoin(string body)  // [수정 필요] 수정된 서버가 playerId 정보를 보내면 등록
    {
        // 모든 플레이어들의 데이터를 전달받음.
        // id,x,y 값이 든 데이터를 받아 오브젝트를 생성하고 현재 players에 정보 저장.
        // 처음 받는다면, 모든 플레이어들 생성.
        if (body.TrimStart().StartsWith("{\"payloads\":["))
        {
            var payloadList = JsonUtility.FromJson<MovePayloadList>(body);
            foreach (var payload in payloadList.payloads)
            {
                Debug.Log($"[Type : First_MSG_HELL] payload : {payload}");
                MainThreadDispatcher.Enqueue(() => {
                    GameObject otherPlayer = Instantiate(playerPrefab);
                    otherPlayer.name = $"Player_{payload.id}";
                    otherPlayer.transform.position = new Vector2(payload.x, payload.y);
                    players[payload.id] = otherPlayer;
                    Debug.Log($"[Type : First_MSG_HELL] payload.id : {payload.id}, payload.x : {payload.x}, payload.y : {payload.y}, otherPlayer : {otherPlayer}");
                });
            }
        }
        else
        {
            var payload = JsonUtility.FromJson<MovePayload>(body);
            Debug.Log($"[Type : MSG_HELL] id:{payload.id}, x:{payload.x}, y:{payload.y}");

            MainThreadDispatcher.Enqueue(() => {
                GameObject otherPlayer = Instantiate(playerPrefab);
                otherPlayer.name = $"Player_{payload.id}";
                otherPlayer.transform.position = new Vector2(payload.x, payload.y);
                players[payload.id] = otherPlayer;
                Debug.Log($"[Type : MSG_HELL] payload.id : {payload.id}, payload.x : {payload.x}, payload.y : {payload.y}, otherPlayer : {otherPlayer}");
            });
        }
    }

    void HandlePlayerLeft(string body)
    {
        var payload = JsonUtility.FromJson<PlayerLeftPayload>(body);
        if (players.TryGetValue(payload.id, out GameObject player))
        {
            MainThreadDispatcher.Enqueue(() => {
                Destroy(player);
                players.Remove(payload.id);
            });
        }
    }
    #endregion

    #region Internal Utility
    bool TryConnect()
    {
        try
        {
            client = new TcpClient("127.0.0.1", 12345);
            stream = client.GetStream();
            isRunning = true;
            connected = true;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[초기 연결 오류] {ex.Message}");
            return false;
        }
    }

    void DisposeClient()
    {
        isRunning = false; // 루프 종료
        stream?.Close();
        client?.Close();
    }
    #endregion
}
