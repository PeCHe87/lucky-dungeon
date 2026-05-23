using System;
using System.Collections.Generic;
using UnityEngine;

public enum FSMParamType
{
    Float = 0,
    Int = 1,
    Bool = 2,
    String = 3,
    Vector3 = 4,
    Object = 5,
}

[Serializable]
public class FSMParam
{
    public string key;
    public FSMParamType type;

    public float floatValue;
    public int intValue;
    public bool boolValue;
    public string stringValue;
    public Vector3 vector3Value;
    public UnityEngine.Object objectValue;
}

public static class FSMParamUtils
{
    public static bool TryGet(IReadOnlyList<FSMParam> list, string key, out FSMParam param)
    {
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p != null && p.key == key)
                {
                    param = p;
                    return true;
                }
            }
        }

        param = null;
        return false;
    }

    public static float GetFloat(IReadOnlyList<FSMParam> list, string key, float defaultValue = 0f)
        => TryGet(list, key, out var p) && p.type == FSMParamType.Float ? p.floatValue : defaultValue;

    public static int GetInt(IReadOnlyList<FSMParam> list, string key, int defaultValue = 0)
        => TryGet(list, key, out var p) && p.type == FSMParamType.Int ? p.intValue : defaultValue;

    public static bool GetBool(IReadOnlyList<FSMParam> list, string key, bool defaultValue = false)
        => TryGet(list, key, out var p) && p.type == FSMParamType.Bool ? p.boolValue : defaultValue;

    public static string GetString(IReadOnlyList<FSMParam> list, string key, string defaultValue = "")
        => TryGet(list, key, out var p) && p.type == FSMParamType.String ? (p.stringValue ?? defaultValue) : defaultValue;

    public static Vector3 GetVector3(IReadOnlyList<FSMParam> list, string key, Vector3 defaultValue)
        => TryGet(list, key, out var p) && p.type == FSMParamType.Vector3 ? p.vector3Value : defaultValue;

    public static T GetObject<T>(IReadOnlyList<FSMParam> list, string key) where T : UnityEngine.Object
        => TryGet(list, key, out var p) && p.type == FSMParamType.Object ? (p.objectValue as T) : null;
}
