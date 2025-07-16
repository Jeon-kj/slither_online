using UnityEngine;

public class ReadContext
{
    // 현재 상태: Header 읽는 중인지 Body 읽는 중인지
    public enum ReadState { ReadingHeader, ReadingBody }
    public ReadState State = ReadState.ReadingHeader;

    // Header는 항상 8바이트 고정
    public byte[] HeaderBuffer = new byte[8];
    public int HeaderBytesRead = 0;

    // Body 버퍼는 동적으로 할당
    public byte[] BodyBuffer = null;
    public int BodyBytesRead = 0;
    public int BodyExpectedLength = 0;

    public void InitReadContext()
    {
        State = ReadState.ReadingHeader;
        HeaderBytesRead = 0;
        BodyBuffer = null;
        BodyBytesRead = 0;
        BodyExpectedLength = 0;
    }
}
