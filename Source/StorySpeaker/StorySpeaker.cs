using System;
using UnityEngine;
using Verse;

namespace StorySpeaker;

public class StorySpeaker : Mod
{
    public static StorySpeaker Instance { get; private set; }
    public StorySpeakerSettings Settings { get; private set; }

    // 运行时状态
    public StorySpeakerState State { get; private set; }

    // 协程处理器（MonoBehaviour，DontDestroyOnLoad）
    internal static TTSCoroutineHandler CoroutineHandler;

    public StorySpeaker(ModContentPack content) : base(content)
    {
        Instance = this;
        Settings = GetSettings<StorySpeakerSettings>();
        State = new StorySpeakerState();

        // 检查 StoryMaker 是否已安装
        if (!IsStoryMakerLoaded())
        {
            Log.Warning("[StorySpeaker] StoryMaker 未安装。StorySpeaker 依赖 StoryMaker，将不会工作。");
            return;
        }

        // 注册 StoryMaker 钩子
        StoryMakerHooks.Register();

        // 游戏启动时自动尝试连接一次
        TryAutoConnect();

        Log.Message("[StorySpeaker] Mod 已加载。请确保 VoxCPM 服务端已启动。");
    }

    public override string SettingsCategory() => "StorySpeaker - TTS 语音";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        // ── 连接 ──
        listing.Label("StorySpeaker_Settings_Connection".Translate());
        if (listing.ButtonText("StorySpeaker_Settings_Connect".Translate()))
            StorySpeakerManager.Connect();

        string statusKey = State.IsConnected
            ? "StorySpeaker_Status_Connected"
            : "StorySpeaker_Status_Disconnected";
        listing.Label("StorySpeaker_Settings_Status".Translate(statusKey.Translate()));

        if (State.IsConnected)
        {
            listing.Label("StorySpeaker_Settings_SampleRate".Translate(State.SampleRate));
            listing.Label("StorySpeaker_Settings_LoraLoaded".Translate(
                State.LoraWeightsLoaded ? State.CurrentLoraName ?? "(unknown)" : "无"));
        }

        listing.Gap();
        listing.GapLine();

        // ── 服务端 URL ──
        listing.Label("StorySpeaker_Settings_ServerUrl".Translate());
        Settings.serverUrl = Widgets.TextField(listing.GetRect(Text.LineHeight), Settings.serverUrl ?? "");

        listing.Gap();

        // ── 生成参数 ──
        listing.Label("StorySpeaker_Settings_CFG".Translate(Settings.cfgValue));
        Settings.cfgValue = listing.Slider(Settings.cfgValue, 0.5f, 5.0f);

        listing.Label("StorySpeaker_Settings_Steps".Translate(Settings.inferenceTimesteps));
        Settings.inferenceTimesteps = (int)listing.Slider(Settings.inferenceTimesteps, 1f, 50f);

        listing.Gap();
        listing.GapLine();

        // ── LoRA ──
        listing.Label("StorySpeaker_Settings_Lora".Translate());
        string loraLabel = State.CurrentLoraName ?? "StorySpeaker_Lora_None".Translate();
        if (listing.ButtonText("StorySpeaker_Settings_LoraSelect".Translate(loraLabel)))
        {
            var options = new System.Collections.Generic.List<FloatMenuOption>();
            options.Add(new FloatMenuOption("StorySpeaker_Lora_None".Translate(),
                () => StorySpeakerManager.SwitchLora(null)));
            if (State.AvailableLoras != null)
            {
                foreach (var lora in State.AvailableLoras)
                {
                    var captured = lora;
                    options.Add(new FloatMenuOption(captured.name,
                        () => StorySpeakerManager.SwitchLora(captured.path)));
                }
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        if (listing.ButtonText("StorySpeaker_Settings_LoraRefresh".Translate()))
            StorySpeakerManager.RefreshLoraList();

        listing.Gap();
        listing.GapLine();

        // ── 语音克隆 ──
        listing.Label("StorySpeaker_Settings_ClonePath".Translate());
        Settings.cloneReferencePath = Widgets.TextField(
            listing.GetRect(Text.LineHeight), Settings.cloneReferencePath ?? "");

        listing.Gap();
        listing.GapLine();

        // ── 对话 TTS ──
        listing.CheckboxLabeled("StorySpeaker_Settings_DialogueTTS".Translate(),
            ref Settings.enableDialogueTTS, "StorySpeaker_Settings_DialogueTTS_Desc".Translate());

        listing.End();
        Settings.Write();
    }

    // 检查 StoryMaker 是否加载（通过其公开的 Instance 属性判断）
    private static bool IsStoryMakerLoaded()
    {
        try
        {
            var smType = System.Type.GetType("StoryMaker.StoryMaker, StoryMaker");
            if (smType == null) return false;
            var instanceProp = smType.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp == null) return false;
            return instanceProp.GetValue(null) != null;
        }
        catch { return false; }
    }

    // 启动时自动连接
    private static void TryAutoConnect()
    {
        if (CoroutineHandler == null)
        {
            // 防止场景重载时残留旧 GameObject
            var existing = GameObject.Find("StorySpeaker_CoroutineHandler");
            if (existing != null)
                CoroutineHandler = existing.GetComponent<TTSCoroutineHandler>();
            if (CoroutineHandler == null)
            {
                var go = new GameObject("StorySpeaker_CoroutineHandler");
                UnityEngine.Object.DontDestroyOnLoad(go);
                CoroutineHandler = go.AddComponent<TTSCoroutineHandler>();
            }
        }
        StorySpeakerManager.Connect();
    }
}
