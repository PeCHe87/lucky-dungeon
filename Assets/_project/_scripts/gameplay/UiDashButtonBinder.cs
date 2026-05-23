using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UiDashButtonBinder : MonoBehaviour
{
    [SerializeField] FeneraxJoystickMoveIntentProvider intentProvider;

    void Awake()
    {
        if (intentProvider == null)
            intentProvider = FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();

        GetComponent<Button>().onClick.AddListener(OnDashClicked);
    }

    void OnDestroy()
    {
        Button b = GetComponent<Button>();
        if (b != null)
            b.onClick.RemoveListener(OnDashClicked);
    }

    void OnDashClicked()
    {
        if (intentProvider != null)
            intentProvider.RegisterUiDashFromUi();
    }
}
