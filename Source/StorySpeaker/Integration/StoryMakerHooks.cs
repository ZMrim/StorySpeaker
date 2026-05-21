using System.Collections.Generic;
using StoryMaker.Action;
using StoryMaker.Dialogue;
using StoryMaker.Response;
using StoryMaker.Schedule;
using Verse;

namespace StorySpeaker;

// 注册 StoryMaker 钩子，桥接 StoryMaker 事件与 StorySpeaker TTS
public static class StoryMakerHooks
{
    private static bool registered;

    public static void Register()
    {
        if (registered) return;
        registered = true;

        // 事件路径
        EventScheduler.OnEventsPlanned += OnEventsPlanned;
        ActionExecutor.OnEventWillExecute += OnEventWillExecute;
        ActionExecutor.OnEventExecuted += OnEventExecuted;
        EventScheduler.OnEventCancelled += OnEventCancelled;

        // 对话路径
        DialogueHandler.OnGenerateTTS += OnDialogueGenerateTTS;

        // 底部面板扩展
        StoryMaker.UI.StoryMakerBottomTab.OnDrawExtensions += DrawBottomBarExtension;

        Log.Message("[StorySpeaker] 已注册 StoryMaker 钩子 (事件 + 对话 + 底部面板)");
    }

    // ── 事件入队回调 ──

    private static void OnEventsPlanned(List<PlannedEvent> events)
    {
        if (events == null || events.Count == 0) return;

        Log.Message($"[StorySpeaker] OnEventsPlanned: {events.Count} 个事件");

        var state = StorySpeaker.Instance?.State;
        if (state != null)
        {
            // 将新事件 ID 加入活跃集合
            var newIds = new List<string>();
            foreach (var evt in events)
                if (!string.IsNullOrEmpty(evt?.event_id))
                    newIds.Add(evt.event_id);
            state.AddActiveEventIds(newIds);

            // 扫描清理不再活跃的音频
            state.ScanOrphanAudio();
        }

        // 将所有 narration_text 加入 TTS 队列
        foreach (var evt in events)
        {
            if (!string.IsNullOrWhiteSpace(evt.narration_text))
            {
                string text = evt.narration_text.Trim();
                string eventId = evt.event_id;

                TTSRequestQueue.Enqueue(eventId, text, wavBytes =>
                {
                    if (wavBytes != null && wavBytes.Length > 0)
                    {
                        state?.StoreAudio(eventId, wavBytes);
                    }
                });
            }
        }
    }

    // ── 事件执行前回调 ──

    private static bool OnEventWillExecute(PlannedEvent evt)
    {
        if (evt == null) return true;
        var state = StorySpeaker.Instance?.State;

        // 如果当前正在生成的就是这个事件的音频，打断它
        if (TTSRequestQueue.IsProcessing && TTSRequestQueue.CurrentItem?.eventId == evt.event_id)
        {
            Log.Warning($"[StorySpeaker] 事件 {evt.event_id} 即将执行，TTS 生成未完成，已打断。");
            TTSRequestQueue.InterruptCurrent();
        }

        // 尝试播放已缓存的音频
        byte[] wav = state?.GetAudio(evt.event_id);
        if (wav != null && wav.Length > 0)
        {
            AudioPlayer.Play(wav);
        }
        else
        {
            Log.Message($"[StorySpeaker] 事件 {evt.event_id} 无对应音频，跳过语音。");
        }

        return true; // 永不阻止事件执行
    }

    // ── 事件执行后回调 ──

    private static void OnEventExecuted(PlannedEvent evt)
    {
        if (evt == null) return;
        StorySpeaker.Instance?.State?.RemoveAudio(evt.event_id);
    }

    // ── 事件取消回调 ──

    private static void OnEventCancelled(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        Log.Message($"[StorySpeaker] 事件已取消，清理音频: {eventId}");
        StorySpeaker.Instance?.State?.RemoveAudio(eventId);
    }

    // ── 对话 TTS 回调 ──

    private static bool OnDialogueGenerateTTS(string dialogueText)
    {
        var settings = StorySpeaker.Instance?.Settings;
        if (settings == null || !settings.enableDialogueTTS)
        {
            Log.Message("[StorySpeaker] 对话 TTS 已关闭，跳过音频生成。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(dialogueText))
        {
            Log.Message("[StorySpeaker] 对话文本为空，跳过音频生成。");
            return false;
        }

        Log.Message($"[StorySpeaker] Dialogue TTS 入队: ({dialogueText.Length} 字符)");

        TTSRequestQueue.EnqueuePriority("dialogue", dialogueText.Trim(), wavBytes =>
        {
            if (wavBytes != null && wavBytes.Length > 0)
            {
                AudioPlayer.Play(wavBytes);
            }
            DialogueHandler.NotifyTTSReady();
        });

        return true;
    }

    // ── 底部面板扩展 ──

    private static void DrawBottomBarExtension(Verse.Listing_Standard listing)
    {
        var state = StorySpeaker.Instance?.State;
        if (state == null) return;

        listing.GapLine();

        // TTS 连接状态
        UnityEngine.GUI.color = state.IsConnected ? UnityEngine.Color.green : UnityEngine.Color.red;
        string statusText = state.IsConnected
            ? "StorySpeaker_Status_Connected".Translate()
            : "StorySpeaker_Status_Disconnected".Translate();
        listing.Label("StorySpeaker_BottomBar_Status".Translate(statusText));
        UnityEngine.GUI.color = UnityEngine.Color.white;

        if (state.IsConnected)
        {
            listing.Label("StorySpeaker_BottomBar_Lora".Translate(
                state.LoraWeightsLoaded ? state.CurrentLoraName ?? "(?)" : "无"));
        }

        // 连接按钮
        string btnLabel = state.IsConnected
            ? "StorySpeaker_BottomBar_Reconnect".Translate()
            : "StorySpeaker_BottomBar_Connect".Translate();
        if (listing.ButtonText(btnLabel))
            StorySpeakerManager.Connect();
    }
}
