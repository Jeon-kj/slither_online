using UnityEngine;
using System.Text;
using System.Collections.Generic;

public static class DebugUtil
{
    public static bool debugEnabled = true;
    // MESSAGE, JOIN, NEW_JOIN, SEQUENCE, FOOD, HIT, POSITION, SLITHER
    private static readonly HashSet<string> excludedTags = new HashSet<string>
    {
        "MESSAGE", "JOIN", "SEQUENCE"
    };

    // 일반 디버그 (태그 없이)
    public static void Dbg(params object[] args)
    {
        DbgTag("General", args);
    }

    public static void DbgTag(string tag, params object[] args)
    {
        if (!debugEnabled) return;
        if (excludedTags.Contains(tag)) return;

        StringBuilder sb = new StringBuilder();
        foreach (var arg in args)
        {
            sb.Append(arg != null ? arg.ToString() : "null");
        }

        string message = $"[{tag}] {sb}";
        Debug.Log(message);
    }
}
