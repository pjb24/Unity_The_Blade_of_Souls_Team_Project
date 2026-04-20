/// <summary>
/// 동일 효과 중복 요청 충돌 시 처리 정책 식별자입니다.
/// </summary>
public enum E_CameraEffectDuplicatePolicy
{
    Ignore = 0,
    Restart = 1,
    Refresh = 2,
    Stack = 3
}
