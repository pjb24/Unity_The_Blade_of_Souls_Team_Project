using UnityEngine;

[DefaultExecutionOrder(-101)]
public class ConveyorBelt : MonoBehaviour, IVelocityInheritable
{
    [Header("Configuration")]
    // 컨베이어 벨트가 로컬 right 방향으로 이동시키는 속도 값이다.
    [SerializeField] private float _moveSpeed = 1f;
    // 탑승 객체가 벨트에서 벗어날 때 수직 발사 보정을 적용할지 여부이다.
    [SerializeField] private bool _launchOnExit = true;
    // 이동 예측 시 벽 충돌 체크를 수행할지 여부이다.
    [SerializeField] private bool _checkForWalls = false;

    [Header("Debug")]
    // 씬 뷰에 이동 방향 기즈모를 표시할지 여부이다.
    [SerializeField] private bool _showGizmos = true;

    // 벨트의 현재 이동 방향과 속도를 기반으로 전달할 속도를 반환한다.
    public Vector2 GetVelocity() => transform.right * _moveSpeed;
    // 벨트 이탈 시 운동량 전달 여부를 저장한다.
    public bool ImpartMomentumOnExit { get; set; } = true;

    // 충돌 프로브를 이동 방향 선행 배치할지 여부를 저장한다.
    public bool ProbesShouldLead { get; set; } = false;
    // 미래 위치 박스캐스트 벽 체크 필요 여부를 반환한다.
    public bool NeedsFuturePositionBoxcastCheck => _checkForWalls;
    // 벨트 이탈 시 수직 발사 보정 적용 여부를 반환한다.
    public bool LaunchVerticallyOnExit => _launchOnExit;

    // 컨베이어 벨트의 이동 방향과 속도를 씬 기즈모 화살표로 시각화한다.
    private void OnDrawGizmos()
    {
        if (_showGizmos)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = GetComponent<Collider2D>() ? GetComponent<Collider2D>().bounds.center : transform.position;
            Vector3 direction = transform.right * _moveSpeed;

            Gizmos.DrawRay(center, direction);

            if (_moveSpeed != 0)
            {
                Vector3 arrowHead = center + direction;
                Vector3 rightWing = Quaternion.Euler(0, 0, 160) * transform.right;
                Vector3 leftWing = Quaternion.Euler(0, 0, -160) * transform.right;
                Gizmos.DrawRay(arrowHead, rightWing * 0.5f);
                Gizmos.DrawRay(arrowHead, leftWing * 0.5f);
            }
        }
    }
}
