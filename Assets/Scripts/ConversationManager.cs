using UnityEngine;

/// <summary>
/// Cloud LLM implements this to control enemy behaviour. Register on
/// GameBridge.Instance.EnemyDirector; AlienAI consults it if present.
/// </summary>
public interface IEnemyDirector
{
    // Return an aggression multiplier (1 = normal). Cloud LLM can raise/lower per enemy.
    float GetAggression(GameObject enemy, Vector3 playerPos, float healthPct);
}

/// <summary>
/// A source of spoken "god"/boss responses. Cloud and Arduino each implement this;
/// Local is the always-available fallback.
/// </summary>
public interface IConversationProvider
{
    bool IsAvailable { get; }
    string Respond(string playerInput);
}

/// <summary>
/// Canned, always-available god responses used when neither cloud nor arduino is online.
/// </summary>
public class LocalConversationProvider : IConversationProvider
{
    public bool IsAvailable => true;

    private readonly string[] genericLines =
    {
        "I hear you, warden. But the rift does not bargain.",
        "Your voice carries across dead universes. Keep fighting.",
        "Words will not seal the breach. Steel will.",
        "You are not alone... yet. Hold the line.",
        "The Overlord listens too. Choose your words like blades."
    };

    public string Respond(string playerInput)
    {
        if (!string.IsNullOrEmpty(playerInput))
        {
            string p = playerInput.ToLower();
            if (p.Contains("help")) return "Help comes to those who hold the line. Aim for the rifts.";
            if (p.Contains("who") || p.Contains("what are you")) return "I am the voice between worlds. Some called me a god. You may too.";
            if (p.Contains("stop") || p.Contains("mercy")) return "Mercy is a luxury of stable universes. This is not one.";
        }
        return genericLines[Random.Range(0, genericLines.Length)];
    }
}

/// <summary>
/// Picks a conversation provider with fallback order Cloud → Arduino → Local.
/// Cloud (Qualcomm AI Cloud) and Arduino (Uno Q) providers are assigned when they connect.
/// </summary>
public class ConversationManager
{
    private readonly GameBridge bridge;
    private readonly IConversationProvider local = new LocalConversationProvider();

    public IConversationProvider CloudProvider;   // set when Qualcomm AI Cloud connects
    public IConversationProvider ArduinoProvider; // set when Arduino Uno Q connects

    public ConversationManager(GameBridge b) { bridge = b; }

    public string GodRespondTo(string input)
    {
        if (bridge != null && bridge.cloudOnline && CloudProvider != null && CloudProvider.IsAvailable)
            return CloudProvider.Respond(input);
        if (bridge != null && bridge.arduinoOnline && ArduinoProvider != null && ArduinoProvider.IsAvailable)
            return ArduinoProvider.Respond(input);
        return local.Respond(input);
    }
}
