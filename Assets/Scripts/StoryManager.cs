using UnityEngine;

/// <summary>
/// Original narrative layer ("RIFTWATCH"). Drives story beats off GameManager events
/// and speaks them through the god-narrator (GameBridge). Auto-spawns; no wiring needed.
///
/// Story: you are the last Warden. The wall behind your world has cracked, and the
/// Overlord — a warlord who empties whole universes — is pushing his legions through
/// the rifts. A voice between worlds guides you. Hold the line.
/// </summary>
public class StoryManager : MonoBehaviour
{
    private bool subscribed = false;
    private int lastNarratedWave = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        GameObject go = new GameObject("StoryManager");
        DontDestroyOnLoad(go);
        go.AddComponent<StoryManager>();
    }

    void Update()
    {
        if (!subscribed)
        {
            if (GameManager.Instance == null) return;
            Subscribe();
        }
        PollWave();
    }

    void Subscribe()
    {
        var gm = GameManager.Instance;
        gm.onGameStart.AddListener(OnGameStart);
        gm.onWaveComplete.AddListener(OnWaveComplete);
        gm.onGameOver.AddListener(OnGameOver);
        subscribed = true;
    }

    void PollWave()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.currentState == GameManager.GameState.Playing
            && gm.currentWave > 0
            && gm.currentWave != lastNarratedWave)
        {
            lastNarratedWave = gm.currentWave;
            Narrate(WaveLine(gm.currentWave));
        }
    }

    void OnGameStart()
    {
        lastNarratedWave = 0;
        Narrate("Warden. The wall behind your world has cracked — and what waits on the other side has waited a very long time.");
    }

    void OnWaveComplete()
    {
        Narrate("The rifts quiet. Breathe. It will not last.");
    }

    void OnGameOver()
    {
        Narrate("The line breaks. The worlds pour through the dark. Perhaps another Warden will rise where you fell.");
    }

    string WaveLine(int wave)
    {
        switch (wave)
        {
            case 1: return "First blood. These are only scouts — the thin ones, sent to measure your resolve.";
            case 2: return "More come through. Across the dark, a warlord counts your kills, and smiles.";
            case 3: return "The rifts widen. Steady your hands. You are being weighed, Warden.";
            case 4: return "Their universe is dying, and they would wear yours in its place. Deny them.";
            case 5: return "He crosses over himself now — the Overlord. Every world he has entered, he has emptied.";
            default: return "Hold. Every breath you steal from them is a world still breathing.";
        }
    }

    void Narrate(string line)
    {
        if (GameBridge.Instance != null) GameBridge.Instance.Narrate(line);
    }
}
