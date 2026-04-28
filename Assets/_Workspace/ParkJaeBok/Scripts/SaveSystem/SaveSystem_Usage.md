# 세이브 시스템 사용법

## 개요

* `SaveDataStore`는 JSON 세이브 파일을 읽고 쓰는 유일한 클래스다.
* 세이브 파일 경로는 `Application.persistentDataPath/SaveData/save_data.json`이다.
* `SaveGameData`는 JSON 직렬화용 데이터 구조다.
* `SaveRuntimeData`는 메모리에 올라간 런타임 데이터다. 로드 시에는 Unity 오브젝트를 통째로 교체하지 않고, 저장된 값만 각 시스템에 적용한다.

## 씬 설정

1. `SaveDataStore`를 Bootstrap 또는 DDOL 시스템 오브젝트에 추가한다.
2. 디자이너가 Inspector에서 기본 옵션을 제어할 수 있도록 `OptionDefaultProfile`을 할당한다.
3. 필요하면 `OptionManager`, `StageProgressRuntime`, `StageSession`을 할당한다. 비워두면 `SaveDataStore`가 싱글톤 인스턴스를 자동으로 참조한다.

## 런타임 흐름

* 수동 저장: `SaveDataStore.Instance.Save("Context")`
* 수동 로드: `SaveDataStore.Instance.Load("Context")`
* 세이브 초기화: `SaveDataStore.Instance.ResetSaveData(true, "Context")`
* 경로 로그 출력: `SaveDataStore.Instance.LogSaveFilePath()`

`OptionManager.SaveCurrentOptions`와 `LoadCurrentOptions`는 내부적으로 `SaveDataStore`를 사용한다.

`GameFlowController.RequestContinue`는 `SaveDataStore`를 통해 데이터를 로드한 뒤, 복원된 `StageSession`을 기준으로 이어하기 씬을 결정한다.

## 멀티플레이 규칙

세이브 파일에는 **로컬 플레이어의 진행 상태와 옵션만 저장된다.**

다음 항목은 저장하지 않는다:

* NGO / UGS 세션 상태
* Host / Client 연결 상태
* 네트워크 동기화 데이터 
