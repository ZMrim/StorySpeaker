using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace StorySpeaker;

// MonoBehaviour + 协程管理，负责所有 TTS 异步请求
public class TTSCoroutineHandler : MonoBehaviour
{
    private readonly List<Coroutine> activeCoroutines = new List<Coroutine>();

    public void StartManagedCoroutine(IEnumerator routine)
    {
        activeCoroutines.Add(StartCoroutine(routine));
    }

    public void StopAllManagedCoroutines()
    {
        foreach (var coroutine in activeCoroutines)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
        activeCoroutines.Clear();
    }

    void OnDestroy()
    {
        StopAllManagedCoroutines();
    }

    // ── 健康检查 ──

    public void CheckHealth(string serverUrl, Action<bool, int> callback)
    {
        StartManagedCoroutine(CheckHealthCoroutine(serverUrl, callback));
    }

    private IEnumerator CheckHealthCoroutine(string serverUrl, Action<bool, int> callback)
    {
        using (var request = TTSClient.CreateHealthRequest(serverUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    int sr = ExtractInt(json, "sample_rate");
                    bool loaded = json.Contains("\"model_loaded\":true")
                        || json.Contains("\"model_loaded\": true");
                    bool loraLoaded = json.Contains("\"lora_weights_loaded\":true")
                        || json.Contains("\"lora_weights_loaded\": true");
                    Verse.Log.Message($"[StorySpeaker] 健康检查成功: sample_rate={sr}, lora_loaded={loraLoaded}");
                    callback?.Invoke(loaded, sr);
                    yield break;
                }
                catch (Exception ex)
                {
                    Verse.Log.Warning($"[StorySpeaker] 健康检查解析失败: {ex.Message}");
                }
            }
            else
            {
                string reason = TTSClient.ResolveFailureReason(request);
                Verse.Log.Warning($"[StorySpeaker] 健康检查失败: {reason}");
            }
        }
        callback?.Invoke(false, 0);
    }

    // ── LoRA 列表 ──

    public void GetLoraList(string serverUrl, Action<List<LoraEntry>> callback)
    {
        StartManagedCoroutine(GetLoraListCoroutine(serverUrl, callback));
    }

    private IEnumerator GetLoraListCoroutine(string serverUrl, Action<List<LoraEntry>> callback)
    {
        using (var request = TTSClient.CreateLoraListRequest(serverUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var loras = ParseLoraList(json);
                    Verse.Log.Message($"[StorySpeaker] LoRA 列表: {loras.Count} 个");
                    callback?.Invoke(loras);
                    yield break;
                }
                catch (Exception ex)
                {
                    Verse.Log.Warning($"[StorySpeaker] LoRA 列表解析失败: {ex.Message}");
                }
            }
            else
            {
                string reason = TTSClient.ResolveFailureReason(request);
                Verse.Log.Warning($"[StorySpeaker] LoRA 列表获取失败: {reason}");
            }
        }
        callback?.Invoke(new List<LoraEntry>());
    }

    // ── LoRA 加载/卸载 ──

    public void LoadLora(string serverUrl, string path, Action<bool> callback)
    {
        StartManagedCoroutine(LoadLoraCoroutine(serverUrl, path, callback));
    }

    private IEnumerator LoadLoraCoroutine(string serverUrl, string path, Action<bool> callback)
    {
        using (var request = TTSClient.CreateLoraLoadRequest(serverUrl, path))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Verse.Log.Message($"[StorySpeaker] LoRA 加载成功: {path}");
                callback?.Invoke(true);
                yield break;
            }
            string reason = TTSClient.ResolveFailureReason(request);
            Verse.Log.Error($"[StorySpeaker] LoRA 加载失败: {reason}");
        }
        callback?.Invoke(false);
    }

    public void UnloadLora(string serverUrl, Action<bool> callback)
    {
        StartManagedCoroutine(UnloadLoraCoroutine(serverUrl, callback));
    }

    private IEnumerator UnloadLoraCoroutine(string serverUrl, Action<bool> callback)
    {
        using (var request = TTSClient.CreateLoraUnloadRequest(serverUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Verse.Log.Message("[StorySpeaker] LoRA 已卸载");
                callback?.Invoke(true);
                yield break;
            }
            string reason = TTSClient.ResolveFailureReason(request);
            Verse.Log.Warning($"[StorySpeaker] LoRA 卸载失败: {reason}");
        }
        callback?.Invoke(false);
    }

    // ── TTS 生成 ──

    public void GenerateTTS(string serverUrl, string text, float cfgValue,
        int inferenceTimesteps, string clonePath,
        Action<byte[]> onComplete, Action<string> onError)
    {
        StartManagedCoroutine(GenerateTTSCoroutine(serverUrl, text,
            cfgValue, inferenceTimesteps, clonePath, onComplete, onError));
    }

    private IEnumerator GenerateTTSCoroutine(string serverUrl, string text,
        float cfgValue, int inferenceTimesteps, string clonePath,
        Action<byte[]> onComplete, Action<string> onError)
    {
        using (var request = TTSClient.CreateTTSRequest(serverUrl, text,
            cfgValue, inferenceTimesteps, clonePath))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] wavBytes = request.downloadHandler?.data;
                onComplete?.Invoke(wavBytes);
            }
            else
            {
                string reason = TTSClient.ResolveFailureReason(request);
                string errorMsg = $"TTS 生成失败: {reason}";
                Verse.Log.Warning($"[StorySpeaker] {errorMsg}");
                onError?.Invoke(reason);
            }
        }
    }

    // ── TTS 打断 ──

    public void InterruptTTS(string serverUrl)
    {
        StartManagedCoroutine(InterruptTTSCoroutine(serverUrl));
    }

    private IEnumerator InterruptTTSCoroutine(string serverUrl)
    {
        using (var request = TTSClient.CreateInterruptRequest(serverUrl))
        {
            yield return request.SendWebRequest();
            Verse.Log.Message("[StorySpeaker] TTS 生成打断已发送");
        }
    }

    // ── 解析辅助 ──

    private static int ExtractInt(string json, string key)
    {
        int idx = json.IndexOf($"\"{key}\"");
        if (idx < 0) return 0;
        int colon = json.IndexOf(':', idx);
        if (colon < 0) return 0;
        string rest = json.Substring(colon + 1).Trim();
        int end = 0;
        while (end < rest.Length && (char.IsDigit(rest[end]) || rest[end] == '-'))
            end++;
        if (int.TryParse(rest.Substring(0, end), out int val)) return val;
        return 0;
    }

    private static List<LoraEntry> ParseLoraList(string json)
    {
        var result = new List<LoraEntry>();
        // 简易 JSON 解析：提取 loras 数组中的每个对象
        int arrStart = json.IndexOf("\"loras\"");
        if (arrStart < 0) return result;
        int bracket = json.IndexOf('[', arrStart);
        if (bracket < 0) return result;
        int end = json.LastIndexOf(']');
        if (end < 0) return result;
        string arrBody = json.Substring(bracket + 1, end - bracket - 1);

        int pos = 0;
        while (pos < arrBody.Length)
        {
            int objStart = arrBody.IndexOf('{', pos);
            if (objStart < 0) break;
            int objEnd = arrBody.IndexOf('}', objStart);
            if (objEnd < 0) break;
            string obj = arrBody.Substring(objStart, objEnd - objStart + 1);

            var entry = new LoraEntry
            {
                name = ExtractString(obj, "name"),
                path = ExtractString(obj, "path"),
                rank = ExtractInt(obj, "rank"),
                alpha = ExtractInt(obj, "alpha"),
                loaded = obj.Contains("\"loaded\":true") || obj.Contains("\"loaded\": true")
            };
            if (!string.IsNullOrEmpty(entry.name))
                result.Add(entry);

            pos = objEnd + 1;
        }
        return result;
    }

    private static string ExtractString(string json, string key)
    {
        int idx = json.IndexOf($"\"{key}\"");
        if (idx < 0) return "";
        int colon = json.IndexOf(':', idx);
        if (colon < 0) return "";
        int quoteStart = json.IndexOf('"', colon);
        if (quoteStart < 0) return "";
        int quoteEnd = json.IndexOf('"', quoteStart + 1);
        if (quoteEnd < 0) return "";
        return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
    }
}
