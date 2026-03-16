/// <summary>
/// 피격 처리 결과 수신 인터페이스입니다.
/// </summary>
public interface IHitListener
{
    /// <summary>
    /// 피격 처리 완료 시 호출됩니다.
    /// </summary>
    void OnHitResolved(HitRequest request, HitResult result);
}
