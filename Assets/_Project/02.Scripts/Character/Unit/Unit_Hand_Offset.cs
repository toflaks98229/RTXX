using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몸통 타일별 무기·방패 위치 보정표입니다.
///
/// 왜 필요한가:
/// DCSS 타일은 무기·방패를 몸통에 그냥 겹치면 손에 들리지 않습니다.
/// crawl은 몸통 타일마다 장비를 픽셀 단위로 밀어 주는 표를 코드에 갖고
/// 있습니다. 원본 주석이 그대로 말해 줍니다.
///
///   "Returns the amount of pixels necessary to shift a wielded weapon
///    from its default placement."
///   (crawl-ref/source/tilemcache.cc, get_weapon_offset)
///
/// 무기는 337종이 정의되어 있고 그중 312종이 0이 아닙니다.
/// 즉 보정 없이 맞는 쪽이 오히려 예외입니다.
///
/// ---------------------------------------------------------------------
/// 왜 '몇몇 유닛만' 어긋나 보이는가
/// ---------------------------------------------------------------------
/// 값이 몸통마다 다릅니다. 인간은 무기가 왼쪽 1px, 오크는 2px입니다.
/// 그런데 Unit._Start는 몸통을 무작위로 고르므로, 같은 부대 안에서도
/// 맞는 병사와 어긋난 병사가 섞입니다.
///
/// ---------------------------------------------------------------------
/// 이것은 Full Rect 문제와 별개입니다
/// ---------------------------------------------------------------------
/// 앞서 고친 Tight 메시 문제는 '트리밍 때문에 피벗이 밀리는' 것이었고,
/// 이 표는 '몸통마다 손 위치가 다른' 것입니다. 원인이 다릅니다.
/// 두 가지가 겹쳐 있었으므로 둘 다 필요합니다.
///
/// 근거와 추출 과정은 Docs/DCSS_Hand_Offset.md를 보십시오.
/// </summary>
public static class Unit_Hand_Offset
{
    /// <summary>
    /// 타일 1픽셀에 해당하는 월드 거리입니다.
    ///
    /// 타일이 32px이고 PPU가 32이므로 1타일 = 1유닛입니다.
    /// 따라서 1px = 1/32 유닛입니다. 임포트 설정(PPU 32)을 바꾸면
    /// 이 값도 함께 바뀌어야 합니다.
    /// </summary>
    private const float pixel = 1.0f / 32.0f;

    /// <summary>
    /// 몸통 스프라이트 이름 -> (무기 오프셋, 방패 오프셋)입니다. 단위는 픽셀입니다.
    ///
    /// 값의 출처는 crawl의 tilemcache.cc이며, 파일 이름은
    /// DCSS_Tile_Importer가 붙인 규칙(경로를 _로 이어 붙임)을 따릅니다.
    ///
    ///   mon/humanoids/humans/human.png -> human_human
    ///
    /// 무기와 방패가 서로 다른 값을 갖는다는 점에 주의하십시오.
    /// 오크는 무기가 왼쪽 2px인데 방패는 오른쪽 1px입니다.
    /// 한 값으로 둘 다 옮기면 한쪽이 여전히 틀어집니다.
    /// </summary>
    private static readonly Dictionary<string, Offset> table =
        new Dictionary<string, Offset>(16)
    {
        // --- 인간 계열 (TILEP_MONS_HUMAN / _1 / _2) ---
        //
        // crawl 원본은 (-1, -2)입니다. (tilemcache.cc:648-654)
        // 세로만 -4로 낮춰 두었습니다. **의도된 차이입니다.**
        //
        // 왜 원본과 다르게 두는가:
        // 무기를 어느 부위에서 쥐는지를 실측했습니다. 무기 타일에서
        // 자루(쥐는 부분)는 y12~16입니다. 인형 베이스에 오프셋 0으로
        // 얹으면 정확히 그 구간을 쥡니다. 오크도 y13~17로 자루를 쥡니다.
        //
        // 그런데 인간 몬스터 타일은 원본값 -2에서 y18~22를 쥡니다.
        // 자루가 아니라 **칼몸**입니다. 손이 손잡이보다 위에 있습니다.
        //
        //   ofs_y   쥐는 무기 부위
        //     -2    y18~22   <- 칼몸 (원본값)
        //     -4    y14~18   <- 자루. 인형과 같은 관계
        //
        // 원인은 이 타일의 팔이 다른 몸통보다 높게 그려진 것입니다.
        // (인간 손 y16~20 / 오크 손 y12~16)
        // crawl에서 이 조합이 눈에 덜 띄는 이유는 확인하지 못했습니다.
        // 다만 우리 화면에서는 어긋나 보이므로 실측값을 따릅니다.
        { "human_human",     new Offset(-1, -4,  0, 0) },
        { "human_human2",    new Offset(-1, -4,  0, 0) },
        { "human_human3",    new Offset(-1, -4,  0, 0) },

        // --- 오크 계열 ---
        // 같은 오크라도 계급마다 자세가 달라 값이 갈립니다.
        { "orc_orc",         new Offset(-2, -1,  1, 0) },
        { "orc_orc_warrior", new Offset(-2, -1,  1, 0) },
        { "orc_orc_knight",  new Offset(-2,  1,  1, 0) },
        { "orc_orc_priest",  new Offset(-1,  1,  1, 0) },
        { "orc_orc_warlord", new Offset(-3,  0,  1, 0) },

        // --- 플레이어 인형 베이스 ---
        //
        // 보정이 0입니다. 오타가 아닙니다.
        // 이 타일들은 player/base에 있는 '인형 파트'이고, 인형 경로의
        // hand1/hand2는 몸통과 같은 규격으로 그려져 있어 crawl도 보정을
        // 걸지 않습니다. (오프셋은 몬스터 타일 합성 경로 전용입니다)
        //
        // 표에 명시적으로 적어 두는 이유: 빠뜨린 것과 0인 것을
        // 구분하기 위해서입니다. 없으면 아래 경고가 뜹니다.
        { "base_human_m",    new Offset(0, 0, 0, 0) },
        { "base_human_f",    new Offset(0, 0, 0, 0) },
    };

    /// <summary>표에 없는 몸통을 한 번만 보고하기 위한 기록입니다.</summary>
    private static readonly HashSet<string> reported = new HashSet<string>();

    /// <summary>
    /// 세로 보정을 적용할지 여부입니다.
    ///
    /// ---------------------------------------------------------------------
    /// 한 번 껐다가 다시 켠 항목입니다
    /// ---------------------------------------------------------------------
    /// 중간에 "ofs_y는 잘린 타일을 전제한 값이므로 쓰면 안 된다"고 판단해
    /// 껐던 적이 있습니다. **그 판단은 틀렸습니다.**
    ///
    /// 확인 방법: crawl 저장소의 타일을 실제로 겹쳐 그려 봤습니다.
    /// human_human.png 위에 weapon_dagger.png를 (-1,-2)만큼 옮겨 합성하면
    /// 단검 손잡이 끝이 팔(y16~20)에 정확히 물립니다. 보정을 빼면 손잡이가
    /// 팔 아래로 내려가고, 부호를 뒤집으면 더 내려갑니다.
    ///
    /// 즉 crawl의 값은 우리 타일에도 그대로 유효합니다.
    /// (뒤의 두 항 inf.offset_y / size_oy는 32x32 무트리밍이라 0이고,
    ///  ofs_y만 남는 것이 맞습니다)
    ///
    /// 끄고 싶다면 이 값을 false로 두십시오. 그 경우 무기가 손보다
    /// 약간 아래에 놓입니다.
    /// </summary>
    public static bool buseVerticalOffset = true;

    /// <summary>
    /// 몸통 하나에 대한 무기·방패 보정값입니다. 단위는 픽셀입니다.
    ///
    /// ---------------------------------------------------------------------
    /// y 부호 — crawl과 유니티는 위아래가 반대입니다
    /// ---------------------------------------------------------------------
    /// 이 구조체에 담기는 값은 crawl 원본 그대로입니다. 그런데 crawl의
    /// 오프셋은 **화면 좌표계(y가 아래로 증가)** 기준입니다.
    ///
    ///   tiletex.cc:122
    ///     int pos_sy_adjust = ofs_y + inf.offset_y + size_oy;
    ///     (size_oy = TILE_Y - inf.height, 즉 위에서 아래로 재는 값)
    ///
    /// 유니티 월드는 y가 위로 증가하므로, 그대로 넣으면 위아래가
    /// 반대로 갑니다. HUMAN의 무기 ofs_y = -2는 crawl에서 '2px 위로'인데
    /// 유니티에서 -2를 그대로 쓰면 '2px 아래로'가 됩니다.
    ///
    /// 실제로 실측에서 그 증상이 잡혔습니다.
    ///   몸통 월드 y 0.29  vs  무기 월드 y 0.23  (0.06 = 2px 아래로)
    ///
    /// 그래서 Weapon_Local/Shield_Local이 y에 음수를 곱합니다.
    /// **표의 값은 원본 그대로 두십시오.** 뒤집기는 변환 지점에서만
    /// 합니다. 그래야 crawl 소스와 표를 그대로 대조할 수 있습니다.
    /// </summary>
    public readonly struct Offset
    {
        /// <summary>무기의 가로 보정(px)입니다. 음수면 왼쪽입니다.</summary>
        public readonly int weaponX;

        /// <summary>무기의 세로 보정(px)입니다. 음수면 아래쪽입니다.</summary>
        public readonly int weaponY;

        /// <summary>방패의 가로 보정(px)입니다.</summary>
        public readonly int shieldX;

        /// <summary>방패의 세로 보정(px)입니다.</summary>
        public readonly int shieldY;

        /// <summary>보정값을 만듭니다.</summary>
        /// <param name="wx">무기 가로 보정(px)입니다.</param>
        /// <param name="wy">무기 세로 보정(px)입니다.</param>
        /// <param name="sx">방패 가로 보정(px)입니다.</param>
        /// <param name="sy">방패 세로 보정(px)입니다.</param>
        public Offset(int wx, int wy, int sx, int sy)
        {
            weaponX = wx;
            weaponY = wy;
            shieldX = sx;
            shieldY = sy;
        }

        /// <summary>
        /// 무기 보정을 유니티 로컬 좌표로 변환합니다.
        ///
        /// y 부호를 뒤집는 것에 주의하십시오. 이유는 아래 Flip_Y를 보십시오.
        /// </summary>
        /// <returns>로컬 위치에 그대로 넣을 수 있는 값입니다.</returns>
        public Vector3 Weapon_Local() => new Vector3(weaponX * pixel, Vertical(weaponY), 0.0f);

        /// <summary>
        /// 방패 보정을 유니티 로컬 좌표로 변환합니다.
        /// </summary>
        /// <returns>로컬 위치에 그대로 넣을 수 있는 값입니다.</returns>
        public Vector3 Shield_Local() => new Vector3(shieldX * pixel, Vertical(shieldY), 0.0f);

        /// <summary>
        /// 세로 보정을 월드 거리로 바꿉니다.
        ///
        /// 기본적으로 0을 돌려줍니다. 이유는 buseVerticalOffset을 보십시오.
        /// 켜져 있을 때만 crawl 값을 쓰며, 그 경우 y 부호를 뒤집습니다.
        /// (crawl은 화면 좌표계라 y가 아래로 증가합니다)
        /// </summary>
        /// <param name="value">crawl 원본의 세로 오프셋(px)입니다.</param>
        /// <returns>로컬 y에 넣을 값입니다.</returns>
        private static float Vertical(int value)
        {
            // 부호를 뒤집습니다.
            //
            // crawl의 ofs_y는 화면 좌표계(y가 아래로 증가)이고 유니티
            // 월드는 y가 위로 증가합니다. HUMAN 무기의 -2는 crawl에서
            // '위로 2px'이므로 유니티에서는 +2가 되어야 같은 방향입니다.
            //
            // 검증: 타일을 실제로 겹쳐 그려 본 결과 이 값일 때 단검 손잡이
            // 끝이 팔(y16~20)에 가장 잘 물립니다.
            return buseVerticalOffset ? -value * pixel : 0.0f;
        }
    }

    // 공개 메서드
    /// <summary>
    /// 이름이 보정표에 등록되어 있는지 확인합니다.
    ///
    /// 검증 도구가 '빠진 몸통'을 미리 찾는 데 씁니다.
    /// Get은 없을 때 0을 돌려주므로 그것만으로는 '등록된 0'과
    /// '누락'을 구분할 수 없습니다.
    /// </summary>
    /// <param name="spriteName">확인할 스프라이트 이름입니다.</param>
    /// <returns>표에 있으면 true입니다.</returns>
    public static bool Has(string spriteName)
    {
        return !string.IsNullOrEmpty(spriteName) && table.ContainsKey(spriteName);
    }

    /// <summary>보정표에 등록된 몸통 이름 전체입니다.</summary>
    public static IEnumerable<string> Keys => table.Keys;

    /// <summary>
    /// 몸통 스프라이트에 해당하는 보정값을 찾습니다.
    ///
    /// 표에 없으면 보정 없음(전부 0)을 돌려주고 경고를 한 번만 남깁니다.
    /// 몸통을 새로 추가했는데 표를 갱신하지 않으면 조용히 어긋나므로,
    /// 그 상황을 눈에 띄게 만드는 것이 목적입니다.
    /// </summary>
    /// <param name="bodySprite">몸통으로 쓰는 스프라이트입니다.</param>
    /// <returns>무기·방패 보정값입니다.</returns>
    public static Offset Get(Sprite bodySprite)
    {
        if (bodySprite == null) return default;

        if (table.TryGetValue(bodySprite.name, out Offset found)) return found;

        if (reported.Add(bodySprite.name))
        {
            Debug.LogWarning(
                $"[Hand_Offset] 몸통 '{bodySprite.name}'의 손 위치 보정값이 없습니다. " +
                "무기와 방패가 손에서 어긋날 수 있습니다.\n" +
                "  crawl의 tilemcache.cc에서 값을 찾아 Unit_Hand_Offset.table에 추가하십시오. " +
                "(자세한 내용: Docs/DCSS_Hand_Offset.md)");
        }

        return default;
    }

    // 몸통별 '손 높이' 보정(Hand_Row_Correction)은 제거되었습니다.
    //
    // 한때 인간 타일의 팔이 4px 높다고 보고 무기를 그만큼 올렸습니다.
    // 그런데 타일을 실제로 겹쳐 그려 확인하니, 그때 팔로 읽은 부분은
    // 반대편 팔이었고 무기를 쥐는 손이 아니었습니다.
    //
    // crawl의 오프셋(-1,-2)만으로 손잡이가 손에 정확히 물립니다.
    // 추가 보정을 넣으면 오히려 손잡이가 팔뚝 위로 올라갑니다.

    /// <summary>
    /// 무기·방패 렌더러의 로컬 위치를 몸통에 맞게 잡아 줍니다.
    ///
    /// ---------------------------------------------------------------------
    /// 왜 오프셋만으로는 안 맞는가 — 계층이 서로 다릅니다
    /// ---------------------------------------------------------------------
    /// 프리팹 실측 결과 두 렌더러의 부모가 다릅니다.
    ///
    ///   Unit_Weapon -> Unit_Sprite 의 자식  (몸통 아래)
    ///   Unit_Shield -> Unit 의 자식         (몸통과 형제)
    ///
    /// 그런데 Unit_Animation이 몸통(Unit_Sprite)에 두 가지를 겁니다.
    ///
    ///   scale = size
    ///   localPosition.y = (size - 1) * 0.5     <- 키가 클수록 위로 올림
    ///
    /// 무기는 몸통의 자식이라 이 둘을 **함께 물려받고**, 방패는 형제라
    /// **전혀 받지 않습니다.** 그래서 같은 픽셀 값을 두 렌더러에 그대로
    /// 넣으면 서로 다른 결과가 나옵니다.
    ///
    /// 실제로 보고된 값(size 1.268인 부대):
    ///   무기가 대충 맞는 위치 = (-0.03125, 0.134)
    ///     x = -0.03125 = 정확히 -1px  (HUMAN 무기 오프셋과 일치)
    ///     y =  0.134   = (1.268-1)*0.5  <- 타일 오프셋이 아니라 몸통 들어올림
    ///
    /// 즉 y로 넣어야 했던 값은 타일 보정이 아니라 **몸통 들어올림을
    /// 되돌리는 보정**이었습니다. 무기는 몸통 아래에 있는데 몸통이
    /// 위로 올라가 버리니 무기만 따라 올라갔던 것입니다.
    ///
    /// 그래서 계층을 코드에 적어 두지 않고 **실제 부모를 보고 판단**합니다.
    /// 프리팹 배선이 바뀌어도 따라오고, 무기와 방패가 서로 다르게
    /// 매달려 있어도 각각 옳게 계산됩니다.
    ///
    ///   몸통의 자손이면 : 크기와 들어올림을 이미 물려받음
    ///                     -> 타일 오프셋을 size로 나누기만 함
    ///   그렇지 않으면   : 아무것도 물려받지 않음
    ///                     -> 오프셋에 size를 곱하고 들어올림을 직접 더함
    /// </summary>
    /// <param name="bodySprite">기준이 되는 몸통 스프라이트입니다.</param>
    /// <param name="body">몸통 렌더러입니다. 계층 판정의 기준이 됩니다.</param>
    /// <param name="weapon">무기 렌더러입니다. 없으면 null이어도 됩니다.</param>
    /// <param name="shield">방패 렌더러입니다. 없으면 null이어도 됩니다.</param>
    /// <param name="size">부대의 유닛 크기 배율입니다. Army_Data.GetSize() 값입니다.</param>
    public static void Apply(Sprite bodySprite, SpriteRenderer body,
                             SpriteRenderer weapon, SpriteRenderer shield, float size)
    {
        Offset offset = Get(bodySprite);

        // size가 0이면 나눗셈이 무너집니다. 스탯이 비어 있는 경우입니다.
        if (size <= 0.0f) size = 1.0f;

        Transform bodyTransform = body != null ? body.transform : null;

        Place(weapon, offset.Weapon_Local(), bodyTransform, size);
        Place(shield, offset.Shield_Local(), bodyTransform, size);
    }

    // 비공개 메서드
    /// <summary>
    /// 장비 렌더러 하나를 몸통 기준으로 배치합니다.
    ///
    /// 몸통의 자손인지 아닌지에 따라 계산이 달라집니다.
    /// 자세한 이유는 Apply의 요약을 보십시오.
    /// </summary>
    /// <param name="renderer">배치할 렌더러입니다. null이면 아무것도 하지 않습니다.</param>
    /// <param name="pixelOffset">타일 보정값을 월드 거리로 바꾼 것입니다.</param>
    /// <param name="body">몸통 트랜스폼입니다. null이면 보정 없이 그대로 둡니다.</param>
    /// <param name="size">유닛 크기 배율입니다.</param>
    private static void Place(SpriteRenderer renderer, Vector3 pixelOffset,
                              Transform body, float size)
    {
        if (renderer == null) return;

        Transform t = renderer.transform;

        Vector3 p;

        if (body != null && t.IsChildOf(body))
        {
            // 몸통 아래에 매달려 있습니다.
            //
            // 부모가 size배로 커져 있으므로 로컬 값을 size로 나눠 두어야
            // 최종 크기가 의도한 픽셀 수와 같아집니다. 들어올림은 부모가
            // 이미 갖고 있으므로 더하면 두 번 올라갑니다.
            p = pixelOffset / size;

            // 좌우 반전은 상쇄하지 않습니다. 오히려 **따라가야** 합니다.
            //
            // 실측으로 확인한 것:
            //   몸통 월드 x 1.60, 무기 로컬 x -0.03 -> 무기 월드 x 1.63
            // 음수 로컬이 더 큰 월드 좌표를 냈습니다. 부모 스케일 x가
            // 음수(반전)이기 때문입니다.
            //
            // 이것은 버그가 아니라 필요한 동작입니다. 병사가 왼쪽을 보면
            // 손도 반대쪽에 있으므로 무기 보정도 같이 뒤집혀야 맞습니다.
            // crawl의 오프셋은 '오른쪽을 본 원본' 기준이고, 반전은 그림
            // 전체를 뒤집으므로 보정도 함께 뒤집히는 것이 옳습니다.
            //
            // 즉 여기서는 아무것도 하지 않는 것이 정답입니다.
            // (예전에 이 부분을 상쇄하려다 왼쪽을 본 병사만 두 배로
            //  어긋나게 만들 뻔했습니다)
        }
        else
        {
            // 몸통과 형제(또는 남)입니다.
            //
            // 부모 스케일이 없으므로 픽셀 값에 size를 곱해 몸통과 같은
            // 척도로 맞추고, 몸통이 올라간 만큼을 직접 더해 줍니다.
            // 그러지 않으면 몸통만 올라가고 장비는 발밑에 남습니다.
            p = pixelOffset * size;
            p.y += (size - 1.0f) * 0.5f;
        }

        // z는 건드리지 않습니다.
        // 앞뒤 순서는 정렬 순서(sortingOrder)가 정하므로, 여기서 z를
        // 옮기면 그 규칙과 충돌합니다.
        p.z = t.localPosition.z;

        t.localPosition = p;
    }
}
