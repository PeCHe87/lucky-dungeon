using UnityEngine;

public class FpsService : MonoBehaviour
{
	[SerializeField] private bool isEnabled = false;

	private void OnEnable()
	{
		if (!isEnabled) return;

		QualitySettings.vSyncCount = 0; // Turn off quality setting VSync
		Application.targetFrameRate = 120; // Or screen refresh rate ratio
	}
}
