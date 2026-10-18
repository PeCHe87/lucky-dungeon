using System.Collections.Generic;

/// <summary>
/// Key ids collected during a run. Owned by <see cref="PlayerRunState"/>.
/// </summary>
public sealed class PlayerRunKeys
{
    readonly HashSet<string> _keys = new HashSet<string>();

    public void BeginRun()
    {
        _keys.Clear();
    }

    public void Clear()
    {
        _keys.Clear();
    }

    public bool HasKey(string keyId)
    {
        if (string.IsNullOrEmpty(keyId))
            return false;
        return _keys.Contains(keyId);
    }

    public bool AddKey(string keyId)
    {
        if (string.IsNullOrEmpty(keyId))
            return false;
        return _keys.Add(keyId);
    }
}
