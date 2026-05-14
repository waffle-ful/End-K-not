using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EndKnot
{
    // Debug-only CNO for visually verifying <size=%> rendering limits on non-modded clients.
    // /sizetest スポーンで使用、/sizeclean で全消去。
    internal sealed class SizeTestCNO : CustomNetObject
    {
        public static readonly List<SizeTestCNO> Active = [];

        public SizeTestCNO(Vector2 position, int sizePercent, string label)
        {
            // <font="VCR SDF"><line-height=67%> ラップは AdventurerItem 等の動作実績パターン。
            // 〇 (U+3007) は VCR SDF に無く fallback で size 不変になる事案を観測したため、
            // ○ (○ U+25CB) を使用 (Adventurer Grouping アイコンと同じ)
            CreateNetObject(
                $"<size={sizePercent}%><font=\"VCR SDF\"><line-height=67%><color=#4488ff>○</color></line-height></font></size>",
                position);
            Active.Add(this);
        }

        public override void OnMeeting() => Despawn();

        public static int SpawnRow(Vector2 origin)
        {
            // 5 個を横並びで spawn。各間隔 4 unit (重なり防止)
            (int Size, string Label)[] cases =
            [
                (600, "600%"),
                (800, "800%"),
                (1000, "1000%"),
                (1200, "1200%"),
                (1500, "1500%"),
            ];

            for (int i = 0; i < cases.Length; i++)
            {
                Vector2 pos = origin + new Vector2(i * 4f, 0f);
                _ = new SizeTestCNO(pos, cases[i].Size, cases[i].Label);
            }

            return cases.Length;
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
