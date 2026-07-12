using UnityEngine;

/// <summary>
/// Generates all game sound effects procedurally using mathematical waveforms.
/// No audio files needed — everything is created at runtime.
/// </summary>
public static class ProceduralAudioGenerator
{
    private const int SAMPLE_RATE = 44100;

    // ========== PUBLIC API ==========

    public static AudioClip GenerateLaserShot()
    {
        float duration = 0.15f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Frequency sweep from 2000Hz down to 400Hz
            float freq = Mathf.Lerp(2000f, 400f, norm);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;

            // Add some noise for texture
            float noise = (Random.Range(-1f, 1f)) * 0.15f * (1f - norm);

            // Sharp attack, quick decay
            float envelope = Mathf.Exp(-norm * 6f);

            data[i] = (wave + noise) * envelope;
        }

        return CreateClip("LaserShot", data, duration);
    }

    public static AudioClip GenerateHitImpact()
    {
        float duration = 0.12f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Low thud
            float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(200f, 80f, norm) * t) * 0.5f;

            // Noise crunch
            float noise = Random.Range(-1f, 1f) * 0.3f;

            float envelope = Mathf.Exp(-norm * 10f);

            data[i] = (thud + noise) * envelope;
        }

        return CreateClip("HitImpact", data, duration);
    }

    public static AudioClip GenerateKillSound()
    {
        float duration = 0.4f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Descending screech
            float freq = Mathf.Lerp(1500f, 100f, norm * norm);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;

            // Wobble
            float wobble = Mathf.Sin(2f * Mathf.PI * 30f * t) * 0.2f;
            wave *= (1f + wobble);

            // Add harmonic
            wave += Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.15f;

            float envelope = Mathf.Exp(-norm * 4f);

            data[i] = wave * envelope;
        }

        return CreateClip("KillSound", data, duration);
    }

    public static AudioClip GenerateAlienSpawn()
    {
        float duration = 0.6f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Rising warble
            float freq = Mathf.Lerp(150f, 800f, norm);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;

            // Vibrato
            float vibrato = Mathf.Sin(2f * Mathf.PI * 12f * t) * 50f;
            wave += Mathf.Sin(2f * Mathf.PI * (freq + vibrato) * t) * 0.15f;

            // Envelope: fade in then fade out
            float envelope = Mathf.Sin(norm * Mathf.PI);

            data[i] = wave * envelope;
        }

        return CreateClip("AlienSpawn", data, duration);
    }

    public static AudioClip GenerateAlienAttack()
    {
        float duration = 0.25f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Aggressive growl
            float growl = Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.3f;
            growl += Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.2f;

            // Distortion via clipping
            float noise = Random.Range(-1f, 1f) * 0.25f;
            float combined = Mathf.Clamp(growl + noise, -0.5f, 0.5f);

            float envelope = norm < 0.1f ? norm / 0.1f : Mathf.Exp(-(norm - 0.1f) * 5f);

            data[i] = combined * envelope;
        }

        return CreateClip("AlienAttack", data, duration);
    }

    public static AudioClip GenerateAlienIdle()
    {
        float duration = 2.0f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Low drone with modulation
            float freq = 80f + Mathf.Sin(t * 2f) * 20f;
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.15f;
            wave += Mathf.Sin(2f * Mathf.PI * freq * 2.01f * t) * 0.08f;

            // Breathing rhythm
            float breath = (Mathf.Sin(t * Mathf.PI * 1.5f) + 1f) * 0.5f;

            // Seamless loop envelope
            float loopEnv = Mathf.Sin(norm * Mathf.PI);

            data[i] = wave * breath * Mathf.Lerp(0.3f, 1f, loopEnv);
        }

        return CreateClip("AlienIdle", data, duration);
    }

    public static AudioClip GenerateAlienDeath()
    {
        float duration = 0.5f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Descending screech with pitch wobble
            float freq = Mathf.Lerp(1200f, 60f, norm);
            float wobble = Mathf.Sin(t * 80f) * 100f;
            float wave = Mathf.Sin(2f * Mathf.PI * (freq + wobble) * t) * 0.35f;

            // Crackle
            float crackle = Random.Range(-1f, 1f) * 0.2f * (1f - norm);

            float envelope = Mathf.Exp(-norm * 3f);

            data[i] = (wave + crackle) * envelope;
        }

        return CreateClip("AlienDeath", data, duration);
    }

    public static AudioClip GeneratePlayerDamage()
    {
        float duration = 0.3f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Low boom
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 40f, norm) * t) * 0.4f;

            // Crunch noise
            float crunch = Random.Range(-1f, 1f) * 0.3f * Mathf.Exp(-norm * 8f);

            float envelope = Mathf.Exp(-norm * 5f);

            data[i] = (boom + crunch) * envelope;
        }

        return CreateClip("PlayerDamage", data, duration);
    }

    public static AudioClip GeneratePlayerDeath()
    {
        float duration = 0.8f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Deep descending rumble
            float freq = Mathf.Lerp(200f, 30f, norm);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
            wave += Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.3f;

            // Static/distortion
            float noise = Random.Range(-1f, 1f) * 0.2f * norm;

            float envelope = norm < 0.05f ? norm / 0.05f : Mathf.Exp(-(norm - 0.05f) * 2f);

            data[i] = (wave + noise) * envelope;
        }

        return CreateClip("PlayerDeath", data, duration);
    }

    public static AudioClip GenerateWaveFanfare()
    {
        float duration = 0.6f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        // Three ascending tones
        float[] toneFreqs = { 440f, 554f, 659f };
        float toneDuration = duration / 3f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            int toneIndex = Mathf.Min((int)(norm * 3f), 2);
            float localT = (t - toneIndex * toneDuration);
            float localNorm = localT / toneDuration;

            float freq = toneFreqs[toneIndex];
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.25f;
            wave += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.1f; // Harmonic

            // Per-tone envelope
            float env = Mathf.Sin(Mathf.Clamp01(localNorm) * Mathf.PI);

            data[i] = wave * env;
        }

        return CreateClip("WaveFanfare", data, duration);
    }

    // ========== INTERNAL ==========

    public static AudioClip GenerateAlienApproach()
    {
        float duration = 1.0f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Rising tension hiss
            float freq = Mathf.Lerp(100f, 600f, norm * norm);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.2f;
            float noise = Random.Range(-1f, 1f) * 0.15f * norm;

            float envelope = norm * norm; // Crescendo
            data[i] = (wave + noise) * envelope * 0.5f;
        }

        return CreateClip("AlienApproach", data, duration);
    }

    public static AudioClip GenerateAlienCharge()
    {
        float duration = 0.3f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            float freq = Mathf.Lerp(200f, 900f, norm);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
            wave += Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.2f;
            float noise = Random.Range(-1f, 1f) * 0.2f;

            float envelope = norm < 0.2f ? norm / 0.2f : 1f;
            data[i] = (wave + noise) * envelope;
        }

        return CreateClip("AlienCharge", data, duration);
    }

    public static AudioClip GenerateShotgunBlast()
    {
        float duration = 0.2f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            float boom = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.5f;
            float noise = Random.Range(-1f, 1f) * 0.5f;
            float envelope = Mathf.Exp(-norm * 8f);
            data[i] = (boom + noise) * envelope;
        }

        return CreateClip("ShotgunBlast", data, duration);
    }

    public static AudioClip GenerateRailgunShot()
    {
        float duration = 0.35f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            float freq = Mathf.Lerp(3000f, 200f, norm);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;
            wave += Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.2f;
            float crack = Random.Range(-1f, 1f) * 0.15f * Mathf.Exp(-norm * 15f);

            float envelope = norm < 0.02f ? norm / 0.02f : Mathf.Exp(-norm * 4f);
            data[i] = (wave + crack) * envelope;
        }

        return CreateClip("RailgunShot", data, duration);
    }

    public static AudioClip GenerateWeaponPickup()
    {
        float duration = 0.4f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];
        float[] notes = { 523f, 659f, 784f };

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;
            int noteIdx = Mathf.Min((int)(norm * 3f), 2);
            float freq = notes[noteIdx];
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;
            float env = Mathf.Sin(Mathf.Clamp01((norm * 3f - noteIdx)) * Mathf.PI);
            data[i] = wave * env;
        }

        return CreateClip("WeaponPickup", data, duration);
    }

    /// <summary>
    /// Realistic firearm crack — sharp transient, low boom body, and a low-passed tail.
    /// Much punchier / more "real" than the laser pew. Pitch-vary at playback for variety.
    /// </summary>
    public static AudioClip GenerateGunshot()
    {
        float duration = 0.30f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];
        float lp = 0f; // low-pass state for the smoky tail

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            // Sharp initial crack (first few ms)
            float crackEnv = Mathf.Exp(-norm * 55f);
            float crack = Random.Range(-1f, 1f) * crackEnv;

            // Low-end boom body (chest thump)
            float boomEnv = Mathf.Exp(-norm * 13f);
            float boom = (Mathf.Sin(2f * Mathf.PI * 85f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 55f * t)) * 0.5f * boomEnv;

            // Mid punch
            float punch = Mathf.Sin(2f * Mathf.PI * 210f * t) * 0.3f * Mathf.Exp(-norm * 24f);

            // Low-passed noise tail (echo/smoke)
            float rawNoise = Random.Range(-1f, 1f);
            lp += (rawNoise - lp) * 0.14f;
            float tail = lp * 0.35f * Mathf.Exp(-norm * 6.5f);

            data[i] = Mathf.Clamp(crack * 0.85f + boom + punch + tail, -1f, 1f);
        }

        return CreateClip("Gunshot", data, duration);
    }

    /// <summary>
    /// Looping mechanical breathing for the boss (Vader). Two-phase inhale/exhale, heavy low-pass.
    /// </summary>
    public static AudioClip GenerateVaderBreath()
    {
        float duration = 4f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];
        float lp = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float cyc = (float)i / samples;

            // Breathy filtered noise
            float rawNoise = Random.Range(-1f, 1f);
            lp += (rawNoise - lp) * 0.05f;
            float breath = lp;

            // Two amplitude humps: inhale then exhale
            float inhale = Mathf.Exp(-Mathf.Pow((cyc - 0.25f) / 0.11f, 2f));
            float exhale = Mathf.Exp(-Mathf.Pow((cyc - 0.72f) / 0.14f, 2f));

            // Tonal component (regulator hum), inhale higher than exhale
            float toneIn = Mathf.Sin(2f * Mathf.PI * 210f * t) * 0.12f * inhale;
            float toneEx = Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.18f * exhale;

            float amp = inhale * 1.0f + exhale * 1.1f;
            data[i] = Mathf.Clamp(breath * amp * 0.65f + toneIn + toneEx, -1f, 1f);
        }

        return CreateClip("VaderBreath", data, duration);
    }

    /// <summary>
    /// Deep, robotic voice stinger played when the boss speaks a line.
    /// </summary>
    public static AudioClip GenerateVaderVoice()
    {
        float duration = 0.7f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;

            float freq = Mathf.Lerp(95f, 68f, norm);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
            wave += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;

            // Ring modulation for a metallic/robotic timbre
            float ring = Mathf.Sin(2f * Mathf.PI * 38f * t);
            wave *= (0.7f + 0.3f * ring);

            float env = Mathf.Sin(Mathf.Clamp01(norm) * Mathf.PI);
            data[i] = wave * env;
        }

        return CreateClip("VaderVoice", data, duration);
    }

    /// <summary>Mechanical reload — mag-out, mag-in, charging-handle clicks.</summary>
    public static AudioClip GenerateReload()
    {
        float duration = 0.7f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        float[] clickTimes = { 0.0f, 0.32f, 0.55f };
        foreach (float ct in clickTimes)
        {
            int start = (int)(ct * SAMPLE_RATE);
            int len = (int)(0.05f * SAMPLE_RATE);
            for (int i = 0; i < len && start + i < samples; i++)
            {
                float n = (float)i / len;
                float tt = (float)i / SAMPLE_RATE;
                float click = Random.Range(-1f, 1f) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1200f * tt) * 0.4f;
                data[start + i] += click * Mathf.Exp(-n * 25f);
            }
        }
        return CreateClip("Reload", data, duration);
    }

    /// <summary>Melee punch — low thud with a noise slap.</summary>
    public static AudioClip GeneratePunch()
    {
        float duration = 0.2f;
        int samples = Mathf.RoundToInt(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = (float)i / samples;
            float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 60f, norm) * t) * 0.6f;
            float noise = Random.Range(-1f, 1f) * 0.4f * Mathf.Exp(-norm * 20f);
            data[i] = (thud + noise) * Mathf.Exp(-norm * 12f);
        }
        return CreateClip("Punch", data, duration);
    }

    private static AudioClip CreateClip(string name, float[] data, float duration)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Deep bass rumble for horror boss atmosphere. Loops well.
    /// </summary>
    public static AudioClip GenerateDeepRumble()
    {
        float duration = 3f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;

            // Deep bass drone at 35Hz
            float bass = Mathf.Sin(2f * Mathf.PI * 35f * t) * 0.4f;

            // Sub-harmonics for rumble
            float sub = Mathf.Sin(2f * Mathf.PI * 18f * t) * 0.25f;

            // Slow modulation for unsettling feel
            float mod = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);

            // Filtered noise for texture
            float noise = (Random.Range(-1f, 1f)) * 0.05f;

            data[i] = (bass + sub + noise) * mod * 0.5f;
        }

        return CreateClip("DeepRumble", data, duration);
    }
}
