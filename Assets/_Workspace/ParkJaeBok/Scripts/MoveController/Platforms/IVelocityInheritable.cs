using UnityEngine;

public interface IVelocityInheritable
{
    // 플랫폼/오브젝트가 현재 프레임에 제공하는 속도를 반환한다.
    Vector2 GetVelocity();

    // 캐릭터의 충돌 프로브를 이동 방향 앞쪽으로 배치할지 여부를 설정한다.
    bool ProbesShouldLead { get; set; }
    // 플랫폼에서 이탈할 때 현재 이동량 기반의 관성을 전달할지 여부를 설정한다.
    bool ImpartMomentumOnExit { get; set; }
    // 플랫폼에서 이탈할 때 수직 방향으로 발사형 보정을 적용할지 여부를 반환한다.
    bool LaunchVerticallyOnExit { get; }
    // 미래 위치 기준 박스캐스트 벽 체크가 필요한지 여부를 반환한다.
    bool NeedsFuturePositionBoxcastCheck { get; }
}
