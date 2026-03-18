using UnityEngine;

public class HitEffectEmitter : MonoBehaviour
{
    [SerializeField]
    private E_EffectId _hitEffectId = E_EffectId.HitSmall; // 피격 시 재생할 이펙트 ID

    /// <summary>
    /// 외부 피격 판정 시스템에서 호출하는 재생 예시 메서드.
    /// </summary>
    public void OnHitConfirmed(Vector3 hitPosition)
    {
        if (EffectService.Instance == null)
        {
            Debug.LogWarning("[HitEffectEmitter] EffectService가 없어 피격 이펙트를 재생하지 못했습니다.", this);
            return;
        }

        EffectService.Instance.Play(_hitEffectId, hitPosition);
    }
}
