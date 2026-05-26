using UnityEngine;

namespace EndKnot
{
    // ============================================================
    // RiptideWaveCNO
    //   ・マップ全体をカバーする波の視覚 CNO。
    //   ・<size=N> 絶対モード + <mark=#xxxxxx>WWWWWWWW</mark> 構造で
    //     1 スプライト ≈ 200 byte 以内を維持し、AU 2026 の 1KB packet 閾値に余裕で収まる。
    //   ・4 方向それぞれ異なるグラデーション色を使用。
    //
    // FontSizeAbsolute の暫定値 1200 について:
    //   ForceField の較正式 visual ≈ 0.05 × font^0.9 を逆算すると
    //   35 unit (半マップ) を覆うには font ≈ 1450 が必要。
    //   ただし文字数や行数が異なるため曲線が変わる可能性がある。
    //   TODO: ユーザーが /sizetest で実機 calibration 後に確定値に差し替えること。
    //         目安: /sizetest 800, 1200, 1600, 2000 で 8×8 スプライトの world 幅を計測し
    //              log-log fit して FontSizeAbsolute を確定する。
    // ============================================================
    internal sealed class RiptideWaveCNO : CustomNetObject
    {
        private const int FontSizeAbsolute = 300;

        // 方向別スプライト — 8 行 × 8 文字で約 200 byte/スプライト
        // 前面は濃い青、後面は薄い青でグラデーション表現
        // </alpha> 閉じタグ禁止 (TMP タグ罠 #10)。半透明は <color=#xxxxxx80> で
        private static readonly string[] Sprites =
        {
            // Index 0 : 左→右 (wave front = 右端)
            $"<size={FontSizeAbsolute}><line-height=97%>" +
            $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark></size>",

            // Index 1 : 右→左 (wave front = 左端)
            $"<size={FontSizeAbsolute}><line-height=97%>" +
            $"<mark=#0095ff>WW</mark><mark=#b8e1ff>WWWWWW</mark>\n" +
            $"<mark=#0095ff>WW</mark><mark=#b8e1ff>WWWWWW</mark>\n" +
            $"<mark=#0095ff>WW</mark><mark=#b8e1ff>WWWWWW</mark>\n" +
            $"<mark=#0095ff>WW</mark><mark=#b8e1ff>WWWWWW</mark>\n" +
            $"<mark=#0095ff>WW</mark><mark=#b8e1ff>WWWWWW</mark>\n" +
            $"<mark=#0095ff>WW</mark><mark=#b8e1ff>WWWWWW</mark>\n" +
            $"<mark=#0095ff>WW</mark><mark=#b8e1ff>WWWWWW</mark>\n" +
            $"<mark=#0095ff>WW</mark><mark=#b8e1ff>WWWWWW</mark></size>",

            // Index 2 : 上→下 (wave front = 下端)
            $"<size={FontSizeAbsolute}><line-height=97%>" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#0095ff>WWWWWWWW</mark>\n" +
            $"<mark=#0073ff>WWWWWWWW</mark></size>",

            // Index 3 : 下→上 (wave front = 上端)
            $"<size={FontSizeAbsolute}><line-height=97%>" +
            $"<mark=#0073ff>WWWWWWWW</mark>\n" +
            $"<mark=#0095ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark>\n" +
            $"<mark=#b8e1ff>WWWWWWWW</mark></size>",
        };

        public static string GetSprite(int directionIndex)
        {
            if (directionIndex < 0 || directionIndex >= Sprites.Length) return Sprites[0];
            return Sprites[directionIndex];
        }

        public RiptideWaveCNO(Vector2 position, int directionIndex)
        {
            CreateNetObject(GetSprite(directionIndex), position);
            // 即 TP で SnapToSendFrameCount を立てて spawn 位置を確実に sync。
            // CreateNetObject 直後の disconnected player は default 位置 (host transform 付近)
            // に描画されてしまうため、これがないと spawn 瞬間に sprite が host 位置に飛ぶ。
            TP(position);
        }

        public override void OnMeeting()
        {
            Despawn();
        }
    }

    // ============================================================
    // RiptidePredictiveGhostCNO
    //   ・ShowPredictiveGhost option ON 時のみ spawn される半透明予告 CNO。
    //   ・波の前方に配置し、進行先を予告する。
    //   ・<color=#xxxxxx80> で 50% 半透明化 — </alpha> 閉じタグは使わない (TMP タグ罠 #10)
    // ============================================================
    internal sealed class RiptidePredictiveGhostCNO : CustomNetObject
    {
        private const int FontSizeAbsolute = 300;

        // 半透明 (AA=80 ≈ 50% alpha) の予告スプライト。
        // </alpha> 閉じタグ禁止 (TMP タグ罠 #10)、<color=#xxxxxx80> ベースで実装
        private static readonly string[] GhostSprites =
        {
            // Index 0 : 左→右
            $"<size={FontSizeAbsolute}><line-height=97%>" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color></size>",

            // Index 1 : 右→左
            $"<size={FontSizeAbsolute}><line-height=97%>" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color></size>",

            // Index 2 : 上→下
            $"<size={FontSizeAbsolute}><line-height=97%>" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color></size>",

            // Index 3 : 下→上
            $"<size={FontSizeAbsolute}><line-height=97%>" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color>\n" +
            $"<color=#b8e1ff80>WWWWWWWW</color></size>",
        };

        public static string GetGhostSprite(int directionIndex)
        {
            if (directionIndex < 0 || directionIndex >= GhostSprites.Length) return GhostSprites[0];
            return GhostSprites[directionIndex];
        }

        public RiptidePredictiveGhostCNO(Vector2 position, int directionIndex)
        {
            CreateNetObject(GetGhostSprite(directionIndex), position);
            TP(position);  // spawn 位置を即 sync (本体 CNO と同様の理由)
        }

        public override void OnMeeting()
        {
            Despawn();
        }
    }
}
