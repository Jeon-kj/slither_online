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
using UnityEngine.UIElements;

public class NetworkManager : MonoBehaviour
{
    TcpClient client;
    NetworkStream stream;

    //public InputField inputField;
    //public Button sendButton;

    volatile bool connected = false;
    volatile bool isRunning = false;
    const int MaxBodyLength = 10000;

    public GameObject playerPrefab; // 유니티에서 프리팹으로 연결
    public GameObject bodyPrefab;

    ConcurrentQueue<(int type, string body)> recvQueue = new();
    Dictionary<int, SlitherContext> players = new Dictionary<int, SlitherContext>();
    private ReadContext readContext = new ReadContext();

    private int playerId=-1;

    [Serializable]
    public struct MovePayload
    {
        public int id;
        public float x;
        public float y;
        public int length;
    }

    [Serializable]
    public struct MovePayloadList
    {
        public int id;
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
        MSG_ANGLE = 4,
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
            //InitUI();
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
    /*void InitUI()   // [수정 필요] 아직 사용안함
    {
        sendButton.onClick.AddListener(() => {
            if (connected && stream != null && stream.CanWrite)
            {
                string msg = inputField.text;
                SendChat(msg);
            }
        });
    }*/
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

    public void SendAngle(float angle)
    {
        if (!connected || stream == null || !stream.CanWrite)
            return;

        var payload = angle;
        byte[] body = Encoding.UTF8.GetBytes(payload.ToString(System.Globalization.CultureInfo.InvariantCulture));
        int type = IPAddress.HostToNetworkOrder(((int)MessageType.MSG_ANGLE));
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
                if (ReadMessage(out var type, out var body))
                {
                    recvQueue.Enqueue((type, body));
                }
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

    bool ReadMessage(out int type, out string body)
    {
        type = -1;
        body = null;

        try
        {
            int read;
            if (readContext.State == ReadContext.ReadState.ReadingHeader)
            {                
                while (readContext.HeaderBytesRead < 8)
                {
                    read = stream.Read(readContext.HeaderBuffer, readContext.HeaderBytesRead, 8 - readContext.HeaderBytesRead);
                    if (read <= 0)
                    {
                        Debug.LogWarning($"[ReadMessage] Header read failed. Total read: {readContext.HeaderBytesRead}");
                        return false;
                    }
                    readContext.HeaderBytesRead += read;
                }

                type = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(readContext.HeaderBuffer, 0));
                int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(readContext.HeaderBuffer, 4));

                if (length < 0 || length > MaxBodyLength) // 너무 큰 값 방지
                {
                    Debug.LogError($"[ReadMessage] Invalid body length: {length}");
                    return false;
                }

                if (length == 0)
                {
                    body = "";
                    readContext.InitReadContext();
                    return true;
                }

                // BodyBuffer는 이 타이밍에 단 한 번 할당
                readContext.BodyExpectedLength = length;
                readContext.BodyBuffer = new byte[length];

                // 상태를 Body 읽기로 전환
                readContext.State = ReadContext.ReadState.ReadingBody;
                return false;
            }            
            else
            {            
                while (readContext.BodyBytesRead < readContext.BodyExpectedLength)
                {
                    read = stream.Read(readContext.BodyBuffer, readContext.BodyBytesRead, readContext.BodyExpectedLength - readContext.BodyBytesRead);
                    if (read <= 0)
                    {
                        Debug.LogError("body read error or closed");
                        return false;
                    }
                    readContext.BodyBytesRead += read;
                }

                body = Encoding.UTF8.GetString(readContext.BodyBuffer);
                readContext.InitReadContext();
                return true;
            }
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
            case MessageType.MSG_ANGLE:
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
        //Debug.Log($"[Type : MSG_POS] id:{payload.id}, x:{payload.x}, y:{payload.y}, length:{payload.length}");

        if (players.ContainsKey(payload.id))
        {
            SlitherContext sCtx = players[payload.id];
            sCtx.bodyLen = payload.length;
            MainThreadDispatcher.Enqueue(() => {
                SyncSlither(payload, sCtx);
            });
        }
    }

    void HandleChat(string body)    // [수정 필요] 채팅창 구현
    {
        //{"id":id, "msg":msg}
        var payload = JsonUtility.FromJson<ChatPayload>(body);
        Debug.Log($"[Player_{payload.id}] : {payload.msg}");
    }

    void HandlePlayerJoin(string body) 
    {
        // 모든 플레이어들의 데이터를 전달받음.
        // id,x,y 값이 든 데이터를 받아 오브젝트를 생성하고 현재 players에 정보 저장.
        // 처음 받는다면, 모든 플레이어들 생성.

        // 새로 입장하여 모든 플레이어에 대한 정보를 받는 경우
        // 이 경우는 slither의 body segments는 어떻게 업데이트 하지?
        // 왜냐하면, 서버에서는 head에 대한 정보밖에 주지 않으니까.
        // 초기에는 그냥 머리에 대한 정보만 줘서 시간이 지날수록 업데이트 되도록 함
        if (body.TrimStart().StartsWith("{\"id\""))
        {
            var payloadList = JsonUtility.FromJson<MovePayloadList>(body);
            playerId = payloadList.id;

            foreach (var payload in payloadList.payloads)
            {
                Debug.Log($"[Type : First_MSG_HELL] payload : {payload}");
                MainThreadDispatcher.Enqueue(() => {
                    InitSlither(payload);
                });
            }
        }
        // 새로운 플레이어에 대한 정보를 받는 경우
        else
        {
            var payload = JsonUtility.FromJson<MovePayload>(body);
            Debug.Log($"[Type : MSG_HELL] id:{payload.id}, x:{payload.x}, y:{payload.y}");

            MainThreadDispatcher.Enqueue(() => {
                InitSlither(payload);
            });
        }
    }

    void HandlePlayerLeft(string body)
    {
        var payload = JsonUtility.FromJson<PlayerLeftPayload>(body);
        if (players.TryGetValue(payload.id, out SlitherContext sCtx))
        {
            MainThreadDispatcher.Enqueue(() => {
                DelSCtx(payload.id, sCtx);
                
            });
        }
    }

    void InitSlither(MovePayload payload)
    {
        GameObject otherPlayer = Instantiate(playerPrefab);
        otherPlayer.name = $"Player_{payload.id}";
        otherPlayer.transform.position = new Vector2(payload.x, payload.y);
        players[payload.id].headObject = otherPlayer;
        Debug.Log($"[Type : MSG_PLAYER_JOIN] payload.id : {payload.id}, payload.x : {payload.x}, payload.y : {payload.y}, otherPlayer : {otherPlayer}");
    }

    void SyncSlither(MovePayload payload, SlitherContext sCtx)
    {
        // 길이가 늘었을 경우
        while (sCtx.bodySegments.Count < sCtx.bodyLen)
        {
            var segment = Instantiate(bodyPrefab);
            Vector2 pos;
            if (sCtx.bodySegments.Count > 0)
                pos = sCtx.bodySegments[sCtx.bodySegments.Count - 1].transform.position;
            else pos = sCtx.headObject.transform.position;

            segment.transform.position = pos;
            sCtx.bodySegments.Add(segment);
        }
        // 길이가 줄었을 경우
        while (sCtx.bodySegments.Count > sCtx.bodyLen)
        {
            var lastSegment = sCtx.bodySegments[sCtx.bodySegments.Count - 1];
            GameObject.Destroy(lastSegment);
            sCtx.bodySegments.RemoveAt(sCtx.bodySegments.Count - 1);
        }
        // 꼬리(body) 위치 갱신
        for (int i = sCtx.bodySegments.Count - 1; i > 0; i--)
        {
            sCtx.bodySegments[i].transform.position = sCtx.bodySegments[i - 1].transform.position;
        }
        if (sCtx.bodySegments.Count > 0)
        {
            sCtx.bodySegments[0].transform.position = sCtx.headObject.transform.position;
        }
        // head 위치 갱신
        sCtx.headObject.transform.position = new Vector2(payload.x, payload.y);
    }

    void DelSCtx(int id, SlitherContext sCtx)
    {
        Destroy(sCtx.headObject);
        foreach (var segment in sCtx.bodySegments)
        {
            Destroy(segment);
        }
        players.Remove(id);
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
