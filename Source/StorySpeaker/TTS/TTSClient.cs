using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.Networking;

namespace StorySpeaker;

// HTTP 请求封装，负责与服务端通信
public static class TTSClient
{
    private static readonly Regex SanitizeRegex = new Regex("[\"\\\\\\n\\r\\t]", RegexOptions.Compiled);

    // ── 健康检查 ──

    public static UnityWebRequest CreateHealthRequest(string serverUrl)
    {
        return UnityWebRequest.Get(serverUrl.TrimEnd('/') + "/health");
    }

    // ── LoRA 列表 ──

    public static UnityWebRequest CreateLoraListRequest(string serverUrl)
    {
        return UnityWebRequest.Get(serverUrl.TrimEnd('/') + "/lora/list");
    }

    // ── LoRA 加载/卸载 ──

    public static UnityWebRequest CreateLoraLoadRequest(string serverUrl, string loraPath)
    {
        var json = $"{{\"path\":\"{EscapeJson(loraPath)}\"}}";
        return BuildPostRequest(serverUrl.TrimEnd('/') + "/lora/load", json);
    }

    public static UnityWebRequest CreateLoraUnloadRequest(string serverUrl)
    {
        return BuildPostRequest(serverUrl.TrimEnd('/') + "/lora/unload", "{}");
    }

    // ── TTS 生成 ──

    public static UnityWebRequest CreateTTSRequest(string serverUrl, string text,
        float cfgValue, int inferenceTimesteps, string clonePath)
    {
        string json = BuildTTSBody(text, cfgValue, inferenceTimesteps, clonePath);
        return BuildPostRequest(serverUrl.TrimEnd('/') + "/tts", json);
    }

    // ── TTS 打断 ──

    public static UnityWebRequest CreateInterruptRequest(string serverUrl)
    {
        return BuildPostRequest(serverUrl.TrimEnd('/') + "/tts/interrupt", "{}");
    }

    // ── 错误分类 ──

    public static string ResolveFailureReason(UnityWebRequest request)
    {
        if (request == null) return "request_error";

        switch (request.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                return LooksLikeTimeout(request.error) ? "timeout" : "connection_error";
            case UnityWebRequest.Result.ProtocolError:
                return $"http_{(int)request.responseCode}";
            case UnityWebRequest.Result.DataProcessingError:
                return "data_processing_error";
            default:
                return "request_error";
        }
    }

    public static bool LooksLikeTimeout(string error)
    {
        string normalized = (error ?? "").ToLowerInvariant();
        return normalized.Contains("timeout") || normalized.Contains("timed out");
    }

    // ── 辅助 ──

    private static UnityWebRequest BuildPostRequest(string url, string jsonBody)
    {
        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 120;
        return request;
    }

    private static string BuildTTSBody(string text, float cfgValue,
        int inferenceTimesteps, string clonePath)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"text\":\"{EscapeJson(text)}\",");
        sb.Append($"\"cfg_value\":{cfgValue},");
        sb.Append($"\"inference_timesteps\":{inferenceTimesteps}");
        if (!string.IsNullOrWhiteSpace(clonePath))
        {
            sb.Append(",");
            sb.Append($"\"reference_wav_path\":\"{EscapeJson(clonePath)}\"");
        }
        sb.Append("}");
        return sb.ToString();
    }

    private static string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }
}
