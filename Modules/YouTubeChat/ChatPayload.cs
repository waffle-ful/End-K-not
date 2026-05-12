using System.Text.Json.Nodes;

namespace EndKnot.Modules.YouTubeChat;

// YouTube 内部 endpoint (/youtubei/v1/live_chat/get_live_chat) の POST body 構築と
// continuation トークン更新を担当。TOHK の Modules/Streamer/ChatData.cs を起源とする。
internal class ChatPayload
{
    public readonly string Key;
    public string Continuation { get; private set; }
    public readonly string VisitorData;
    public readonly string ClientVersion;

    public int? PollingIntervalMillis { get; private set; }

    public ChatPayload(string key, string continuation, string visitorData, string clientVersion)
    {
        Key = key;
        Continuation = continuation;
        VisitorData = visitorData;
        ClientVersion = clientVersion;
    }

    public void UpdateContinuation(string postResult)
    {
        var node = JsonNode.Parse(postResult);
        var contRoot = node?["continuationContents"]?["liveChatContinuation"]?["continuations"]?[0];
        if (contRoot == null) return;

        var contNode = contRoot["invalidationContinuationData"] ?? contRoot["timedContinuationData"];
        if (contNode == null) return;

        var newCont = contNode["continuation"]?.ToString();
        if (!string.IsNullOrEmpty(newCont)) Continuation = newCont;

        // YouTube 側から「これ以下で叩くな」と返ってくる timeoutMs を尊重する。
        // exists in both invalidation/timed continuations as "timeoutMs".
        var timeoutMs = contNode["timeoutMs"]?.GetValue<int?>();
        if (timeoutMs is > 0) PollingIntervalMillis = timeoutMs;
    }

    public string Build()
    {
        return $"{{\"context\":{{\"client\":{{\"visitorData\":\"{VisitorData}\",\"clientName\":\"WEB\",\"clientVersion\":\"{ClientVersion}\"}}}},\"continuation\":\"{Continuation}\"}}";
    }
}
