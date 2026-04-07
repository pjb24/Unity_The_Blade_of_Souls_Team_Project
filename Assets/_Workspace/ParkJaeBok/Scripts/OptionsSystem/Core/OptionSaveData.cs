using System;

/// <summary>
/// 옵션 저장 파일 루트 데이터입니다.
/// </summary>
[Serializable]
public class OptionSaveData
{
    public int SchemaVersion; // 옵션 저장 스키마 버전입니다.
    public DisplayOptionsData Display; // Display 탭 저장 데이터입니다.
    public AudioOptionsData Audio; // Audio 탭 저장 데이터입니다.
    public InputOptionsData Input; // Input 탭 저장 데이터입니다.
    public AccessibilityOptionsData Accessibility; // Accessibility 탭 저장 데이터입니다.
    public GameplayOptionsData Gameplay; // Gameplay 탭 저장 데이터입니다.
}
