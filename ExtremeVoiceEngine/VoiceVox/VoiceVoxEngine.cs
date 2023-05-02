﻿using System.Collections;

using System.Threading;

using UnityEngine;
using Newtonsoft.Json.Linq;

using ExtremeRoles.Extension.Json;

using ExtremeVoiceEngine.Interface;
using ExtremeVoiceEngine.Utility;

namespace ExtremeVoiceEngine.VoiceVox;

public sealed class VoiceVoxEngine : IParametableEngine<VoiceVoxParameter>
{
    public float Wait { get; set; }
    public AudioSource? Source { get; set; }
    
    private VoiceVoxParameter? param;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private int speakerId = 0;

    private static CancellationToken cancellationToken => default(CancellationToken);

    public void Cancel()
    {
        this.cts.Cancel();
        if (Source != null)
        {
            Source.Stop();
            Source.clip = null;
        }
    }

    public void SetParameter(VoiceVoxParameter param)
    {
        var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken).Token;
        
        string jsonStr = VoiceVoxBridge.GetVoice(linkedToken).GetAwaiter().GetResult();
        JObject json = JObject.Parse(jsonStr);

        for (int i = 0; i < json.Count; ++i)
        {
            JObject? speakerInfo = json.ChildrenTokens[i].TryCast<JObject>();
            
            if (speakerInfo == null) { continue; }

            JProperty? nameProp = speakerInfo.Get<JProperty>("name");
            if (nameProp == null) { continue; }

            string name = nameProp.ToString();
            if (name != param.Speaker) { continue; }

            JArray? styles = speakerInfo.Get<JArray>("styles");
            if (styles == null) { continue; }

            for (int j = 0; j < styles.Count; ++j)
            {
                JObject? styleData = styles.ChildrenTokens[i].TryCast<JObject>();
                JProperty? styleNameProp = styleData.Get<JProperty>("name");
                if (styleNameProp == null) { continue; }

                string styleName = styleNameProp.ToString();
                if (styleName != param.Style) { continue; }

                JProperty? idProp = styleData.Get<JProperty>("id");
                if (idProp == null) { continue; }

                this.speakerId = (int)idProp;
                return;
            }
        }

    }

    public IEnumerator Speek(string text)
    {
        if (param is null) { yield break; }
        if (Source == null)
        {
            var source = ISpeakEngine.CreateAudioMixer();
            if (source == null)
            {
                yield break;
            }
            Source = source;
        }

        var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken).Token;

        var jsonQueryTask = VoiceVoxBridge.PostAudioQueryAsync(this.speakerId, text, linkedToken);
        yield return TaskHelper.CoRunWaitAsync(jsonQueryTask);

        var streamTask = VoiceVoxBridge.PostSynthesisAsync(this.speakerId, jsonQueryTask.Result, linkedToken);
        yield return TaskHelper.CoRunWaitAsync(streamTask);

        using var stream = streamTask.Result;

        var audioClipTask = AudioClipHelper.CreateFromStreamAsync(stream, linkedToken);
        yield return TaskHelper.CoRunWaitAsync(audioClipTask);

        Source.volume = param.MasterVolume;
        Source.clip = audioClipTask.Result;
        Source.Play();
        
        while (Source.isPlaying)
        {
            yield return null;
        }
        yield break;
    }
}
