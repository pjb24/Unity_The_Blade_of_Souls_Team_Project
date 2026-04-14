using UnityEngine;

/// <summary>
/// 플레이어 외형 테마(Y/Z)별 표현 데이터를 보관하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerVisualSet", menuName = "GameFlow/Multiplayer/Player Visual Set")]
public class PlayerVisualSet : ScriptableObject
{
    [Header("Y Variant")]
    [Tooltip("Y 외형 테마에서 사용할 메인 색상입니다.")]
    [SerializeField] private Color _variantYColor = Color.white; // Y 외형 테마 적용 시 SpriteRenderer에 반영할 색상 값입니다.

    [Tooltip("Y 외형 테마에서 사용할 Animator Controller입니다. 비어 있으면 기존 컨트롤러를 유지합니다.")]
    [SerializeField] private RuntimeAnimatorController _variantYAnimatorController; // Y 외형 테마 적용 시 Animator에 반영할 컨트롤러 참조입니다.

    [Header("Z Variant")]
    [Tooltip("Z 외형 테마에서 사용할 메인 색상입니다.")]
    [SerializeField] private Color _variantZColor = new Color(0.7f, 0.85f, 1f, 1f); // Z 외형 테마 적용 시 SpriteRenderer에 반영할 색상 값입니다.

    [Tooltip("Z 외형 테마에서 사용할 Animator Controller입니다. 비어 있으면 기존 컨트롤러를 유지합니다.")]
    [SerializeField] private RuntimeAnimatorController _variantZAnimatorController; // Z 외형 테마 적용 시 Animator에 반영할 컨트롤러 참조입니다.

    /// <summary>
    /// 요청한 외형 타입에 맞는 컬러 값을 반환합니다.
    /// </summary>
    public Color GetColor(E_PlayerVisualVariant visualVariant)
    {
        return visualVariant == E_PlayerVisualVariant.Y ? _variantYColor : _variantZColor;
    }

    /// <summary>
    /// 요청한 외형 타입에 맞는 Animator Controller를 반환합니다.
    /// </summary>
    public RuntimeAnimatorController GetAnimatorController(E_PlayerVisualVariant visualVariant)
    {
        return visualVariant == E_PlayerVisualVariant.Y ? _variantYAnimatorController : _variantZAnimatorController;
    }
}

/// <summary>
/// 플레이어 외형 테마 식별자입니다.
/// </summary>
public enum E_PlayerVisualVariant
{
    Y = 0,
    Z = 1
}
