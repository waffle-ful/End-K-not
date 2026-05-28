using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EndKnot
{
    // ============================================================
    // RiptideWaveSizeTestCNO — Riptide 視覚較正用 debug CNO
    //
    // RiptideWaveCNO とまったく同じ形 (8×8 W グリッド、default font、line-height=97%) を
    // 任意 FontSizeAbsolute で生成して、実機で「波 1 個の world unit 幅」を測る用途。
    //
    // ForceField の <size=N><font="VCR SDF"><○> 較正は **別物** なので流用不可:
    //   - 文字: W vs ○ (W は約 2× 横幅)
    //   - フォント: 默认 (LiberationSans) vs VCR SDF (固定幅)
    //   - レイアウト: 8×8 グリッド vs 単一文字
    //
    // /ripsize で 5 段階を並べてユーザーが目視測定する想定。
    // ============================================================
    internal sealed class RiptideWaveSizeTestCNO : CustomNetObject
    {
        public static readonly List<RiptideWaveSizeTestCNO> Active = [];

        // Riptide direction 0 (左→右) と同形のスプライト、size 指定子のみ差替え。
        // sizeSpec は "50" (絶対) / "200%" (パーセント) / null (タグ無しデフォルト) のいずれか。
        private static string BuildSprite(string sizeSpec)
        {
            string openSize = string.IsNullOrEmpty(sizeSpec) ? string.Empty : $"<size={sizeSpec}>";
            string closeSize = string.IsNullOrEmpty(sizeSpec) ? string.Empty : "</size>";
            return $"{openSize}<line-height=97%>" +
                   $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
                   $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
                   $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
                   $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
                   $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
                   $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
                   $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>\n" +
                   $"<mark=#b8e1ff>WWWWWW</mark><mark=#0095ff>WW</mark>{closeSize}";
        }

        public RiptideWaveSizeTestCNO(Vector2 position, string sizeSpec)
        {
            CreateNetObject(BuildSprite(sizeSpec), position);
            TP(position);
            Active.Add(this);
        }

        public override void OnMeeting() => Despawn();

        public enum Mode { Absolute, Percent, Default }

        // mode 別に 5 個並び spawn。各 sprite は 8×8 W グリッド (Riptide 本体と同形)。
        public static int SpawnRow(Vector2 origin, Mode mode)
        {
            (string Size, string Label)[] cases;
            float spacing;
            switch (mode)
            {
                case Mode.Percent:
                    cases = [("100%", "100%"), ("150%", "150%"), ("200%", "200%"), ("250%", "250%"), ("300%", "300%")];
                    spacing = 25f;
                    break;
                case Mode.Default:
                    // タグ無しデフォルト 5 個 (全部同じサイズ、間隔比較用)
                    cases = [("", "default"), ("", "default"), ("", "default"), ("", "default"), ("", "default")];
                    spacing = 8f;
                    break;
                default: // Absolute
                    cases = [("20", "20"), ("30", "30"), ("40", "40"), ("50", "50"), ("80", "80")];
                    spacing = 30f;
                    break;
            }

            for (int i = 0; i < cases.Length; i++)
            {
                Vector2 pos = origin + new Vector2(i * spacing, 0f);
                _ = new RiptideWaveSizeTestCNO(pos, cases[i].Size);
            }
            return cases.Length;
        }

        // 単発 spawn (任意 size 指定子を 1 個だけ player 横に置く)
        public static void SpawnOne(Vector2 origin, string sizeSpec)
        {
            _ = new RiptideWaveSizeTestCNO(origin, sizeSpec);
        }

        public static int DespawnAll()
        {
            int n = Active.Count;
            Active.ToArray().Do(c => c.Despawn());
            Active.Clear();
            return n;
        }
    }
}
