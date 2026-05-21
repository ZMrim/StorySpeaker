using System;
using System.Collections.Generic;
using Verse;

namespace StorySpeaker;

// TTS 请求项
public class TTSRequestItem
{
    public string eventId;
    public string text;
    public Action<byte[]> onComplete;
}

// 串行请求队列：FIFO，逐个发送 TTS 请求
public static class TTSRequestQueue
{
    private static readonly Queue<TTSRequestItem> pending = new Queue<TTSRequestItem>();
    private static bool isProcessing;
    private static TTSRequestItem currentItem;

    // 对话优先项（插队到当前项之后）
    private static TTSRequestItem priorityItem;

    public static bool IsProcessing => isProcessing;
    public static TTSRequestItem CurrentItem => currentItem;
    public static int PendingCount => pending.Count;

    // 入队
    public static void Enqueue(string eventId, string text, Action<byte[]> onComplete)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Log.Message($"[StorySpeaker] 跳过空文本: {eventId}");
            onComplete?.Invoke(null);
            return;
        }

        var item = new TTSRequestItem { eventId = eventId, text = text, onComplete = onComplete };
        pending.Enqueue(item);
        Log.Message($"[StorySpeaker] TTS 入队: {eventId} (队列深度={pending.Count})");

        if (!isProcessing)
            ProcessNext();
    }

    // 对话优先入队（当前项完成后优先处理）
    public static void EnqueuePriority(string dialogueId, string text, Action<byte[]> onComplete)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onComplete?.Invoke(null);
            return;
        }

        priorityItem = new TTSRequestItem { eventId = dialogueId, text = text, onComplete = onComplete };
        Log.Message($"[StorySpeaker] TTS 优先入队(对话): {dialogueId}");

        if (!isProcessing)
            ProcessNext();
    }

    // 处理下一个
    private static void ProcessNext()
    {
        // 优先项优先
        if (priorityItem != null)
        {
            currentItem = priorityItem;
            priorityItem = null;
        }
        else if (pending.Count > 0)
        {
            currentItem = pending.Dequeue();
        }
        else
        {
            isProcessing = false;
            currentItem = null;
            return;
        }

        isProcessing = true;
        var settings = StorySpeaker.Instance?.Settings;
        string url = settings?.serverUrl ?? "http://127.0.0.1:8809";
        if (string.IsNullOrWhiteSpace(url))
        {
            Log.Warning("[StorySpeaker] 服务端 URL 为空，跳过 TTS 生成。");
            currentItem.onComplete?.Invoke(null);
            currentItem = null;
            ProcessNext();
            return;
        }
        float cfg = settings?.cfgValue ?? 2.0f;
        int steps = settings?.inferenceTimesteps ?? 10;
        string clonePath = settings?.cloneReferencePath ?? "";

        // 克隆路径校验：路径非空但文件不存在时回退到普通模式
        if (!string.IsNullOrWhiteSpace(clonePath) && !System.IO.File.Exists(clonePath))
        {
            Log.Warning($"[StorySpeaker] 克隆参考音频不存在 ({clonePath})，回退到普通模式。");
            clonePath = "";
        }

        Log.Message($"[StorySpeaker] TTS 生成开始: {currentItem.eventId} ({currentItem.text.Length} 字符){(string.IsNullOrEmpty(clonePath) ? "" : " [clone]")}");

        if (StorySpeaker.CoroutineHandler != null)
        {
            StorySpeaker.CoroutineHandler.GenerateTTS(url, currentItem.text,
                cfg, steps, clonePath,
                onComplete: wavBytes =>
                {
                    Log.Message($"[StorySpeaker] TTS 生成完成: {currentItem.eventId} ({wavBytes?.Length ?? 0} bytes)");
                    currentItem.onComplete?.Invoke(wavBytes);
                    currentItem = null;
                    ProcessNext();
                },
                onError: reason =>
                {
                    Log.Warning($"[StorySpeaker] TTS 生成失败: {currentItem.eventId} ({reason})");
                    currentItem.onComplete?.Invoke(null); // null 表示无音频
                    currentItem = null;
                    ProcessNext();
                });
        }
        else
        {
            Log.Warning("[StorySpeaker] CoroutineHandler 未初始化，跳过 TTS 请求");
            currentItem.onComplete?.Invoke(null);
            currentItem = null;
            ProcessNext();
        }
    }

    // 打断当前生成
    public static void InterruptCurrent()
    {
        if (!isProcessing || currentItem == null) return;

        string url = StorySpeaker.Instance?.Settings?.serverUrl ?? "http://127.0.0.1:8809";
        StorySpeaker.CoroutineHandler?.InterruptTTS(url);

        Log.Warning($"[StorySpeaker] TTS 生成被打断: {currentItem.eventId}");
        currentItem.onComplete?.Invoke(null);
        currentItem = null;
        ProcessNext();
    }

    // 清空队列
    public static void Clear()
    {
        int count = pending.Count;
        foreach (var item in pending)
            item.onComplete?.Invoke(null);
        pending.Clear();
        priorityItem = null;
        Log.Message($"[StorySpeaker] TTS 队列已清空 ({count} 个待处理)");
    }
}
