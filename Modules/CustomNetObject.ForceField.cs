using UnityEngine;

namespace EndKnot
{
    internal sealed class ForceFieldCNO : CustomNetObject
    {
        // 単一 〇 文字を <size> で半径に応じてスケール。
        //
        // 設計理由 (2026-05-14 fix):
        // - 8×8 ring (~900 byte packet) は AU 2026 anti-cheat の 1KB 単一 packet 閾値ぎりぎり
        // - 24×24 ring (~3072 byte packet) は確実に閾値超えで host が Hacking で kick される
        // - 単一文字 + 大 <size>% は packet ~50 byte で閾値に圧倒的余裕、かつ <size> 600% まで
        //   なら非モッドでも安全に描画される (memory: 700% 以上で描画崩壊、300% 確認済)
        //
        // multiplier=120 で radius=5 → 600% cap。低半径でも 120% から表示。
        private static string BuildSprite(float radius)
        {
            int s = System.Math.Clamp((int)(radius * 120f), 60, 600);
            return $"<size={s}%><color=#4488ff>〇</color></size>";
        }

        public ForceFieldCNO(Vector2 position, float radius)
        {
            CreateNetObject(BuildSprite(radius), position);
        }

        public override void OnMeeting()
        {
            Despawn();
        }
    }
}
