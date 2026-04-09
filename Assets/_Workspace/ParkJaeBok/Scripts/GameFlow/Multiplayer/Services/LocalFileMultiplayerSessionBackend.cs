using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

/// <summary>
/// 동일 PC의 서로 다른 실행 인스턴스 간 세션 정보를 파일로 공유하는 로컬 백엔드 구현입니다.
/// 운영 환경에서는 UGS Relay 기반 백엔드로 교체해야 합니다.
/// </summary>
public class LocalFileMultiplayerSessionBackend : MonoBehaviour, IMultiplayerSessionBackend
{
    [Serializable]
    private sealed class SessionStore
    {
        public List<SessionRecord> Sessions = new List<SessionRecord>(); // 파일에 저장할 세션 목록입니다.
    }

    [Serializable]
    private sealed class SessionRecord
    {
        public string JoinCode; // 세션 조회에 사용할 Join Code입니다.
        public string HostClientId; // 세션을 생성한 Host 식별자입니다.
        public int MaxPlayerCount; // 세션 최대 인원 수입니다.
        public bool IsStageInProgress; // 중도 Join 제한을 위한 Stage 진행 상태입니다.
        public List<string> ClientIds = new List<string>(); // 현재 세션 참가자 목록입니다.
        public List<string> ReadyClientIds = new List<string>(); // 준비 완료를 보고한 Client 목록입니다.
    }

    [Tooltip("세션 정보를 저장할 로컬 파일 이름입니다. Application.persistentDataPath 하위에 생성됩니다.")]
    [SerializeField] private string _storageFileName = "multiplayer_sessions_local.json"; // 로컬 세션 저장 파일명입니다.

    [Tooltip("파일 저장소 접근 시 뮤텍스 대기 제한 시간(밀리초)입니다.")]
    [SerializeField] private int _mutexWaitMilliseconds = 2000; // 프로세스 간 파일 접근 잠금 대기 시간입니다.

    [Tooltip("디버그 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // 파일 백엔드 동작 로그 출력 여부입니다.

    private string StoragePath => Path.Combine(Application.persistentDataPath, _storageFileName); // 로컬 세션 파일 절대 경로입니다.

    /// <summary>
    /// Host 세션을 생성하고 Join Code를 발급합니다.
    /// </summary>
    public bool TryCreateSession(string hostClientId, int maxPlayerCount, out string joinCode, out string reason)
    {
        joinCode = string.Empty;
        reason = string.Empty;
        string createdJoinCode = string.Empty; // 생성 성공 시 호출자에게 전달할 Join Code 임시 버퍼입니다.
        string storageReason = string.Empty; // 저장소 접근 실패 사유를 보관할 임시 버퍼입니다.

        bool succeeded = AccessStore(
            store =>
            {
                string generatedJoinCode = GenerateUniqueJoinCode(store); // 새 세션에서 사용할 고유 Join Code입니다.
                SessionRecord session = new SessionRecord
                {
                    JoinCode = generatedJoinCode,
                    HostClientId = hostClientId,
                    MaxPlayerCount = Mathf.Max(1, maxPlayerCount),
                    IsStageInProgress = false
                };

                session.ClientIds.Add(hostClientId);
                store.Sessions.Add(session);
                createdJoinCode = generatedJoinCode;
                return true;
            },
            out storageReason,
            true);

        reason = storageReason;
        if (succeeded)
        {
            joinCode = createdJoinCode;
        }

        return succeeded;
    }

    /// <summary>
    /// Join Code 기반 참가를 시도합니다.
    /// </summary>
    public bool TryJoinSession(string joinCode, string clientId, out string reason)
    {
        string failureReason = string.Empty; // Join 검증 실패 사유를 담을 임시 버퍼입니다.
        string storageReason = string.Empty; // 저장소 접근 실패 사유를 담을 임시 버퍼입니다.
        bool succeeded = AccessStore(
            store =>
            {
                if (!TryFindSession(store, joinCode, out SessionRecord session))
                {
                    failureReason = "SessionNotFound";
                    return false;
                }

                if (session.IsStageInProgress)
                {
                    failureReason = "StageInProgress";
                    return false;
                }

                if (session.ClientIds.Count >= session.MaxPlayerCount)
                {
                    failureReason = "SessionFull";
                    return false;
                }

                if (!session.ClientIds.Contains(clientId))
                {
                    session.ClientIds.Add(clientId);
                }

                return true;
            },
            out storageReason,
            true);

        reason = string.IsNullOrWhiteSpace(failureReason) ? storageReason : failureReason;
        return succeeded;
    }

    /// <summary>
    /// 세션의 Stage 진행 상태를 갱신합니다.
    /// </summary>
    public void SetStageInProgress(string joinCode, bool isInProgress)
    {
        string reason = string.Empty;
        AccessStore(
            store =>
            {
                if (!TryFindSession(store, joinCode, out SessionRecord session))
                {
                    return false;
                }

                session.IsStageInProgress = isInProgress;
                return true;
            },
            out reason,
            true);
    }

    /// <summary>
    /// 세션의 현재 참가 인원 수를 조회합니다.
    /// </summary>
    public bool TryGetPlayerCount(string joinCode, out int playerCount)
    {
        int resolvedPlayerCount = 0; // 조회 성공 시 반환할 세션 인원 수 임시 버퍼입니다.
        string reason = string.Empty; // 저장소 접근 실패 사유 임시 버퍼입니다.

        bool succeeded = AccessStore(
            store =>
            {
                if (!TryFindSession(store, joinCode, out SessionRecord session))
                {
                    return false;
                }

                resolvedPlayerCount = session.ClientIds.Count;
                return true;
            },
            out reason,
            false);

        playerCount = succeeded ? resolvedPlayerCount : 0;
        return succeeded;
    }

    /// <summary>
    /// Client가 로드 완료/준비 완료 상태임을 세션에 기록합니다.
    /// </summary>
    public bool TryMarkClientReady(string joinCode, string clientId, out string reason)
    {
        string failureReason = string.Empty; // 준비 완료 보고 실패 사유를 담을 임시 버퍼입니다.
        string storageReason = string.Empty; // 저장소 접근 실패 사유를 담을 임시 버퍼입니다.
        bool succeeded = AccessStore(
            store =>
            {
                if (!TryFindSession(store, joinCode, out SessionRecord session))
                {
                    failureReason = "SessionNotFound";
                    return false;
                }

                if (!session.ClientIds.Contains(clientId))
                {
                    failureReason = "ClientNotInSession";
                    return false;
                }

                if (string.Equals(clientId, session.HostClientId, StringComparison.Ordinal))
                {
                    failureReason = "HostDoesNotNeedReady";
                    return false;
                }

                if (!session.ReadyClientIds.Contains(clientId))
                {
                    session.ReadyClientIds.Add(clientId);
                }

                return true;
            },
            out storageReason,
            true);

        reason = string.IsNullOrWhiteSpace(failureReason) ? storageReason : failureReason;
        return succeeded;
    }

    /// <summary>
    /// Host가 Stage 진입을 시작해도 되는 세션 준비 완료 상태인지 조회합니다.
    /// </summary>
    public bool TryGetStageEntryReady(string joinCode, out bool isReady, out string reason)
    {
        bool resolvedIsReady = false; // 조회 성공 시 반환할 준비 완료 판정 결과 임시 버퍼입니다.
        string failureReason = string.Empty; // 도메인 판정 실패 사유 임시 버퍼입니다.
        string storageReason = string.Empty; // 저장소 접근 실패 사유 임시 버퍼입니다.

        bool succeeded = AccessStore(
            store =>
            {
                if (!TryFindSession(store, joinCode, out SessionRecord session))
                {
                    failureReason = "SessionNotFound";
                    return false;
                }

                if (session.ClientIds.Count < session.MaxPlayerCount)
                {
                    failureReason = "PlayersNotFull";
                    resolvedIsReady = false;
                    return true;
                }

                int requiredReadyClientCount = Mathf.Max(0, session.ClientIds.Count - 1); // Host를 제외하고 준비 완료가 필요한 인원 수입니다.
                resolvedIsReady = session.ReadyClientIds.Count >= requiredReadyClientCount;
                failureReason = resolvedIsReady ? string.Empty : "ClientNotReady";
                return true;
            },
            out storageReason,
            false);

        isReady = succeeded && resolvedIsReady;
        reason = string.IsNullOrWhiteSpace(failureReason) ? storageReason : failureReason;
        return succeeded;
    }

    /// <summary>
    /// 세션을 종료합니다.
    /// </summary>
    public void CloseSession(string joinCode)
    {
        string reason = string.Empty;
        AccessStore(
            store =>
            {
                int removedCount = store.Sessions.RemoveAll(session => string.Equals(session.JoinCode, joinCode, StringComparison.Ordinal)); // 종료할 세션 제거 수입니다.
                return removedCount > 0;
            },
            out reason,
            true);
    }

    /// <summary>
    /// 저장소를 로드하고 콜백을 실행한 뒤 필요 시 저장하는 공통 접근 헬퍼입니다.
    /// </summary>
    private bool AccessStore(Func<SessionStore, bool> action, out string reason, bool saveWhenActionSucceeded)
    {
        reason = string.Empty;

        using Mutex mutex = BuildProcessMutex();
        bool lockAcquired = false;

        try
        {
            lockAcquired = mutex.WaitOne(Mathf.Max(1, _mutexWaitMilliseconds));
            if (!lockAcquired)
            {
                reason = "StorageLockTimeout";
                return false;
            }

            SessionStore store = LoadStore(); // 파일에서 불러온 세션 저장소 스냅샷입니다.
            bool succeeded = action(store);
            if (succeeded && saveWhenActionSucceeded)
            {
                SaveStore(store);
            }

            return succeeded;
        }
        catch (Exception exception)
        {
            reason = $"StorageException:{exception.GetType().Name}";
            if (_verboseLogging)
            {
                Debug.LogWarning($"[LocalFileMultiplayerSessionBackend] 저장소 접근 실패 reason={reason}, message={exception.Message}", this);
            }

            return false;
        }
        finally
        {
            if (lockAcquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    /// <summary>
    /// Join Code로 세션 레코드를 조회합니다.
    /// </summary>
    private static bool TryFindSession(SessionStore store, string joinCode, out SessionRecord session)
    {
        for (int i = 0; i < store.Sessions.Count; i++)
        {
            SessionRecord candidate = store.Sessions[i]; // 현재 조회 중인 세션 후보 레코드입니다.
            if (string.Equals(candidate.JoinCode, joinCode, StringComparison.Ordinal))
            {
                session = candidate;
                return true;
            }
        }

        session = null;
        return false;
    }

    /// <summary>
    /// 파일에서 세션 저장소를 로드합니다.
    /// </summary>
    private SessionStore LoadStore()
    {
        string path = StoragePath; // 세션 저장소 파일 경로입니다.
        if (!File.Exists(path))
        {
            return new SessionStore();
        }

        string json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SessionStore();
        }

        SessionStore store = JsonUtility.FromJson<SessionStore>(json);
        return store ?? new SessionStore();
    }

    /// <summary>
    /// 세션 저장소를 파일에 저장합니다.
    /// </summary>
    private void SaveStore(SessionStore store)
    {
        string path = StoragePath; // 세션 저장소 파일 경로입니다.
        string directoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string json = JsonUtility.ToJson(store, true);
        File.WriteAllText(path, json);

        if (_verboseLogging)
        {
            Debug.Log($"[LocalFileMultiplayerSessionBackend] 저장소 저장 완료 path={path}, sessions={store.Sessions.Count}", this);
        }
    }

    /// <summary>
    /// 동일 저장소 파일 접근을 위한 프로세스 간 뮤텍스를 생성합니다.
    /// </summary>
    private Mutex BuildProcessMutex()
    {
        string mutexKey = StoragePath.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_'); // 파일 경로 기반 뮤텍스 식별자입니다.
        string mutexName = $"BladeOfSouls_LocalSession_{mutexKey}";
        return new Mutex(false, mutexName);
    }

    /// <summary>
    /// 중복되지 않는 Join Code를 생성합니다.
    /// </summary>
    private static string GenerateUniqueJoinCode(SessionStore store)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            string joinCode = GenerateJoinCode(); // 새로 생성한 Join Code 후보 문자열입니다.
            bool alreadyExists = false;

            for (int i = 0; i < store.Sessions.Count; i++)
            {
                if (string.Equals(store.Sessions[i].JoinCode, joinCode, StringComparison.Ordinal))
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                return joinCode;
            }
        }

        return $"{GenerateJoinCode()}{UnityEngine.Random.Range(10, 99)}";
    }

    /// <summary>
    /// 알파벳 대문자 기반 6자리 Join Code를 생성합니다.
    /// </summary>
    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 혼동 가능한 문자(0/O, 1/I)를 제외한 코드 생성용 문자셋입니다.
        char[] buffer = new char[6]; // Join Code 문자 버퍼입니다.

        for (int i = 0; i < buffer.Length; i++)
        {
            int index = UnityEngine.Random.Range(0, chars.Length); // 문자셋에서 랜덤으로 선택한 인덱스입니다.
            buffer[i] = chars[index];
        }

        return new string(buffer);
    }
}
