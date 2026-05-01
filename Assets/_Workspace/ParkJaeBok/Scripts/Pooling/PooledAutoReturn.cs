using System.Collections;
using UnityEngine;

/// <summary>
/// 설정된 시간이 지나면 LocalObjectPoolManager로 개체 반환을 요청하는 보조 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledAutoReturn : MonoBehaviour
{
    private Coroutine _autoReturnCoroutine; // 현재 실행 중인 자동 반환 Coroutine입니다.

    /// <summary>
    /// 이전 자동 반환 예약을 취소하고 새 반환 예약을 시작합니다.
    /// </summary>
    public void Schedule(LocalObjectPoolManager poolManager, float delaySeconds)
    {
        CancelSchedule();

        if (poolManager == null)
        {
            Debug.LogWarning($"[PooledAutoReturn] PoolManager is null. object={name}", this);
            return;
        }

        _autoReturnCoroutine = StartCoroutine(CoReturnAfterDelay(poolManager, Mathf.Max(0.01f, delaySeconds)));
    }

    /// <summary>
    /// 실행 중인 자동 반환 예약을 취소합니다.
    /// </summary>
    public void CancelSchedule()
    {
        if (_autoReturnCoroutine == null)
        {
            return;
        }

        StopCoroutine(_autoReturnCoroutine);
        _autoReturnCoroutine = null;
    }

    /// <summary>
    /// 비활성화 시 중복 반환 예약이 남지 않도록 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        CancelSchedule();
    }

    /// <summary>
    /// 지정된 시간만큼 기다린 뒤 Pool 반환을 요청합니다.
    /// </summary>
    private IEnumerator CoReturnAfterDelay(LocalObjectPoolManager poolManager, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        _autoReturnCoroutine = null;
        poolManager.Return(gameObject);
    }
}
