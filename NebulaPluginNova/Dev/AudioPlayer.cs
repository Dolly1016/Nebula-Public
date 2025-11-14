using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Dev;

[NebulaPreprocess(PreprocessPhase.FixStructure)]
internal class AudioPlayer : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public void Preprocess(NebulaPreprocessor preprocess) => DIManager.Instance.RegisterModule(() => DebugTools.UseAudioPlayer ? new AudioPlayer() : null);
    public AudioPlayer()
    {
        ModSingleton<AudioPlayer>.Instance = this;
        this.RegisterPermanently();
    }

    string[] keys = VanillaAsset.GetAudioKeys();
    int index = 0;
    string CurrentKey => keys[index];
    AudioSource source = null!;
    void OnUpdate(GameHudUpdateEvent ev)
    {
        if (Input.GetKeyDown(KeyCode.J)) {
            index = (index + keys.Length - 1) % keys.Length;
            PlayCurrentAudio();
        }
        if (Input.GetKeyDown(KeyCode.K)) {
            ClipboardHelper.PutClipboardString(CurrentKey);
            DebugScreen.Push("クリップボードにコピーしました。", 2f);
        }
        if (Input.GetKeyDown(KeyCode.L)) {
            index = (index + 1) % keys.Length;
            PlayCurrentAudio();
        }
    }

    void PlayCurrentAudio()
    {
        DebugScreen.Push("Playing: " + CurrentKey + " (" + (index + 1) + "/" + keys.Length + ")", 3f);
        var clip = VanillaAsset.GetAudioClip(CurrentKey);
        if (!source)
        {
            source = SoundManager.instance.PlayNamedSound("AudioPlayer", clip, true, SoundManager.instance.SfxChannel);
        }
        else
        {
            if(source.isPlaying) source.Stop();
            source.clip = clip;
            source.loop = true;
            source.Play();
        }
    }
}