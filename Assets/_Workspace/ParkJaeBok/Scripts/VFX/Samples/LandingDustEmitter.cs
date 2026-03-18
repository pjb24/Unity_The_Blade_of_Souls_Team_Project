using UnityEngine;

public class LandingDustEmitter : MonoBehaviour
{
    [SerializeField]
    private E_EffectId _landingDustId = E_EffectId.LandingDust; // 착지 시 사용할 먼지 이펙트 ID

    [SerializeField]
    private Transform _dustSpawnPoint; // 먼지 이펙트를 생성할 기준 위치

    /// <summary>
    /// 이동/점프 시스템에서 착지 확정 시 호출하는 예시 메서드.
    /// </summary>
    public void OnLanded()
    {
        if (EffectService.Instance == null)
        {
            Debug.LogWarning("[LandingDustEmitter] EffectService가 없어 착지 먼지를 재생하지 못했습니다.", this);
            return;
        }

        Vector3 spawnPosition = _dustSpawnPoint == null ? transform.position : _dustSpawnPoint.position;
        EffectService.Instance.Play(_landingDustId, spawnPosition);
    }
}
