using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 무기·방패 손 위치 보정이 제대로 적용되는지 확인하는 검증 도구입니다.
///
/// 왜 필요한가:
/// 보정값은 1~3px입니다. 32px 타일에서도 작은 값이라 화면만 봐서는
/// "고쳤는데 여전히 어긋난 것"과 "원래 그 정도인 것"을 구분하기
/// 어렵습니다. 숫자로 확인해야 합니다.
///
/// 확인하는 것:
///   1) 부대가 쓰는 몸통이 전부 보정표에 있는가 (빠지면 조용히 어긋남)
///   2) 표의 값이 실제 로컬 위치로 변환되는가
/// </summary>
public static class Hand_Offset_Probe
{
    /// <summary>보정표가 씬의 몸통을 모두 덮는지 검사합니다.</summary>
    [MenuItem("RTXX/검증: 무기·방패 손 위치 보정")]
    public static void Verify()
    {
        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsInactive.Include);

        if (armies.Length == 0)
        {
            Debug.LogError("[Hand] 씬에 부대가 없습니다. 전투 씬을 연 뒤 실행하십시오.");
            return;
        }

        // 부대들이 실제로 쓰는 몸통을 모읍니다.
        // 중복이 많으므로 집합으로 받습니다.
        HashSet<Sprite> bodies = new HashSet<Sprite>();

        for (int i = 0; i < armies.Length; i++)
        {
            List<Sprite> list = armies[i].images_Unit;
            if (list == null) continue;

            for (int j = 0; j < list.Count; j++)
            {
                if (list[j] != null) bodies.Add(list[j]);
            }
        }

        if (bodies.Count == 0)
        {
            Debug.LogError("[Hand] 부대에 몸통 스프라이트가 배선되어 있지 않습니다.");
            return;
        }

        int covered = 0;
        int missing = 0;

        Debug.Log($"[Hand] 몸통 {bodies.Count}종의 보정값을 확인합니다.");

        foreach (Sprite body in bodies)
        {
            Unit_Hand_Offset.Offset o = Unit_Hand_Offset.Get(body);

            // 전부 0인 경우는 두 가지입니다.
            //   - 표에 있고 실제로 0 (플레이어 인형 베이스)
            //   - 표에 없어서 기본값 0 (누락)
            // Get이 누락 시 경고를 남기므로, 여기서는 값만 보고합니다.
            bool bzero = o.weaponX == 0 && o.weaponY == 0
                         && o.shieldX == 0 && o.shieldY == 0;

            if (bzero) missing++;
            else covered++;

            Debug.Log($"  {body.name,-20} 무기({o.weaponX,2},{o.weaponY,2})  " +
                      $"방패({o.shieldX,2},{o.shieldY,2})  " +
                      $"-> 무기 로컬 {o.Weapon_Local()}");
        }

        Debug.Log($"[Hand] 보정 적용 {covered}종 / 보정 없음(0) {missing}종\n" +
                  "  '보정 없음'이 플레이어 인형 베이스라면 정상입니다.\n" +
                  "  몬스터 타일인데 0이면 표에 빠진 것이므로 위 경고를 확인하십시오.");
    }

    /// <summary>배치모드에서 검증하고 종료합니다.</summary>
    public static void Verify_From_CLI()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/_Project/01.Scenes/Scene1.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        Verify();
        EditorApplication.Exit(0);
    }
}
