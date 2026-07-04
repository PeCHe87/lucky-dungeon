using UnityEngine;

public class DebugCanvasManager : MonoBehaviour
{
	[RuntimeInitializeOnLoadMethod]
	private static void RefreshStatus()
	{
		UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = false;
		Debug.Log("DebugCanvasManager::RefreshStatus");
	}
}
