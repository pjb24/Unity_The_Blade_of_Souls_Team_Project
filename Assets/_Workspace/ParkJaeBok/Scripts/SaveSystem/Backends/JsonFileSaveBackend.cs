using System;
using System.IO;
using UnityEngine;

/// <summary>
/// persistentDataPath 기반 파일 저장 백엔드입니다.
/// </summary>
public class JsonFileSaveBackend : MonoBehaviour, ISaveBackend
{
    [Tooltip("저장 파일을 둘 하위 폴더 이름입니다.")]
    [SerializeField] private string _saveFolderName = "SaveData"; // 저장 파일 루트 하위 폴더 이름입니다.

    /// <summary>
    /// 저장 루트 경로를 계산합니다.
    /// </summary>
    public string GetSaveRootPath()
    {
        return Path.Combine(Application.persistentDataPath, _saveFolderName);
    }

    /// <summary>
    /// 파일에 JSON을 기록합니다.
    /// </summary>
    public bool TryWrite(string fileName, string json, bool useAtomicReplace, int backupCount)
    {
        try
        {
            string rootPath = GetSaveRootPath();
            Directory.CreateDirectory(rootPath);

            string targetPath = Path.Combine(rootPath, fileName);
            if (useAtomicReplace)
            {
                string tempPath = targetPath + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(targetPath))
                {
                    RotateBackups(targetPath, backupCount);
                    File.Delete(targetPath);
                }

                File.Move(tempPath, targetPath);
            }
            else
            {
                if (File.Exists(targetPath))
                {
                    RotateBackups(targetPath, backupCount);
                }

                File.WriteAllText(targetPath, json);
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[JsonFileSaveBackend] 저장 실패 file={fileName}, message={exception.Message}", this);
            return false;
        }
    }

    /// <summary>
    /// 파일에서 JSON 문자열을 읽어옵니다.
    /// </summary>
    public bool TryRead(string fileName, out string json)
    {
        json = string.Empty;

        try
        {
            string rootPath = GetSaveRootPath();
            string targetPath = Path.Combine(rootPath, fileName);
            if (!File.Exists(targetPath))
            {
                return false;
            }

            json = File.ReadAllText(targetPath);
            return !string.IsNullOrWhiteSpace(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[JsonFileSaveBackend] 로드 실패 file={fileName}, message={exception.Message}", this);
            return false;
        }
    }

    /// <summary>
    /// 타깃 파일과 관련 임시/백업 파일을 삭제합니다.
    /// </summary>
    public bool TryDelete(string fileName)
    {
        try
        {
            string rootPath = GetSaveRootPath();
            string targetPath = Path.Combine(rootPath, fileName);
            DeleteIfExists(targetPath);
            DeleteIfExists(targetPath + ".tmp");

            for (int backupIndex = 1; backupIndex <= 10; backupIndex++)
            {
                DeleteIfExists(targetPath + $".bak{backupIndex}");
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[JsonFileSaveBackend] 삭제 실패 file={fileName}, message={exception.Message}", this);
            return false;
        }
    }

    /// <summary>
    /// 타깃 파일의 롤링 백업을 순차적으로 갱신합니다.
    /// </summary>
    private void RotateBackups(string targetPath, int backupCount)
    {
        int safeBackupCount = Mathf.Max(0, backupCount);
        if (safeBackupCount <= 0)
        {
            return;
        }

        for (int i = safeBackupCount - 1; i >= 1; i--)
        {
            string oldPath = targetPath + $".bak{i}";
            string nextPath = targetPath + $".bak{i + 1}";
            if (File.Exists(oldPath))
            {
                if (File.Exists(nextPath))
                {
                    File.Delete(nextPath);
                }

                File.Move(oldPath, nextPath);
            }
        }

        string firstBackupPath = targetPath + ".bak1";
        if (File.Exists(firstBackupPath))
        {
            File.Delete(firstBackupPath);
        }

        File.Copy(targetPath, firstBackupPath);
    }

    /// <summary>
    /// 파일이 존재할 때만 안전하게 삭제합니다.
    /// </summary>
    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
