using UnityEngine;

public class BuffAuraController : MonoBehaviour
{
    [SerializeField]
    private E_EffectId _buffAuraId = E_EffectId.BuffAura; // 버프 시작 시 부착할 오라 이펙트 ID

    [SerializeField]
    private Transform _auraAttachTarget; // 오라를 부착할 본/트랜스폼

    private EffectHandle _auraHandle; // 현재 활성 오라 이펙트 핸들

    /// <summary>
    /// 버프 시작 시 Attach 모드 오라를 재생한다.
    /// </summary>
    public void OnBuffStart()
    {
        if (EffectService.Instance == null)
        {
            Debug.LogWarning("[BuffAuraController] EffectService가 없어 버프 오라를 재생하지 못했습니다.", this);
            return;
        }

        Transform attachTarget = _auraAttachTarget == null ? transform : _auraAttachTarget;

        EffectRequest request = new EffectRequest();
        request.EffectId = _buffAuraId;
        request.PlayMode = E_EffectPlayMode.Attach;
        request.AttachTarget = attachTarget;
        request.Owner = gameObject;
        request.LocalOffset = Vector3.zero;
        request.LifetimeOverride = 0f;
        request.AutoReturnOverrideEnabled = false;
        request.AutoReturn = false;
        request.IgnoreDuplicateGuard = false;

        _auraHandle = EffectService.Instance.Play(request);
    }

    /// <summary>
    /// 버프 종료 시 핸들로 오라를 정지한다.
    /// </summary>
    public void OnBuffEnd()
    {
        if (_auraHandle == null)
        {
            return;
        }

        _auraHandle.Stop();
    }
}
