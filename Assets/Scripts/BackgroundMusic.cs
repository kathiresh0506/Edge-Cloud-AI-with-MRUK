using UnityEngine;

/// <summary>
/// Background music manager. Auto-spawns at runtime (no scene wiring needed).
///
/// If you drop your OWN legally-obtained track at
///   Assets/Resources/Audio/bgm.(mp3|wav|ogg)
/// it will be used automatically. Use <see cref="startTime"/> to begin playback
/// at your favorite part of that track (e.g. the drop / chorus).
///
/// If no file is present, it plays an ORIGINAL royalty-free synthwave loop that
/// is generated in-code (an 80s-style driving progression — not any copyrighted song).
/// </summary>
public class BackgroundMusic : MonoBehaviour
{
    public static BackgroundMusic Instance { get; private set; }

    [Header("Custom Track (optional)")]
    [Tooltip("Play the user file at Assets/Resources/Audio/bgm.mp3. Off = play the built-in instrumental (no vocals).")]
    public bool useUserTrack = false;
    [Tooltip("Resources path (no extension) of the user track.")]
    public string userClipResourcePath = "Audio/bgm";

    [Tooltip("Seconds into the track to start — begins (and loops back to) the best/most-played section of your own track.")]
    public float startTime = 10f;

    [Header("Mix")]
    [Range(0f, 1f)] public float volume = 0.42f; // energetic, but still below SFX/voice

    private AudioSource source;
    private bool usingUserClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("BackgroundMusic");
        DontDestroyOnLoad(go);
        go.AddComponent<BackgroundMusic>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D
        source.volume = volume;

        // Default to the built-in instrumental (no vocals). Flip useUserTrack on to use the user file.
        AudioClip clip = useUserTrack ? Resources.Load<AudioClip>(userClipResourcePath) : null;
        usingUserClip = clip != null;
        if (clip == null) clip = GenerateSynthwaveLoop();

        source.clip = clip;
        source.loop = true;
        source.Play();
        if (usingUserClip && startTime > 0f && startTime < clip.length)
            source.time = startTime;
    }

    void Update()
    {
        // Loop the user track back to the chosen start point (e.g. 10s) instead of 0,
        // so it always replays from the best section.
        if (usingUserClip && startTime > 0.1f && source != null && source.isPlaying)
        {
            if (source.time < startTime - 0.1f)
                source.time = startTime;
        }
    }

    /// <summary>Fade/set the music volume (e.g. to duck under boss dialogue).</summary>
    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (source != null) source.volume = volume;
    }

    // ---- original synthwave loop generation ----

    AudioClip GenerateSynthwaveLoop()
    {
        const int sr = 44100;
        const float bpm = 128f;          // faster, more driving
        float beat = 60f / bpm;
        float barLen = beat * 4f;
        int bars = 8;                    // 8-bar loop with a second, brighter cycle
        float loopLen = barLen * bars;
        int samples = (int)(sr * loopLen);
        float[] data = new float[samples];

        // vi–IV–I–V in A minor (Am – F – C – G): an extremely common,
        // uncopyrightable pop chord loop. Original melody line on top.
        float[] roots = { 220.00f, 174.61f, 261.63f, 196.00f };
        bool[] isMinor = { true, false, false, false };

        for (int b = 0; b < bars; b++)
        {
            int chord = b % 4;
            float barStart = b * barLen;
            float R = roots[chord];
            float third = R * Mathf.Pow(2f, (isMinor[chord] ? 3f : 4f) / 12f);
            float fifth = R * Mathf.Pow(2f, 7f / 12f);
            float oct = R * 2f;

            // Sixteenth-note arpeggio for energy
            float[] arp = { R, fifth, third, oct, fifth, oct, third, fifth };
            float sixteenth = beat / 4f;
            for (int n = 0; n < 16; n++)
                AddNote(data, sr, barStart + n * sixteenth, sixteenth * 0.95f, arp[n % 8] * 2f, 0.075f, WaveType.Saw);

            // Driving bass — eighth-note pulse
            float eighth = beat / 2f;
            for (int k = 0; k < 8; k++)
                AddNote(data, sr, barStart + k * eighth, eighth * 0.9f, R / 2f, 0.20f, WaveType.Sine);

            // Four-on-the-floor kick
            for (int k = 0; k < 4; k++)
                AddNote(data, sr, barStart + k * beat, 0.15f, 50f, 0.55f, WaveType.Kick);

            // Offbeat hi-hats
            for (int k = 0; k < 4; k++)
                AddNote(data, sr, barStart + k * beat + eighth, 0.045f, 9000f, 0.06f, WaveType.Noise);

            // Lead hook (quarter notes) — original motif, brighter in the second cycle
            float leadAmp = (b >= 4) ? 0.11f : 0.09f;
            float[] lead = { oct, fifth, oct, third * 2f };
            for (int k = 0; k < 4; k++)
                AddNote(data, sr, barStart + k * beat, beat * 0.85f, lead[k], leadAmp, WaveType.Saw);
        }

        // Soft clip to keep it from distorting
        for (int i = 0; i < samples; i++)
            data[i] = Mathf.Clamp(data[i], -0.95f, 0.95f);

        AudioClip clip = AudioClip.Create("SynthwaveLoop", samples, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    enum WaveType { Saw, Sine, Kick, Noise }

    static void AddNote(float[] data, int sr, float startSec, float durSec, float freq, float amp, WaveType type)
    {
        int start = (int)(startSec * sr);
        int len = (int)(durSec * sr);

        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= data.Length) continue;

            float tt = (float)i / sr;
            float norm = (float)i / len;
            float ph = 2f * Mathf.PI * freq * tt;

            float w, env;
            switch (type)
            {
                case WaveType.Saw: // bright pluck
                    w = Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph) + 0.33f * Mathf.Sin(3f * ph);
                    float attack = Mathf.Min(1f, norm / 0.01f);
                    env = attack * Mathf.Exp(-norm * 3.5f);
                    break;
                case WaveType.Sine: // bass
                    w = Mathf.Sin(ph);
                    env = Mathf.Exp(-norm * 2.5f);
                    break;
                case WaveType.Noise: // hi-hat
                    w = Random.Range(-1f, 1f);
                    env = Mathf.Exp(-norm * 40f);
                    break;
                default: // Kick — pitch drop thump
                    w = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 40f, norm) * tt);
                    env = Mathf.Exp(-norm * 16f);
                    break;
            }

            data[idx] += w * amp * env;
        }
    }
}
