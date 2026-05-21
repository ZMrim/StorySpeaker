using System.Collections.Generic;
using Verse;

namespace StorySpeaker;

// 连接管理、LoRA 管理
public static class StorySpeakerManager
{
    // 连接服务端
    public static void Connect()
    {
        string url = StorySpeaker.Instance?.Settings?.serverUrl ?? "http://127.0.0.1:8809";
        var state = StorySpeaker.Instance?.State;
        if (state == null) return;

        if (StorySpeaker.CoroutineHandler == null)
        {
            Log.Warning("[StorySpeaker] CoroutineHandler 未初始化，无法连接。");
            return;
        }

        Log.Message($"[StorySpeaker] 尝试连接: {url}");

        StorySpeaker.CoroutineHandler.CheckHealth(url, (loaded, sr) =>
        {
            if (loaded)
            {
                state.IsConnected = true;
                state.SampleRate = sr;
                Log.Message($"[StorySpeaker] 连接成功: sample_rate={sr}");
                RefreshLoraList();
            }
            else
            {
                state.IsConnected = false;
                Log.Warning("[StorySpeaker] 连接失败: 服务端模型未加载");
            }
        });
    }

    // 刷新 LoRA 列表
    public static void RefreshLoraList()
    {
        string url = StorySpeaker.Instance?.Settings?.serverUrl ?? "http://127.0.0.1:8809";
        var state = StorySpeaker.Instance?.State;
        if (state == null) return;

        StorySpeaker.CoroutineHandler?.GetLoraList(url, loras =>
        {
            state.AvailableLoras = loras;
            // 检查当前已加载的 LoRA
            foreach (var lora in loras)
            {
                if (lora.loaded)
                {
                    state.LoraWeightsLoaded = true;
                    state.CurrentLoraName = lora.name;
                    break;
                }
            }
            if (!state.LoraWeightsLoaded)
                state.CurrentLoraName = null;
        });
    }

    // 切换 LoRA（先卸载当前 → 加载新）
    public static void SwitchLora(string newLoraPath)
    {
        string url = StorySpeaker.Instance?.Settings?.serverUrl ?? "http://127.0.0.1:8809";
        var state = StorySpeaker.Instance?.State;
        if (state == null) return;

        // 如果有正在生成的音频，先打断
        TTSRequestQueue.InterruptCurrent();

        // 如果当前有 LoRA 已加载
        if (state.LoraWeightsLoaded)
        {
            Log.Message("[StorySpeaker] 卸载当前 LoRA...");
            StorySpeaker.CoroutineHandler?.UnloadLora(url, success =>
            {
                state.LoraWeightsLoaded = false;
                state.CurrentLoraName = null;

                if (newLoraPath != null)
                {
                    DoLoadLora(url, newLoraPath, state);
                }
            });
        }
        else if (newLoraPath != null)
        {
            DoLoadLora(url, newLoraPath, state);
        }
    }

    private static void DoLoadLora(string url, string path, StorySpeakerState state)
    {
        Log.Message($"[StorySpeaker] 加载 LoRA: {path}");
        StorySpeaker.CoroutineHandler?.LoadLora(url, path, success =>
        {
            if (success)
            {
                state.LoraWeightsLoaded = true;
                // 从可用列表中查找名称
                foreach (var lora in state.AvailableLoras ?? new List<LoraEntry>())
                {
                    if (lora.path == path)
                    {
                        state.CurrentLoraName = lora.name;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(state.CurrentLoraName))
                    state.CurrentLoraName = System.IO.Path.GetFileName(path);
            }
        });
    }
}
