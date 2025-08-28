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

    // 네트워크와 게임 실행 상태
    TcpClient client;
    NetworkStream stream;
    volatile bool connected = false;    // 보내도 되는가?
    volatile bool isRunning = false;    // 수신해도 되는가?

    // 수신 thread
    Thread thread;
    ConcurrentQueue<(int type, string body)> recvQueue = new();
    private int _closeRequested; // 0/1
    private ReadContext readContext = new ReadContext();

    const int MaxBodyLength = 100000;

    // public Properties
    public bool CanSend => connected && stream != null;
    public bool IsConnected => connected;

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

    public bool SendPlayerJoin()
    {
        DebugUtil.DbgTag("SEQUENCE", "[SendPlayerJoin()]");
        return TrySend(MessageType.MSG_PLAYER_JOIN, null);
    }

    public bool SendPlayerLeft()
    {
        // 서버에게 자신의 id를 전송
        var id = gameManager.GetLocalPlayerId();

        PlayerIdPayload payload = new PlayerIdPayload(id);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        return TrySend(MessageType.MSG_PLAYER_LEFT, body);
    }

    public bool SendAngle(float angle)
    {
        AnglePayload payload = new AnglePayload(angle);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        return TrySend(MessageType.MSG_ANGLE, body);
    }

    bool SendChat(string msg)   
    {
        int id = gameManager.GetLocalPlayerId();

        ChatPayload payload = new ChatPayload(id, msg);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        return TrySend(MessageType.MSG_CHAT, body);
    }

    public bool SendHit(int slitherId, Vector2 hitPos)
    {
        HitPayload payload = new HitPayload(slitherId, hitPos.x, hitPos.y);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        return TrySend(MessageType.MSG_HIT, body);
    }

    public bool SendEat(int foodId, Vector2 hitPos)
    {
        EatPayload payload = new EatPayload(foodId, hitPos.x, hitPos.y);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        return TrySend(MessageType.MSG_EAT, body);
    }

    public bool SendBoostStatus(bool mousePressed)
    {
        BoostStatusPayload payload = new BoostStatusPayload(mousePressed);
        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        return TrySend(MessageType.MSG_BOOST_STATUS, body);
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
            if (Interlocked.Exchange(ref _closeRequested, 1) == 0)
            {
                MainThreadDispatcher.Enqueue(() => DisposeClient());
            }
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
            client.NoDelay = true;
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
    
    // 정상 종료
    public void DisposeClient()
    {
        // 1) 수신 루프 종료 신호 먼저
        isRunning = false;

        // 2) 가능하면 종료 통지 패킷을 안전하게 시도
        try { SendPlayerLeft(); } catch { }

        // 3) 소켓 정상 종료 → Read를 깨워서 finally로 가게 함
        try { client?.Client?.Shutdown(SocketShutdown.Both); } catch { }

        // 4) 스트림/소켓 닫기
        try { stream?.Close(); } catch { }
        try { client?.Close(); } catch { }

        // 5) 수신 스레드 join (자기 자신이면 금지)
        if (thread != null && thread.IsAlive && Thread.CurrentThread != thread)
            thread.Join();

        // 6) 최종 상태
        connected = false;
    }

    // 비정상 종료
    private void MarkClosed()
    {
        if (!connected) return;
        connected = false;
        isRunning = false;
        try { client?.Client?.Shutdown(SocketShutdown.Both); } catch { }
        try { stream?.Close(); } catch { }
        try { client?.Close(); } catch { }
    }

    private bool TrySend(MessageType type, byte[] body)
    {
        if (!CanSend) return false;

        try
        {
            int t = IPAddress.HostToNetworkOrder((int)type);
            int len = IPAddress.HostToNetworkOrder(body?.Length ?? 0);

            // 8바이트 헤더 + 바디를 하나의 버퍼로 구성
            int bodyLen = body?.Length ?? 0;
            byte[] buf = new byte[8 + bodyLen];

            Buffer.BlockCopy(BitConverter.GetBytes(t), 0, buf, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(len), 0, buf, 4, 4);
            if (bodyLen > 0)
                Buffer.BlockCopy(body, 0, buf, 8, bodyLen);

            stream.Write(buf, 0, buf.Length);
            return true;
        }
        catch (IOException)
        {
            MarkClosed();
            return false;
        }
        catch (SocketException)
        {
            MarkClosed();
            return false;
        }
    }
    #endregion
}
