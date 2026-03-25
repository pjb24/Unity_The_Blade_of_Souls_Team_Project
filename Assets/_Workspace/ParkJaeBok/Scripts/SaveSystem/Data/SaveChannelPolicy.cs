using UnityEngine;

/// <summary>
/// 저장 채널별 파일/백업 정책을 정의하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "SaveChannelPolicy", menuName = "Game/Save System/Save Channel Policy")]
public class SaveChannelPolicy : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("이 정책이 담당하는 저장 채널 타입입니다.")]
    [SerializeField] private E_SaveChannelType _channelType = E_SaveChannelType.Persistent; // 현재 정책이 적용되는 저장 채널 타입입니다.

    [Tooltip("저장 파일 이름입니다. 확장자(.json)를 포함하는 것을 권장합니다.")]
    [SerializeField] private string _fileName = "save_persistent.json"; // 해당 채널을 저장할 파일 이름입니다.

    [Header("Write Options")]
    [Tooltip("저장 시 임시 파일을 사용한 원자적 교체를 수행할지 여부입니다.")]
    [SerializeField] private bool _useAtomicReplace = true; // 파일 손상을 줄이기 위해 임시 파일을 거쳐 교체할지 여부입니다.

    [Tooltip("유지할 백업 파일 개수입니다.")]
    [SerializeField] private int _backupCount = 2; // 저장 성공 후 보관할 롤링 백업 개수입니다.

    public E_SaveChannelType ChannelType => _channelType;
    public string FileName => _fileName;
    public bool UseAtomicReplace => _useAtomicReplace;
    public int BackupCount => Mathf.Max(0, _backupCount);
}
