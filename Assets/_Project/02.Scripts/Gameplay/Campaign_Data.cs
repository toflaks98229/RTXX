using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투를 넘어 이어지는 부대 하나의 기록입니다.
///
/// 왜 필요한가:
/// 지금까지 전투는 1회성이었습니다. 아무리 잘 싸워도 다음 전투는 언제나
/// 만원 편성으로 시작하므로, "정예 창병을 잃었다"는 사실에 무게가 없습니다.
/// 사기 붕괴도, 측후방 보너스도, 병종 상성도 전부 그 전투 안에서만 의미가
/// 있고 플레이어의 판단에 지속적인 대가가 따르지 않습니다.
///
/// 이 구조체는 그 대가를 만듭니다. 살아남은 인원이 다음 전투로 이어지고,
/// 싸운 경험이 능력치로 남습니다. 그러면 '이겼지만 값비쌌다'가 성립합니다.
///
/// 왜 Army를 직접 저장하지 않는가:
/// Army는 MonoBehaviour이고 씬에 묶여 있습니다. 전투가 끝나면 파괴되므로
/// 그대로 들고 있을 수 없습니다. 필요한 값만 떠서 순수 데이터로 남깁니다.
/// </summary>
[Serializable]
public class Campaign_Army
{
    /// <summary>
    /// 이 부대를 식별하는 고유 번호입니다.
    ///
    /// 이름으로 식별하지 않는 이유: 같은 이름의 부대가 여럿일 수 있고,
    /// 이름은 표시용이라 나중에 바뀔 수 있습니다.
    /// </summary>
    public int id;

    /// <summary>표시 이름입니다.</summary>
    public string name;

    /// <summary>병종입니다. 다음 전투에서 같은 스탯 에셋을 씁니다.</summary>
    public E_Unit_Class unitClass;

    /// <summary>장군 부대인지 여부입니다.</summary>
    public bool bgeneral;

    /// <summary>현재 인원입니다. 전투를 치를수록 줄어듭니다.</summary>
    public int strength;

    /// <summary>이 부대의 정원입니다. 보충은 이 값을 넘지 못합니다.</summary>
    public int maxStrength;

    /// <summary>누적 전과입니다.</summary>
    public int totalKills;

    /// <summary>누적 손실입니다.</summary>
    public int totalLosses;

    /// <summary>치른 전투 수입니다.</summary>
    public int battles;

    /// <summary>
    /// 경험 등급(0~3)입니다. 싸울수록 오릅니다.
    ///
    /// 왜 킬 수가 아니라 등급인가:
    /// 킬 수를 그대로 보너스로 쓰면 한 전투에서 크게 이긴 부대가
    /// 영원히 압도적이 됩니다. 등급으로 묶으면 성장이 완만해지고,
    /// 플레이어가 "이 부대는 정예다"를 한눈에 읽을 수 있습니다.
    /// </summary>
    public int veterancy;

    /// <summary>이 부대가 전투 불능(전멸/와해)인지 여부입니다.</summary>
    public bool bdestroyed;

    /// <summary>경험 등급을 올리는 데 필요한 누적 전과입니다.</summary>
    private static readonly int[] veterancyThresholds = { 0, 40, 120, 300 };

    /// <summary>최고 경험 등급입니다.</summary>
    public const int veterancyMax = 3;

    /// <summary>
    /// 경험 등급에 따른 능력 보너스 배율입니다.
    /// 1.0(신병)에서 시작해 등급마다 조금씩 오릅니다.
    ///
    /// 값이 작은 이유: 이 게임의 전투는 사기와 진형이 결정합니다.
    /// 경험이 그것을 압도하면 전술이 무의미해지므로, 체감되되
    /// 뒤집지는 못하는 크기로 둡니다.
    /// </summary>
    public float GetVeterancyRate()
    {
        return 1.0f + veterancy * 0.05f;
    }

    /// <summary>경험 등급 이름입니다. UI 표시용입니다.</summary>
    public string GetVeterancyName()
    {
        switch (veterancy)
        {
            case 1: return "숙련";
            case 2: return "정예";
            case 3: return "근위";
            default: return "신병";
        }
    }

    /// <summary>
    /// 전투 결과를 이 부대에 반영합니다.
    ///
    /// 전투가 끝날 때 한 번만 호출합니다.
    /// </summary>
    /// <param name="entry">그 전투의 전과 기록입니다.</param>
    public void Apply_Battle_Result(in Battle_Report_Entry entry)
    {
        // 생존자가 곧 다음 전투의 병력입니다.
        // 대입이지 누적이 아닙니다. 보고서의 survivors는 이미 '전투 후'
        // 값이므로, 여기서 빼거나 더하면 이중 계산이 됩니다.
        strength = entry.survivors;

        // 전과와 손실은 캠페인 전체에 걸쳐 누적합니다.
        totalKills += entry.kills;
        totalLosses += entry.losses;
        battles++;

        // 전멸했거나 와해된 부대는 다시 편성되지 않습니다.
        //
        // 와해(Shattered)를 포함하는 이유: 그 부대는 전장을 영구히 떠난
        // 것으로 처리됩니다. 생존자가 남아 있어도 부대로서는 끝입니다.
        if (entry.survivors <= 0 || entry.morale == E_Army_Morale.Shattered)
        {
            bdestroyed = true;
            strength = 0;
        }

        Update_Veterancy();
    }

    /// <summary>누적 전과에 따라 경험 등급을 갱신합니다.</summary>
    private void Update_Veterancy()
    {
        int grade = 0;

        // 높은 등급부터 확인해 처음 만족하는 것을 씁니다.
        // 낮은 쪽부터 보면 매번 마지막 조건까지 훑게 됩니다.
        for (int i = veterancyThresholds.Length - 1; i >= 0; i--)
        {
            if (totalKills >= veterancyThresholds[i])
            {
                grade = i;
                break;
            }
        }

        // 등급은 내려가지 않습니다.
        // 한 번 정예가 된 부대가 보충으로 신병이 섞였다고 강등되면,
        // 플레이어가 '이 부대는 믿을 만하다'는 판단을 유지할 수 없습니다.
        if (grade > veterancy) veterancy = grade;
        if (veterancy > veterancyMax) veterancy = veterancyMax;
    }

    /// <summary>
    /// 전투 사이에 인원을 보충합니다.
    ///
    /// 정원까지 한 번에 채우지 않는 이유:
    /// 그러면 손실에 대가가 없어져 이 시스템 전체가 무의미해집니다.
    /// 조금씩 채워야 '지금 무리할 것인가'라는 판단이 생깁니다.
    /// </summary>
    /// <param name="rate">정원 대비 보충 비율(0~1)입니다.</param>
    public void Reinforce(float rate)
    {
        if (bdestroyed) return;
        if (strength >= maxStrength) return;

        // 정원 대비 비율로 보충합니다. 현재 인원 대비가 아닙니다.
        //
        // 현재 인원 기준으로 하면 크게 무너진 부대일수록 회복이 느려져
        // 한 번 꺾인 부대가 영원히 못 일어섭니다. 정원 기준이면
        // 손실이 클수록 상대적으로 빨리 돌아옵니다.
        int amount = Mathf.CeilToInt(maxStrength * rate);

        // 정원을 넘지 않습니다.
        strength = Mathf.Min(strength + amount, maxStrength);
    }

    /// <summary>누적 교환비입니다. 잃은 1명당 잡은 수입니다.</summary>
    public float GetKillRatio()
    {
        return totalLosses > 0 ? (float)totalKills / totalLosses : totalKills;
    }
}

/// <summary>
/// 캠페인 한 판의 전체 상태입니다.
///
/// JsonUtility로 직렬화되므로 필드는 전부 public이고 [Serializable]입니다.
/// (JsonUtility는 프로퍼티와 Dictionary를 다루지 못합니다)
/// </summary>
[Serializable]
public class Campaign_State
{
    /// <summary>플레이어가 보유한 부대들입니다.</summary>
    public List<Campaign_Army> armies = new List<Campaign_Army>();

    /// <summary>치른 전투 수입니다.</summary>
    public int battlesFought;

    /// <summary>이긴 전투 수입니다.</summary>
    public int battlesWon;

    /// <summary>다음에 부여할 부대 번호입니다.</summary>
    public int nextArmyId = 1;

    /// <summary>전투 사이에 보충되는 비율입니다.</summary>
    public float reinforceRate = 0.15f;

    /// <summary>아직 싸울 수 있는 부대 수입니다.</summary>
    public int Get_Alive_Count()
    {
        int n = 0;

        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] != null && !armies[i].bdestroyed && armies[i].strength > 0) n++;
        }

        return n;
    }

    /// <summary>전 부대의 총 병력입니다.</summary>
    public int Get_Total_Strength()
    {
        int n = 0;

        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] != null && !armies[i].bdestroyed) n += armies[i].strength;
        }

        return n;
    }

    /// <summary>id로 부대를 찾습니다. 없으면 null입니다.</summary>
    public Campaign_Army Find(int id)
    {
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] != null && armies[i].id == id) return armies[i];
        }

        return null;
    }

    /// <summary>새 부대를 편성합니다.</summary>
    public Campaign_Army Recruit(string name, E_Unit_Class unitClass,
                                 int strength, bool bgeneral = false)
    {
        Campaign_Army army = new Campaign_Army
        {
            id = nextArmyId++,
            name = name,
            unitClass = unitClass,
            bgeneral = bgeneral,
            strength = strength,
            maxStrength = strength
        };

        armies.Add(army);
        return army;
    }

    /// <summary>전투 사이 보충을 전 부대에 적용합니다.</summary>
    public void Reinforce_All()
    {
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            armies[i].Reinforce(reinforceRate);
        }
    }
}
