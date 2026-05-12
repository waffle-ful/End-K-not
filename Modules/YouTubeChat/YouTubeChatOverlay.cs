using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace EndKnot.Modules.YouTubeChat;

// HUD 右上に YouTube ライブチャットを表示する overlay。
// BGMInfoDisplay と同じく AspectPosition.ComputeWorldPosition で camera-relative anchor を維持。
//
// Manager.OnMessage は worker thread から発火するので、ここでは buffer に積むだけにして
// 実際の TMP 操作は Refresh()（main thread の Tick）で行う。
public static class YouTubeChatOverlay
{
    private static TextMeshPro displayText;
    private static readonly Queue<string> messages = new();
    private static readonly object bufferLock = new();
    private static bool dirty;
    private static bool subscribed;

    public static void EnsureSubscribed()
    {
        if (subscribed) return;
        YouTubeChatManager.OnMessage += HandleMessage;
        YouTubeChatManager.OnStatusChanged += HandleStatus;
        subscribed = true;
    }

    public static void Reset()
    {
        lock (bufferLock) messages.Clear();
        dirty = true;
        if (displayText != null) displayText.text = string.Empty;
    }

    private static void HandleMessage(string author, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // TMP 制御文字エスケープ。`<` は表示されると tag として解釈されてしまう
        // ので半角不等号は全角に潰す（簡易だが十分）。
        string safeText = SanitizeForTmp(text);
        string safeAuthor = SanitizeForTmp(author);

        string line = YouTubeChatOptions.ShowAuthor != null && YouTubeChatOptions.ShowAuthor.GetBool() && !string.IsNullOrEmpty(safeAuthor)
            ? $"<color=#FFAA00>{safeAuthor}</color>: {safeText}"
            : safeText;

        int max = YouTubeChatOptions.DisplayCount?.GetInt() ?? 5;

        lock (bufferLock)
        {
            messages.Enqueue(line);
            while (messages.Count > max) messages.Dequeue();
            dirty = true;
        }
    }

    private static void HandleStatus(string status)
    {
        // 現状は無視。将来的にエラー表示などに使う想定。
    }

    private static string SanitizeForTmp(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("<", "＜").Replace("\n", " ").Replace("\r", " ");
    }

    // FixedUpdateCaller から HudManager 存在時に呼ばれる main-thread コールバック。
    public static void Tick()
    {
        try
        {
            if (YouTubeChatOptions.Enabled == null || !YouTubeChatOptions.Enabled.GetBool())
            {
                if (displayText != null && displayText.gameObject.activeSelf) displayText.gameObject.SetActive(false);
                return;
            }

            // option ON で初めて呼ばれた瞬間に必ず購読する。
            // 以前は /yt 実行成功時のみ購読していたためヒント UI を出す前段で漏れていた。
            EnsureSubscribed();

            if (YouTubeChatOptions.HideDuringMeetings.GetBool() && GameStates.IsMeeting)
            {
                if (displayText != null && displayText.gameObject.activeSelf) displayText.gameObject.SetActive(false);
                return;
            }

            // option ON だが Manager が動いていない場合 → /yt 入力を促すヒント表示
            // option ON で Manager 動作中だがメッセージ未着の場合 → 待機表示
            // option ON で Manager 動作中かつメッセージあり → 通常表示
            string overrideText = null;
            if (!YouTubeChatManager.IsActive)
            {
                overrideText = "<color=#888888><size=80%>▶ /yt &lt;配信URL&gt; で開始</size></color>";
            }
            else if (messages.Count == 0)
            {
                string vid = YouTubeChatManager.CurrentVideoId;
                overrideText = $"<color=#888888><size=80%>📺 取得中... ({vid})</size></color>";
            }

            EnsureDisplay();
            if (displayText == null) return;

            if (!displayText.gameObject.activeSelf) displayText.gameObject.SetActive(true);

            if (overrideText != null)
            {
                if (displayText.text != overrideText)
                {
                    displayText.text = overrideText;
                    displayText.ForceMeshUpdate();
                }
            }
            else if (dirty)
            {
                dirty = false;
                RebuildText();
            }

            AnchorToCamera();
        }
        catch (Exception ex) { Utils.ThrowException(ex); }
    }

    private static void RebuildText()
    {
        var sb = new StringBuilder();
        lock (bufferLock)
        {
            foreach (var line in messages)
            {
                sb.Append(line).Append('\n');
            }
        }

        if (displayText != null)
        {
            displayText.text = sb.ToString();
            displayText.ForceMeshUpdate();
        }
    }

    private static void EnsureDisplay()
    {
        if (displayText != null && displayText.gameObject != null) return;
        displayText = null;

        TextMeshPro template = null;
        if (HudManager.InstanceExists && HudManager.Instance.KillButton != null)
            template = HudManager.Instance.KillButton.cooldownTimerText;
        else
            template = UnityEngine.Object.FindObjectOfType<TextMeshPro>();

        if (template == null) return;

        displayText = UnityEngine.Object.Instantiate(template);

        // BGMInfoDisplay と同じ初期化: stale mesh を消してから設定。
        displayText.gameObject.SetActive(false);
        displayText.text = string.Empty;
        displayText.gameObject.name = "YouTubeChatOverlay";
        displayText.DestroyTranslator();

        displayText.transform.SetParent(null, false);
        var inheritedAp = displayText.GetComponent<AspectPosition>();
        if (inheritedAp != null) UnityEngine.Object.Destroy(inheritedAp);

        displayText.alignment = TextAlignmentOptions.TopRight;
        displayText.fontStyle = FontStyles.Normal;
        displayText.fontSize = displayText.fontSizeMax = displayText.fontSizeMin = 1.8f;
        displayText.color = Color.white;
        displayText.transform.localScale = Vector3.one;
        displayText.outlineWidth = 0.15f;
        displayText.outlineColor = new Color32(0, 0, 0, 200);

        var rt = displayText.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(7f, 5f);
            var anchor = new Vector2(0.5f, 0.5f);
            rt.anchorMax = anchor;
            rt.anchorMin = anchor;
        }

        displayText.overflowMode = TextOverflowModes.Overflow;
        displayText.enableWordWrapping = true;
        displayText.sortingOrder = 100;
        displayText.ForceMeshUpdate();
    }

    private static void AnchorToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || displayText == null) return;
        // BGM クレジット (y=0.9) の下に配置。両方ON時の重なりを避ける。
        Vector3 offset = new(0.4f, 1.5f, cam.nearClipPlane + 0.1f);
        displayText.transform.position = AspectPosition.ComputeWorldPosition(cam, AspectPosition.EdgeAlignments.RightTop, offset);
    }
}
