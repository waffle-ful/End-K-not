using System;
using System.Text;
using UnityEngine;

namespace EndKnot
{
    internal sealed class ForceFieldCNO : CustomNetObject
    {
        // 24×24 リング（オリジナル 8×8 を 3× scale）。alpha sticky で payload 圧縮。
        //
        // 較正: 実機テストで multiplier=42 + line-height=67% でも visual が hit より小さい
        // と観測されたため、以下の 2 つを同時に補正:
        //   - multiplier: 42 → 55 (横方向 +30%)
        //   - line-height: 67% → 90% (縦圧縮を緩和、ring を真円に近づける)
        // 元 NaturalDisasters の 8×8 + 170% + 67% = Range 1.5 という較正は disaster の
        // 視覚 vs kill range が一致前提だが、実際には kill range の方が小さく設定されており
        // 較正基準として正確でなかった可能性が高い。
        //
        // 副作用として line-height 緩和でリング全体の縦寸法が ~34% 大きくなり、ネームプレート
        // 位置オフセット（visual がプレイヤー本体より上に出る現象）も相対的に目立たなくなる。
        // 300% cap での飽和は radius ≈ 5.45 で発生。option max=8 では visual が頭打ちになる。
        private static string BuildSprite(float radius)
        {
            int s = Math.Clamp((int)(radius * 55f), 30, 300);
            var sb = new StringBuilder(2800);
            sb.Append($"<size={s}%><font=\"VCR SDF\"><line-height=90%><color=#4488ff>");

            // オリジナル 8×8 を 3 倍に拡大したパターン:
            //   EEBBBBEE  →  rows 0-2 : 6E 12B 6E
            //   EBBEEBBE  →  rows 3-5 : 3E 6B 6E 6B 3E
            //   BBEEEEBB  →  rows 6-8 : 6B 12E 6B
            //   BEEEEEEB  →  rows 9-14: 3B 18E 3B
            //   BBEEEEBB  →  rows 15-17
            //   EBBEEBBE  →  rows 18-20
            //   EEBBBBEE  →  rows 21-23
            for (int i = 0; i < 3; i++) AddRow(sb, false, 6, 12, 6);
            for (int i = 0; i < 3; i++) AddRow(sb, false, 3, 6, 6, 6, 3);
            for (int i = 0; i < 3; i++) AddRow(sb, true, 6, 12, 6);
            for (int i = 0; i < 6; i++) AddRow(sb, true, 3, 18, 3);
            for (int i = 0; i < 3; i++) AddRow(sb, true, 6, 12, 6);
            for (int i = 0; i < 3; i++) AddRow(sb, false, 3, 6, 6, 6, 3);
            for (int i = 0; i < 3; i++) AddRow(sb, false, 6, 12, 6);

            sb.Append("</color></line-height></font></size>");
            return sb.ToString();
        }

        // startOpaque=false なら最初の run が透明から始まる。runs は不透明/透明を交互に消費。
        // Color tag は 1 度だけ Build() の頭で設定済みで、行内では alpha tag だけで切替。
        private static void AddRow(StringBuilder sb, bool startOpaque, params int[] runs)
        {
            bool opaque = startOpaque;
            foreach (int n in runs)
            {
                if (n > 0)
                {
                    sb.Append(opaque ? "<alpha=#ff>" : "<alpha=#00>");
                    for (int i = 0; i < n; i++) sb.Append('█');
                }

                opaque = !opaque;
            }

            sb.Append('\n');
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
