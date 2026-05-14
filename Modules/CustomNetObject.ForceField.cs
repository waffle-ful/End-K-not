using UnityEngine;

namespace EndKnot
{
    internal sealed class ForceFieldCNO : CustomNetObject
    {
        // 単一 ○ 文字を <size> で半径に応じてスケール。
        //
        // 設計理由 (2026-05-14 fix):
        // - 8×8 ring (~900 byte packet) は AU 2026 anti-cheat の 1KB 単一 packet 閾値ぎりぎり
        // - 24×24 ring (~3072 byte packet) は確実に閾値超えで host が Hacking で kick される
        // - 単一文字 + 大 <size>% は packet ~50 byte で閾値に圧倒的余裕
        //
        // 文字選択: ○ (U+25CB White Circle) を採用。当初 〇 (U+3007) を試したが VCR SDF に
        // 無く fallback font 経由で <size>% が効かない事案を観測したため、AdventurerItem の
        // Grouping アイコンと同じ ○ + <font="VCR SDF"> wrapping に統一
        //
        // multiplier=120 で radius=5 → 600% cap。低半径でも 120% から表示。
        private static string BuildSprite(float radius)
        {
            int s = System.Math.Clamp((int)(radius * 120f), 60, 600);
            return $"<size={s}%><font=\"VCR SDF\"><line-height=67%><color=#4488ff>○</color></line-height></font></size>";
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
