using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace EndKnot.Modules;

// ============================================================================
// Backrooms 壁のシャドウキャスター生成 (Phase 2b: 壁を囲む inset caster)
//
// back-edge (壁の裏 1 辺) は「壁が全部光る」が、1 辺しか遮蔽しないため ①十字で光が漏れる
// ②左右非対称 の 2 欠陥があった。これを直すため、壁の **床に面する 4 辺すべて** を caster にし、
// かつ各辺を壁内部へ inset (壁の芯へ寄せる) する:
//   ・閉じた囲い → 十字/角で透けない (full occlusion)。
//   ・どの床側からも同じ → 左右対称。
//   ・inset δ で「外側 δ ぶんの壁が光り、中心に幅 (1-2δ) の影の芯」。δ→0.5 で中心線(半分lit)。
//
// 床に面する辺だけ出す (隣が壁の内部辺は出さない) ので、壁同士の間で光は漏れない。
// 「壁が全部光る (両面フル lit)」が欲しい場合は別途 壁スプライトの二重面化が要る (本家の二重壁方式)。
//
// per-segment 短 collider の理由: OverlapCircle は radius 圏内しか返さないので GPU の辺処理仕様に非依存。
// ============================================================================
public static class BackroomsCasters
{
    private const string Tag = "BBShadow";

    private static readonly List<GameObject> _casters = [];

    // 床に面する各辺を壁の芯へ寄せる量 (0..0.49)。大きいほど光る壁が太く・影の芯が細い。0.5 で中心線。
    // H=上下辺 (WallH 用)、V=左右辺 (WallV 用)。/bbshadow inset <v> [h] で実機調整。
    public static float HInset = 0.42f;
    public static float VInset = 0.48f;

    // 占有格子 + 4 方向の床面バッファ (再利用で alloc 回避。Rebuild は _occludersDirty 時のみ)。
    private static readonly HashSet<long> _cells = [];
    private static readonly List<(int key, int cell)> _bottom = []; // 床が下: y=iy-0.5+HInset、x=cell 方向に連続
    private static readonly List<(int key, int cell)> _top = [];    // 床が上: y=iy+0.5-HInset
    private static readonly List<(int key, int cell)> _left = [];   // 床が左: x=ix-0.5+VInset、y=cell 方向に連続
    private static readonly List<(int key, int cell)> _right = [];  // 床が右: x=ix+0.5-VInset

    // WallAabbs (per-cell grid box) から壁を囲む inset caster を作り直す。壁が cull/stream で変わった時だけ呼ぶ。
    public static void Rebuild(List<(float cx, float cy, float halfX, float halfY)> wallCells)
    {
        Clear();
        if (wallCells == null || wallCells.Count == 0) return;

        // 占有格子 (中心を整数セルにスナップ。collider offset=0 なので RoundToInt が exact)
        _cells.Clear();
        foreach (var w in wallCells) _cells.Add(PackCell(Mathf.RoundToInt(w.cx), Mathf.RoundToInt(w.cy)));

        // 床に面する辺のみ 4 方向バッファへ (隣が壁の内部辺は出さない = 壁同士の間で漏れない)
        _bottom.Clear(); _top.Clear(); _left.Clear(); _right.Clear();
        foreach (var w in wallCells)
        {
            int ix = Mathf.RoundToInt(w.cx), iy = Mathf.RoundToInt(w.cy);
            if (!_cells.Contains(PackCell(ix, iy - 1))) _bottom.Add((iy, ix)); // 下が床
            if (!_cells.Contains(PackCell(ix, iy + 1))) _top.Add((iy, ix));    // 上が床
            if (!_cells.Contains(PackCell(ix - 1, iy))) _left.Add((ix, iy));   // 左が床
            if (!_cells.Contains(PackCell(ix + 1, iy))) _right.Add((ix, iy));  // 右が床
        }

        float h = Mathf.Clamp(HInset, 0f, 0.49f);
        float v = Mathf.Clamp(VInset, 0f, 0.49f);
        int r = 0;
        r += EmitFaceRuns(_bottom, horizontal: true, lineOffset: -0.5f + h); // y = iy-0.5+h
        r += EmitFaceRuns(_top, horizontal: true, lineOffset: 0.5f - h);     // y = iy+0.5-h
        r += EmitFaceRuns(_left, horizontal: false, lineOffset: -0.5f + v);  // x = ix-0.5+v
        r += EmitFaceRuns(_right, horizontal: false, lineOffset: 0.5f - v);  // x = ix+0.5-v

        Logger.Info($"WallCasters rebuilt (inset H={h:F2} V={v:F2}): {_casters.Count} segs ({r} runs) from {_cells.Count} cells", Tag);
    }

    // bucket: (key=固定軸セル index, cell=可変軸セル index)。固定軸 world = key + lineOffset、可変軸 span = cell±0.5。
    // 同一 key 内で cell が連続する辺を最大長 run にマージ → per-segment EdgeCollider2D。
    private static int EmitFaceRuns(List<(int key, int cell)> edges, bool horizontal, float lineOffset)
    {
        if (edges.Count == 0) return 0;
        edges.Sort((a, b) => a.key != b.key ? a.key.CompareTo(b.key) : a.cell.CompareTo(b.cell));

        int runs = 0;
        for (int i = 0; i < edges.Count;)
        {
            int key = edges[i].key, start = edges[i].cell, end = start, j = i + 1;
            while (j < edges.Count && edges[j].key == key && edges[j].cell <= end + 1)
            {
                if (edges[j].cell > end) end = edges[j].cell;
                j++;
            }

            float lineWorld = key + lineOffset;
            float lo = start - 0.5f, hi = end + 0.5f;
            if (horizontal) SpawnSegment(new Vector2(lo, lineWorld), new Vector2(hi, lineWorld));
            else SpawnSegment(new Vector2(lineWorld, lo), new Vector2(lineWorld, hi));

            runs++;
            i = j;
        }

        return runs;
    }

    // 1 本の直線セグメントを layer10 の 2 点 EdgeCollider2D として spawn。GO は中点・点は local。
    private static void SpawnSegment(Vector2 p0, Vector2 p1)
    {
        Vector2 mid = (p0 + p1) * 0.5f;
        GameObject go = new("BBWallCaster");
        go.transform.position = new Vector3(mid.x, mid.y, 0f);
        go.layer = BackroomsConfig.ShadowCasterLayer;
        EdgeCollider2D ec = go.AddComponent<EdgeCollider2D>();
        Il2CppStructArray<Vector2> arr = new(2)
        {
            [0] = p0 - mid,
            [1] = p1 - mid
        };
        ec.points = arr;
        _casters.Add(go);
    }

    // (x,y) 整数セルを long に詰める。x を上位 32bit、y を下位 32bit。
    private static long PackCell(int x, int y) => ((long)x << 32) ^ (uint)y;

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
