using System;
using UnityEngine;

namespace EndKnot
{
    internal sealed class EvilJumperMark : CustomNetObject
    {
        private static readonly (int LineHeight, int Size)[] SizeTable =
        [
            (1400, 1200),
            (2400, 1900),
            (2700, 2200),
            (3000, 2800),
            (3500, 3200),
        ];

        public EvilJumperMark(Vector2 position, int rangeIndex)
        {
            (int lh, int sz) = SizeTable[Math.Clamp(rangeIndex - 1, 0, 4)];
            CreateNetObject($"<size={sz}%><color=#ff1919>●</color></size>", position);
        }

        public override void OnMeeting()
        {
            Despawn();
        }
    }
}
