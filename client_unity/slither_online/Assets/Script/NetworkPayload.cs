using System;

namespace Slither.Protocol
{
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

    [Serializable]
    public struct HitPayload
    {
        public int attackerId;
        public float headX;
        public float headY;
        public int targetId;
        public int targetBodyIndex;
        public float targetBodyX;
        public float targetBodyY;
    }

    [Serializable]
    public struct EatPayload
    {
        public int id;
        public float headX;
        public float headY;
        public float foodX;
        public float foodY;
    }
}
