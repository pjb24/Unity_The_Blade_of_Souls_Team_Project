using UnityEngine;

/// <summary>
/// 재생 요청 파라미터를 한 번에 전달하는 구조체.
/// </summary>
public struct EffectRequest
{
    public E_EffectId EffectId; // 재생할 이펙트 ID
    public E_EffectPlayMode PlayMode; // 재생 방식
    public Vector3 Position; // 스폰/추적 시작 기준 월드 좌표
    public Vector3 LocalOffset; // 기준 좌표 또는 타겟 기준 로컬 오프셋
    public Transform FollowTarget; // Follow 시 추적할 대상
    public Transform AttachTarget; // Attach 시 부모로 연결할 대상
    public GameObject Owner; // 소유자 파괴 시 정리에 사용할 오브젝트
    public bool AutoReturnOverrideEnabled; // 자동 반환 여부 오버라이드 사용 플래그
    public bool AutoReturn; // 자동 반환 여부 오버라이드 값
    public float LifetimeOverride; // 생존 시간 오버라이드 값(0 이하이면 정의값 사용)
    public bool IgnoreDuplicateGuard; // 중복 제한 무시 여부

    /// <summary>
    /// EffectId와 위치만으로 OneShot 요청을 간단하게 생성한다.
    /// </summary>
    public static EffectRequest CreateSimple(E_EffectId effectId, Vector3 position)
    {
        EffectRequest request = new EffectRequest();
        request.EffectId = effectId;
        request.PlayMode = E_EffectPlayMode.OneShot;
        request.Position = position;
        request.LocalOffset = Vector3.zero;
        request.FollowTarget = null;
        request.AttachTarget = null;
        request.Owner = null;
        request.AutoReturnOverrideEnabled = false;
        request.AutoReturn = true;
        request.LifetimeOverride = 0f;
        request.IgnoreDuplicateGuard = false;
        return request;
    }
}
