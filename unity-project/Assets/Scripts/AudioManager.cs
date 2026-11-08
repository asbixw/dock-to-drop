using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public AudioSource voice;
    public AudioSource sfx;
    public AudioSource footsteps;
    public AudioSource engine;
    public AudioSource music;
    public AudioSource horn;

    // The music sits under the world, then steps back while somebody is talking.
    public float musicBaseVolume = 0.20f;
    public float musicDuckVolume = 0.05f;

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    Coroutine voiceRoutine;
    Coroutine engineFade;
    Coroutine musicFade;
    int lastFootstep = -1;

    void Awake()
    {
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voice.volume = 1f;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        sfx.volume = 0.8f;

        // Footsteps get their own source: PlayOneShot pitch is a property of the
        // source, so sharing it with doors/horn would bend those sounds too.
        footsteps = gameObject.AddComponent<AudioSource>();
        footsteps.playOnAwake = false;
        footsteps.spatialBlend = 0f;
        footsteps.volume = 1f;

        // Engine gets its own child object so the low-pass filter colours only the
        // diesel loop, never the dispatch voice or sound effects.
        var engineGo = new GameObject("EngineAudio");
        engineGo.transform.SetParent(transform, false);
        engine = engineGo.AddComponent<AudioSource>();
        engine.playOnAwake = false;
        engine.loop = true;
        engine.spatialBlend = 0f;
        engine.volume = 0f;

        // Calm piano bed. It is deliberately quiet and loops seamlessly; the old
        // "wind" pad was removed because it read as a constant electrical buzz.
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        music.spatialBlend = 0f;
        music.volume = 0f;

        horn = gameObject.AddComponent<AudioSource>();
        horn.playOnAwake = false;
        horn.spatialBlend = 0f;
        horn.volume = 0.85f;

        LoadAll();
    }

    void LoadAll()
    {
        string[] ids = {
            "dispatch_01","dispatch_02","dispatch_03","dispatch_04","dispatch_05",
            "guard_01","guard_02","receiver_01","receiver_02","coach_01","alert_01"
        };
        var missing = new List<string>();
        foreach (var id in ids)
        {
            var c = Resources.Load<AudioClip>("Voice/" + id);
            if (c != null) clips[id] = c;
            else missing.Add("Voice/" + id);
        }
        foreach (var id in new[] {
            "engine_idle", "air_brake", "horn", "door_open", "door_close", "ignition",
            "gate_motor", "camera_shutter", "seat", "ring", "fifth_wheel_lock",
            "glad_hand", "crank_ratchet", "trailer_air", "music_loop",
            "footstep_1", "footstep_2", "footstep_3", "footstep_4", "footstep_5", "footstep_6"
        })
        {
            var c = Resources.Load<AudioClip>("Audio/" + id);
            if (c != null) clips[id] = c;
            else missing.Add("Audio/" + id);
        }
        if (missing.Count > 0)
            Debug.LogWarning("D2D AUDIO missing: " + string.Join(", ", missing));
        else
            Debug.Log("D2D AUDIO loaded=" + clips.Count);
    }

    public AudioClip Get(string id)
    {
        if (clips.TryGetValue(id, out var c)) return c;
        return null;
    }

    public void PlaySfx(string id, float volume = 1f, float pitch = 1f)
    {
        var c = Get(id);
        if (c == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(c, volume);
    }

    // Six real boot impacts, random pick and a little pitch scatter.  The player
    // should hear weight under each step, not the same synthetic tick 120 times.
    public void PlayFootstep(float volume = 0.55f)
    {
        int count = 6;
        int i = UnityEngine.Random.Range(0, count);
        if (i == lastFootstep) i = (i + 1) % count;
        lastFootstep = i;
        var c = Get("footstep_" + (i + 1));
        if (c == null) return;
        footsteps.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
        footsteps.PlayOneShot(c, volume);
    }

    public void PlayVoice(string id, Action onDone = null)
    {
        if (voiceRoutine != null) StopCoroutine(voiceRoutine);
        voiceRoutine = StartCoroutine(VoiceRoutine(new[] { id }, onDone));
    }

    public void PlayVoiceQueue(string[] ids, Action onDone = null)
    {
        if (voiceRoutine != null) StopCoroutine(voiceRoutine);
        voiceRoutine = StartCoroutine(VoiceRoutine(ids, onDone));
    }

    IEnumerator VoiceRoutine(string[] ids, Action onDone)
    {
        DuckMusic(true);
        foreach (var id in ids)
        {
            var c = Get(id);
            if (c == null) continue;
            voice.clip = c;
            voice.Play();
            yield return new WaitWhile(() => voice.isPlaying);
        }
        DuckMusic(false);
        onDone?.Invoke();
        voiceRoutine = null;
    }

    public void StopVoice()
    {
        if (voiceRoutine != null) StopCoroutine(voiceRoutine);
        voice.Stop();
        voiceRoutine = null;
        DuckMusic(false);
    }

    // Called when the player actually starts the shift. The title screen stays
    // silent, as the design asks; the piano only arrives with the job.
    public void StartMusic()
    {
        var c = Get("music_loop");
        if (c == null) return;
        music.clip = c;
        music.loop = true;
        music.volume = 0f;
        music.Play();
        Debug.Log("D2D AUDIO music=" + c.length.ToString("F1") + "s");
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeMusic(musicBaseVolume, 5f));
    }

    public void DuckMusic(bool duck)
    {
        if (music == null || !music.isPlaying) return;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeMusic(duck ? musicDuckVolume : musicBaseVolume, duck ? 0.35f : 0.9f));
    }

    IEnumerator FadeMusic(float target, float seconds)
    {
        float from = music.volume;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
            music.volume = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        music.volume = target;
        musicFade = null;
    }

    public void StartEngine()
    {
        var c = Get("engine_idle");
        if (c == null) return;
        PlaySfx("ignition", 0.9f);
        engine.clip = c;
        engine.loop = true;
        engine.pitch = 0.94f;
        engine.volume = 0f;
        // Add the cab low-pass only once there is a clip. Unity warns about a
        // filtered AudioSource with no clip if the filter is added in Awake.
        var filter = engine.GetComponent<AudioLowPassFilter>();
        if (filter == null)
        {
            filter = engine.gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 4200f;
            filter.lowpassResonanceQ = 0.7f;
        }
        engine.Play();
        if (engineFade != null) StopCoroutine(engineFade);
        engineFade = StartCoroutine(EngineStartRoutine());
    }

    IEnumerator EngineStartRoutine()
    {
        // Let the starter crank and the engine catch before the idle bed arrives.
        yield return new WaitForSeconds(1.15f);
        yield return FadeEngine(0.26f, 1.4f);
    }

    IEnumerator FadeEngine(float target, float seconds)
    {
        float from = engine.volume;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
            engine.volume = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        engine.volume = target;
        engineFade = null;
    }

    public void StopEngine()
    {
        if (engineFade != null) { StopCoroutine(engineFade); engineFade = null; }
        engine.Stop();
        PlaySfx("air_brake", 0.65f);
    }

    public void SetEngineLoad(float speed01, bool throttle)
    {
        if (!engine.isPlaying) return;
        float targetPitch = Mathf.Lerp(0.94f, 1.22f, speed01);
        if (throttle) targetPitch += 0.04f;
        engine.pitch = Mathf.Lerp(engine.pitch, targetPitch, Time.deltaTime * 2.5f);
        if (engineFade != null) return;
        engine.volume = Mathf.Lerp(engine.volume, 0.22f + speed01 * 0.11f, Time.deltaTime * 2f);
    }

    public void AirBrake(float volume = 0.7f) { PlaySfx("air_brake", volume); }
    public void Horn() { var c = Get("horn"); if (c != null) horn.PlayOneShot(c, 0.8f); }
    public void Door(bool open) { PlaySfx(open ? "door_open" : "door_close", open ? 0.65f : 0.85f); }
    public void Ignition() { PlaySfx("ignition", 0.9f); }
    public void Shutter() { PlaySfx("camera_shutter", 0.9f); }
}
