using UnityEngine;

/// <summary>
/// Animation Event를 통해 전달된 마커를 ActionController로 전달하는 브리지입니다.
/// </summary>
public class ActionMarkerReceiver : MonoBehaviour
{
    [SerializeField] private ActionController _actionController; // 마커를 전달할 액션 컨트롤러

    /// <summary>
    /// Animation Event에서 Object 마커를 수신해 컨트롤러에 전달합니다.
    /// </summary>
    public void ReceiveMarker(Object markerObject)
    {
        if (!TryResolveActionControllerReference())
        {
            Debug.LogWarning("[ActionMarkerReceiver] ActionController is not assigned and fallback resolve failed.");
            return;
        }

        _actionController.ReceiveMarker(markerObject);
    }

    /// <summary>
    /// ActionController 참조가 비어있을 때 동일 오브젝트에서 fallback으로 보정합니다.
    /// </summary>
    private bool TryResolveActionControllerReference()
    {
        if (_actionController != null)
        {
            return true;
        }

        _actionController = GetComponent<ActionController>();
        if (_actionController != null)
        {
            Debug.LogWarning($"[ActionMarkerReceiver] Fallback resolved ActionController from same GameObject on {name}.");
            return true;
        }

        return _actionController != null;
    }
}
