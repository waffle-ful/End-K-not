using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace EndKnot.Modules;

// ============================================================================
// Backrooms 壁の輪郭線シャドウキャスター生成 (Phase 2a)
//
// 既存の壁レイアウト (BackroomsLobby._mergedOccluders = WallAabbs を greedy merge した矩形群) を
// **layer10 の EdgeCollider2D 線** に変換する。バニラ GPU 影はこれを Physics2D で拾って遮蔽メッシュ化。
//
// なぜ per-cell 箱でなく線か (今回実機で確定した事実):
//   ・GPU 遮蔽は collider の辺からメッシュを作る。同形なら塗り箱も線も同じ影だが、
//     **多数の per-cell 小箱は共有内辺で degenerate geometry になり blocky 黒**になる (旧 mergecast の失敗)。
//   ・薄い壁を 1 本の連続線にすれば内部辺が消え、部屋の境界が「壁線」として綺麗に影を落とす
//     (LevelImposter / Submerged と同型: 少数の大きい輪郭線)。共有壁は 1 本・ドアは線の隙間になる。
//
// 変換規則 (矩形 cx,cy,halfX,halfY):
//   ・両軸とも厚い (>0.6) = 本物の塗りブロック → 周囲を閉ループ (箱影が物理的に正しい)。
//   ・横長 (halfX>=halfY) = 横壁 → 水平中心線 1 本。
//   ・縦長 = 縦壁 → 垂直中心線 1 本。
// _mergedOccluders は ~数十個なので GPU 固定長 hits(100) に収まる (per-cell だと数百で溢れる)。
// ============================================================================
public static class BackroomsCasters
{
    private const string Tag = "BBShadow";

    // 厚いブロック判定の閾値。wall_h(1cell)=0.5 / wall_v=0.225 はこれ未満なので必ず線、
    // 2cell 以上の厚み(=1.0)だけ閉ループにする。
    private const float ThickThreshold = 0.6f;

    private static readonly List<GameObject> _casters = [];

    // merged 矩形群から layer10 EdgeCollider2D 線/ループ caster を作り直す。
    // 壁が stream で変わった時 (BackroomsLobby._occludersDirty) だけ呼ぶ。per-frame ではない。
    public static void Rebuild(List<(float cx, float cy, float halfX, float halfY)> merged)
    {
        Clear();
        if (merged == null) return;

        int loops = 0, hLines = 0, vLines = 0;
        foreach ((float cx, float cy, float halfX, float halfY) in merged)
        {
            Vector2[] pts;
            if (halfX > ThickThreshold && halfY > ThickThreshold)
            {
                // 厚い塗りブロック → 周囲閉ループ
                pts =
                [
                    new(-halfX, -halfY), new(halfX, -halfY), new(halfX, halfY), new(-halfX, halfY), new(-halfX, -halfY)
                ];
                loops++;
            }
            else if (halfX >= halfY)
            {
                // 横壁 → 水平中心線 (square な wall_h もここ: 横線が正しい)
                pts = [new(-halfX, 0f), new(halfX, 0f)];
                hLines++;
            }
            else
            {
                // 縦壁 → 垂直中心線
                pts = [new(0f, -halfY), new(0f, halfY)];
                vLines++;
            }

            GameObject go = new("BBWallCaster");
            go.transform.position = new Vector3(cx, cy, 0f);
            go.layer = BackroomsConfig.ShadowCasterLayer;
            EdgeCollider2D ec = go.AddComponent<EdgeCollider2D>();
            Il2CppStructArray<Vector2> arr = new(pts.Length);
            for (int i = 0; i < pts.Length; i++) arr[i] = pts[i];
            ec.points = arr;

            _casters.Add(go);
        }

        Logger.Info($"WallCasters rebuilt: {_casters.Count} (hLine={hLines} vLine={vLines} loop={loops}) from {merged.Count} rects", Tag);
    }

    public static int Clear()
    {
        int n = 0;
        foreach (GameObject go in _casters)
        {
            if (go == null) continue;
            try { Object.Destroy(go); n++; } catch { /* 既に破棄 — 無視 */ }
        }

        _casters.Clear();
        return n;
    }

    public static int Count => _casters.Count;
}
