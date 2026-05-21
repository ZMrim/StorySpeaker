using System;
using System.IO;
using UnityEngine;
using Verse;

namespace StorySpeaker;

// Unity AudioClip 创建与播放
public static class AudioPlayer
{
    private static GameObject audioHost;
    private static AudioSource audioSource;

    private static void EnsureHost()
    {
        if (audioHost == null)
        {
            var existing = GameObject.Find("StorySpeaker_AudioPlayer");
            if (existing != null)
            {
                audioHost = existing;
                audioSource = existing.GetComponent<AudioSource>();
            }
        }
        if (audioHost == null)
        {
            audioHost = new GameObject("StorySpeaker_AudioPlayer");
            UnityEngine.Object.DontDestroyOnLoad(audioHost);
        }
        if (audioSource == null)
        {
            audioSource = audioHost.GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = audioHost.AddComponent<AudioSource>();
        }
    }

    // 从 WAV 字节创建 AudioClip 并播放
    public static void Play(byte[] wavBytes, int sampleRate = 0)
    {
        if (wavBytes == null || wavBytes.Length == 0)
        {
            Log.Warning("[StorySpeaker] 无法播放: WAV 数据为空");
            return;
        }

        EnsureHost();

        try
        {
            AudioClip clip = WAVToAudioClip(wavBytes, sampleRate);
            if (clip == null) return;

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();

            Log.Message($"[StorySpeaker] 播放音频: {clip.length:F1}s, sampleRate={clip.frequency}");
        }
        catch (Exception ex)
        {
            Log.Error($"[StorySpeaker] 音频播放失败: {ex.Message}");
        }
    }

    // 停止播放
    public static void Stop()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    // 是否正在播放
    public static bool IsPlaying => audioSource != null && audioSource.isPlaying;

    // WAV 字节 → AudioClip
    // WAV 格式: 44 字节头部 + PCM 数据
    private static AudioClip WAVToAudioClip(byte[] wavBytes, int forceSampleRate = 0)
    {
        // 解析 WAV 头部
        int channels = BitConverter.ToInt16(wavBytes, 22);
        int frequency = BitConverter.ToInt32(wavBytes, 24);
        int bitDepth = BitConverter.ToInt16(wavBytes, 34);
        int dataSize = BitConverter.ToInt32(wavBytes, 40);
        int dataOffset = 44; // 标准 PCM WAV 头部大小

        if (forceSampleRate > 0) frequency = forceSampleRate;

        // 只支持 16-bit PCM
        if (bitDepth != 16)
        {
            Log.Warning($"[StorySpeaker] 不支持的位深度: {bitDepth}，需要 16-bit PCM WAV");
            return null;
        }

        // 转换为 Unity float 样本
        int sampleCount = dataSize / (bitDepth / 8);
        float[] samples = new float[sampleCount];
        int sampleIdx = 0;
        for (int i = dataOffset; i < dataOffset + dataSize - 1; i += 2)
        {
            short value = BitConverter.ToInt16(wavBytes, i);
            samples[sampleIdx++] = value / 32768f;
        }

        AudioClip clip = AudioClip.Create("StorySpeaker_TTS_" + DateTime.Now.Ticks,
            sampleCount / channels, channels, frequency, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
