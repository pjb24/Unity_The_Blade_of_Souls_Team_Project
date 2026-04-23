using UnityEngine;

/// <summary>
/// Animator가 붙은 오브젝트에서 발생한 애니메이션 이벤트를 StationaryRangedEnemyController로 중계합니다.
/// </summary>
[DisallowMultipleComponent]
public class StationaryRangedEnemyAnimationEventRelay : MonoBehaviour
{
    [Tooltip("애니메이션 이벤트를 전달받을 StationaryRangedEnemyController입니다.")]
    [SerializeField] private StationaryRangedEnemyController _controller; // 애니메이션 이벤트 수신 대상 컨트롤러입니다.

    /// <summary>
    /// 런타임에 이벤트 수신 대상을 바인딩합니다.
    /// </summary>
    public void BindController(StationaryRangedEnemyController controller)
    {
        _controller = controller;
    }

    /// <summary>
    /// Attack 클립의 Animation Event에서 호출되어 투사체 발사 이벤트를 Controller에 전달합니다.
    /// </summary>
    public void OnFireProjectile()
    {
        if (_controller == null)
        {
            Debug.LogWarning($"[StationaryRangedEnemyAnimationEventRelay] Missing controller on {name}. Could not forward OnFireProjectile.");
            return;
        }

        _controller.OnFireProjectile();
    }
}
