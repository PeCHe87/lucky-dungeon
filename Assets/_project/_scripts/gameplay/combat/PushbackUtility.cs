using UnityEngine;

/// <summary>Finds <see cref="PushbackReceiver"/> on a hit hierarchy and applies pushback.</summary>
public static class PushbackUtility
{
    public static bool TryApplyOnHierarchy(GameObject hitObject, in PushbackContext ctx)
    {
        if (hitObject == null || ctx.distance <= 0f || ctx.duration <= 0f)
            return false;

        Transform tr = hitObject.transform;
        while (tr != null)
        {
            PushbackReceiver receiver = tr.GetComponent<PushbackReceiver>();
            if (receiver != null)
            {
                receiver.ApplyPushback(in ctx);
                return true;
            }

            tr = tr.parent;
        }

        return false;
    }
}
