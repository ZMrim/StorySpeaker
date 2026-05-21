using Verse;

namespace StorySpeaker;

public class StorySpeakerSettings : ModSettings
{
    // 服务端连接
    public string serverUrl = "http://127.0.0.1:8809";

    // TTS 生成参数
    public float cfgValue = 2.0f;
    public int inferenceTimesteps = 10;

    // 语音克隆参考音频路径（空 = 普通模式）
    public string cloneReferencePath = "";

    // 对话 TTS 开关
    public bool enableDialogueTTS = false;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref serverUrl, "serverUrl", "http://127.0.0.1:8809");
        Scribe_Values.Look(ref cfgValue, "cfgValue", 2.0f);
        Scribe_Values.Look(ref inferenceTimesteps, "inferenceTimesteps", 10);
        Scribe_Values.Look(ref cloneReferencePath, "cloneReferencePath", "");
        Scribe_Values.Look(ref enableDialogueTTS, "enableDialogueTTS", false);
    }
}
