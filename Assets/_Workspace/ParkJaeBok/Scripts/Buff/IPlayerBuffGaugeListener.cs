/// <summary>
/// PlayerBuffGauge 값 변경을 수신하는 리스너 인터페이스입니다.
/// </summary>
public interface IPlayerBuffGaugeListener
{
    /// <summary>
    /// 게이지 현재값/최대값이 변경될 때 호출됩니다.
    /// </summary>
    void OnBuffGaugeChanged(float currentGauge, float maxGauge, float normalizedGauge);
}
