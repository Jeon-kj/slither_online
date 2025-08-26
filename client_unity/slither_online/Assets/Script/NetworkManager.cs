using Slither.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEngine.GraphicsBuffer;

public class NetworkManager : MonoBehaviour
{
    enum ReadResult
    {
        NeedMore,
        GotMessage,
        Closed,          // 정상 종료 (FIN)
        Error            // 예외 종료 (IOException, RST 등)
    }


    public GameManager gameManager;
    public SlitherMove slitherMove;
    public MessageHandler messageHandler;
    public SlitherManager slitherManager;

    TcpClient client;
    NetworkStream stream;

    Thread thread;
    //public InputField inputField;
    //public Button sendButton;

    volatile bool connected = false;
    volatile bool isRunning = false;
    const int MaxBodyLength = 100000;

    ConcurrentQueue<(int type, string body)> recvQueue = new();
    
    private ReadContext readContext = new ReadContext();

    #region Unity Lifecycle
    void Start()
    {
        if (TryConnect())
        {
            DebugUtil.DbgTag("SEQUENCE", "[Start] tryConnect()");

            thread = new Thread(RunReceiver);
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
            messageHandler.ProcessMessage(msg.type, msg.body); // 메인 스레드에서 처리
        }
    }

    void OnApplicationQuit()
    {
        gameManager.DisposeGame();
    }

    void OnDestroy()
    {
        gameManager.DisposeGame();
    }

    private void OnDisable()
    {
        gameManager.DisposeGame();
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
        DebugUtil.DbgTag("SEQUENCE", "[SendPlayerJoin()]");
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
        var id = gameManager.GetLocalPlayerId();

        PlayerIdPayload payload = new PlayerIdPayload(id);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

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

        AnglePayload payload = new AnglePayload(angle);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

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
        int id = gameManager.GetLocalPlayerId();

        ChatPayload payload = new ChatPayload(id, msg);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        int type = IPAddress.HostToNetworkOrder(((int)MessageType.MSG_CHAT));
        byte[] headerType = BitConverter.GetBytes(type);
        int len = IPAddress.HostToNetworkOrder(body.Length);
        byte[] headerLen = BitConverter.GetBytes(len);

        stream.Write(headerType, 0, headerType.Length);
        stream.Write(headerLen, 0, headerLen.Length);
        stream.Write(body, 0, body.Length);
    }

    public void SendHit(int slitherId, Vector2 hitPos)
    {
        HitPayload payload = new HitPayload(slitherId, hitPos.x, hitPos.y);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        int type = IPAddress.HostToNetworkOrder(((int)MessageType.MSG_HIT));
        byte[] headerType = BitConverter.GetBytes(type);
        int len = IPAddress.HostToNetworkOrder(body.Length);
        byte[] headerLen = BitConverter.GetBytes(len);

        stream.Write(headerType, 0, headerType.Length);
        stream.Write(headerLen, 0, headerLen.Length);
        stream.Write(body, 0, body.Length);
    }

    public void SendEat(int foodId, Vector2 hitPos)
    {
        EatPayload payload = new EatPayload(foodId, hitPos.x, hitPos.y);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        int type = IPAddress.HostToNetworkOrder(((int)MessageType.MSG_EAT));
        byte[] headerType = BitConverter.GetBytes(type);
        int len = IPAddress.HostToNetworkOrder(body.Length);
        byte[] headerLen = BitConverter.GetBytes(len);

        stream.Write(headerType, 0, headerType.Length);
        stream.Write(headerLen, 0, headerLen.Length);
        stream.Write(body, 0, body.Length);
    }

    public void SendBoostStatus(bool mousePressed)
    {
        BoostStatusPayload payload = new BoostStatusPayload(mousePressed);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        int type = IPAddress.HostToNetworkOrder(((int)MessageType.MSG_BOOST_STATUS));
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
                ReadResult result = ReadMessage(out var type, out var body);
                switch (result)
                {
                    case ReadResult.GotMessage:
                        recvQueue.Enqueue((type, body));
                        break;
                    case ReadResult.Closed:
                        Debug.Log("연결 정상 종료");
                        return;
                    case ReadResult.Error:
                        Debug.LogWarning("네트워크 오류로 연결 끊김");
                        return;
                }

            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Receiver 오류] {ex}");
        }
        finally
        {
            MainThreadDispatcher.Enqueue(() => gameManager.DisposeGame());
        }
    }

    ReadResult ReadMessage(out int type, out string body)
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
                    if (read < 0)  return ReadResult.Error;
                    if (read == 0) return ReadResult.Closed;
                    readContext.HeaderBytesRead += read;
                }

                int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(readContext.HeaderBuffer, 4));

                if (length < 0 || length > MaxBodyLength) // 너무 큰 값 방지
                {
                    Debug.LogError($"[ReadMessage] Invalid body length: {length}");
                    return ReadResult.Error;
                }

                if (length == 0)
                {
                    body = "";
                    type = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(readContext.HeaderBuffer, 0));
                    readContext.InitReadContext();
                    return ReadResult.GotMessage;
                }

                // BodyBuffer는 이 타이밍에 단 한 번 할당
                readContext.BodyExpectedLength = length;
                readContext.BodyBuffer = new byte[length];

                // 상태를 Body 읽기로 전환
                readContext.State = ReadContext.ReadState.ReadingBody;
                return ReadResult.NeedMore;
            }            
            else
            {            
                while (readContext.BodyBytesRead < readContext.BodyExpectedLength)
                {
                    read = stream.Read(readContext.BodyBuffer, readContext.BodyBytesRead, readContext.BodyExpectedLength - readContext.BodyBytesRead);
                    if (read < 0) return ReadResult.Error;
                    if (read == 0) return ReadResult.Closed;
                    readContext.BodyBytesRead += read;
                }

                type = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(readContext.HeaderBuffer, 0));
                body = Encoding.UTF8.GetString(readContext.BodyBuffer);
                readContext.InitReadContext();
                return ReadResult.GotMessage;
            }
        }
        catch (IOException) { return ReadResult.Closed; }
        catch (SocketException) { return ReadResult.Closed; }
        catch (Exception ex) { Debug.LogError($"[ReadMessage Exception] {ex}"); return ReadResult.Closed; }
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

    public void DisposeClient()
    {
        isRunning = false; // 루프 종료        
        stream?.Close();
        client?.Close();
        thread?.Join();
    }
    #endregion
}
