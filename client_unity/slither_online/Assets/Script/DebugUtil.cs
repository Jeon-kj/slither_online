public static class DebugUtil
{
    public static bool debugEnabled = true;

    public static void Dbg(params object[] args)
    {
        if (!debugEnabled) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var arg in args)
        {
            sb.Append(arg);
        }

        UnityEngine.Debug.Log(sb.ToString());
    }
}
