using UnityEngine;

namespace EndKnot
{
    internal sealed class SandboxBlock : CustomNetObject
    {
        public byte OwnerId { get; }

        public SandboxBlock(Vector2 position, byte ownerId)
        {
            OwnerId = ownerId;
            Position = position;
            CreateNetObject("<size=126%><line-height=67%>" +
                "<#5a5a5a>█<#5a5a5a>█<#5a5a5a>█<#5a5a5a>█<#5a5a5a>█<#5a5a5a>█<br>" +
                "<#5a5a5a>█<#999999>█<#999999>█<#999999>█<#999999>█<#5a5a5a>█<br>" +
                "<#5a5a5a>█<#999999>█<#bbbbbb>█<#bbbbbb>█<#999999>█<#5a5a5a>█<br>" +
                "<#5a5a5a>█<#999999>█<#bbbbbb>█<#bbbbbb>█<#999999>█<#5a5a5a>█<br>" +
                "<#5a5a5a>█<#999999>█<#999999>█<#999999>█<#999999>█<#5a5a5a>█<br>" +
                "<#5a5a5a>█<#5a5a5a>█<#5a5a5a>█<#5a5a5a>█<#5a5a5a>█<#5a5a5a>█<br>" +
                "</line-height></size>", position);
        }

        // 固定位置のため毎フレーム SnapTo を送らない (帯域節約)
        protected override void OnFixedUpdate() { }

        public override void OnMeeting() => Despawn();
    }
}
