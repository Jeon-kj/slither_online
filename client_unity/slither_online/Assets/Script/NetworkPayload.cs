using System;

namespace Slither.Protocol
{
    [Serializable]
    public struct PlayerJoinPayload
    {
        public int id;
        public float x;
        public float y;
        public int length;
    }

    [Serializable]
    public struct PlayerJoinPayloadList
    {
        public int id;
        public PlayerJoinPayload[] slitherPayloads;
        public FoodSpawnPayload[] foodPayloads;
    }

    [Serializable]
    public struct MovePayload
    {
        public int id;
        public float x;
        public float y;
    }

    [Serializable]
    public struct PlayerIdPayload
    {
        public int id;
        public PlayerIdPayload(int id)
        {
            this.id = id;
        }
    }

    [Serializable]
    public struct AnglePayload
    {
        public float angle;
        public AnglePayload(float angle)
        {
            this.angle = angle;
        }
    }

    [Serializable]
    public struct ChatPayload
    {
        public int id;
        public string msg;

        public ChatPayload(int id, string msg)
        {
            this .id = id;
            this .msg = msg;
        }
    }

    [Serializable]
    public struct PlayerLeftPayload
    {
        public int id;
    }

    [Serializable]
    public struct HitPayload
    {
        public int bodySlitherId;
        public float reportedX;
        public float reportedY;

        public HitPayload(int id, float x, float y)
        {
            this.bodySlitherId = id;
            this.reportedX = x;
            this.reportedY = y;
        }
    }

    [Serializable]
    public struct EatPayload
    {
        public int foodId;
        public float reportedX;
        public float reportedY;

        public EatPayload(int id, float x, float y)
        {
            this.foodId = id;
            this.reportedX = x;
            this.reportedY = y;
        }
    }

    [Serializable]
    public struct  FoodSpawnPayload
    {
        public int id;
        public float x;
        public float y;
    }

    [Serializable]
    public struct FoodSpawnPayloadList
    {
        public FoodSpawnPayload[] payloads;
    }

    [Serializable]
    public struct FoodRemovePayload
    {
        public int id;
    }

    [Serializable]
    public struct SlitherLengthUpdatePayload
    {
        public int id;
        public int length;
        public int delta;
    }
}
