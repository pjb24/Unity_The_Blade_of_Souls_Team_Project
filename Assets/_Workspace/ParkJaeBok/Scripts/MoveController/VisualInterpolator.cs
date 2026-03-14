using UnityEngine;

public class VisualInterpolator : MonoBehaviour
{
    // 보간 대상이 되는 물리 바디 참조
    private Rigidbody2D _rb;

    // 직전 물리 프레임의 위치
    private Vector3 _prevPos;
    // 현재 물리 프레임의 위치
    private Vector3 _currPos;

    // 시각적 위치에 추가로 더할 피벗 오프셋
    public Vector3 PivotOffset { get; set; }

    // 시작 시 Rigidbody2D를 연결하고 초기 위치 상태를 설정한다.
    private void Start()
    {
        _rb = GetComponentInParent<Rigidbody2D>();

        transform.parent = null;
        _prevPos = _currPos = _rb.position;
    }

    // 렌더 프레임마다 이전/현재 물리 위치를 보간해 부드러운 시각 위치를 계산한다.
    private void LateUpdate()
    {
        float t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        t = Mathf.Clamp01(t);

        Vector3 newPos = Vector3.Lerp(_prevPos, _currPos, t);

        transform.position = newPos + PivotOffset;
    }

    // 물리 업데이트 시점의 위치를 기록해 다음 렌더 프레임 보간에 사용한다.
    public void UpdatePhysicsState()
    {
        _prevPos = _currPos;
        _currPos = _rb.position;
    }

    // 지정한 위치로 시각/물리 위치를 즉시 동기화한다.
    public void ForceTeleport(Vector3 pos)
    {
        _prevPos = _currPos = pos;
        transform.position = pos;
        _rb.position = pos;
    }
}
