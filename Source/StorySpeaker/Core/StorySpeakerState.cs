using System.Collections.Generic;

namespace StorySpeaker;

// LoRA 条目（从服务端 /lora/list 获取）
public class LoraEntry
{
    public string name;
    public string path;
    public int rank;
    public int alpha;
    public bool loaded;
}

// 运行时状态（不序列化到存档）
public class StorySpeakerState
{
    public bool IsConnected;
    public int SampleRate;
    public bool LoraWeightsLoaded;
    public string CurrentLoraName;
    public List<LoraEntry> AvailableLoras;

    // 音频缓存（event_id → WAV bytes），仅运行时不序列化
    public Dictionary<string, byte[]> AudioCache = new Dictionary<string, byte[]>();
    // 当前活跃的事件 ID 集合（OnEventsPlanned 添加，OnEventExecuted/OnEventCancelled 移除）
    private HashSet<string> activeEventIds = new HashSet<string>();

    // 缓存上限
    public const int MaxCacheEntries = 20;

    public void StoreAudio(string eventId, byte[] wavBytes)
    {
        if (wavBytes == null || wavBytes.Length == 0) return;
        if (AudioCache.Count >= MaxCacheEntries)
        {
            string oldest = null;
            foreach (var kv in AudioCache)
            {
                oldest = kv.Key;
                break;
            }
            if (oldest != null)
            {
                AudioCache.Remove(oldest);
                Verse.Log.Warning($"[StorySpeaker] 音频缓存达到上限，已删除最旧条目: {oldest}");
            }
        }
        AudioCache[eventId] = wavBytes;
        Verse.Log.Message($"[StorySpeaker] 音频已缓存: {eventId} ({wavBytes.Length} bytes)");
    }

    public byte[] GetAudio(string eventId)
    {
        if (eventId == null) return null;
        AudioCache.TryGetValue(eventId, out var wav);
        return wav;
    }

    public void RemoveAudio(string eventId)
    {
        if (eventId != null && AudioCache.Remove(eventId))
            Verse.Log.Message($"[StorySpeaker] 音频已删除: {eventId}");
        activeEventIds.Remove(eventId);
    }

    // 添加事件 ID 到活跃集合（OnEventsPlanned 调用）
    public void AddActiveEventIds(List<string> eventIds)
    {
        if (eventIds == null) return;
        foreach (var id in eventIds)
            if (!string.IsNullOrEmpty(id))
                activeEventIds.Add(id);
    }

    // 扫描孤立音频（缓存中有但不在活跃事件集合中）
    public int ScanOrphanAudio()
    {
        int cleaned = 0;
        var orphans = new List<string>();
        foreach (var key in AudioCache.Keys)
        {
            if (!activeEventIds.Contains(key))
                orphans.Add(key);
        }
        foreach (var orphan in orphans)
        {
            AudioCache.Remove(orphan);
            cleaned++;
        }
        if (cleaned > 0)
            Verse.Log.Message($"[StorySpeaker] 孤立音频清理: {cleaned} 个");
        return cleaned;
    }

    public void ClearCache()
    {
        int count = AudioCache.Count;
        AudioCache.Clear();
        activeEventIds.Clear();
        if (count > 0)
            Verse.Log.Message($"[StorySpeaker] 音频缓存已清空 ({count} 个)");
    }
}
