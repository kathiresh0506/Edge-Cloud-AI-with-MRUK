using UnityEngine;

/// <summary>
/// Left-hand torchlight. Holds the torch model with a real Spot Light child that
/// toggles on/off on left trigger press. Once the darkness vignette tightens
/// (gems collected), this is the player's only way to see into darkened areas:
/// DarknessController reads the beam data from here to cut a see-through hole
/// in the darkness dome along the beam.
/// </summary>
public class TorchController : MonoBehaviour
{
    public static TorchController Instance { get; private set; }

    [Header("Input")]
    public UnityEngine.XR.XRNode controllerNode = UnityEngine.XR.XRNode.LeftHand;

    [Header("Model")]
    public string torchModelAssetPath = "Assets/Resources/Models/TorchLight.glb";
    [Tooltip("Torch model height in meters.")]
    public float torchSize = 0.3f;

    [Header("Spot Light")]
    public float lightRange = 10f;
    public float lightSpotAngle = 42f;
    public float lightIntensity = 6f;
    public Color lightColor = new Color(1f, 0.93f, 0.75f); // warm torch light
    public bool startOn = false;

    [Header("Darkness Cutout")]
    [Tooltip("Half-angle (degrees) of the see-through cone punched into the darkness dome.")]
    public float cutoutAngle = 24f;

    // Runtime
    private Light spotLight;
    private GameObject modelInstance;
    private GameObject flameGlow;
    private AudioSource audioSource;
    private AudioClip clickSound;
    private bool previousTriggerState = false;
    private bool isOn;

    /// <summary>Is the torch beam currently on?</summary>
    public bool IsOn => isOn;
    /// <summary>World-space origin of the beam.</summary>
    public Vector3 BeamOrigin => spotLight != null ? spotLight.transform.position : transform.position;
    /// <summary>World-space direction of the beam.</summary>
    public Vector3 BeamDirection => spotLight != null ? spotLight.transform.forward : transform.forward;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        clickSound = ProceduralAudioGenerator.GenerateReload(); // short mechanical click

        BuildTorchModel();
        BuildSpotLight();
        SetTorch(startOn);
    }

    void BuildTorchModel()
    {
        GameObject prefab = ModelUtil.LoadPrefab(torchModelAssetPath);
        if (prefab != null)
        {
            modelInstance = Instantiate(prefab);
            modelInstance.name = "TorchModel";
            ModelUtil.NormalizeHeight(modelInstance, torchSize);
            modelInstance.transform.SetParent(transform, false);
            // Grip like a handheld torch: slightly forward, tilted ahead of the hand
            modelInstance.transform.localPosition = new Vector3(0f, -0.03f, 0.05f);
            modelInstance.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            ModelUtil.StripColliders(modelInstance);
        }
        else
        {
            // Fallback: a simple stick so the demo never breaks
            modelInstance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            modelInstance.name = "TorchModel_Fallback";
            Object.Destroy(modelInstance.GetComponent<Collider>());
            modelInstance.transform.SetParent(transform, false);
            modelInstance.transform.localPosition = new Vector3(0f, -0.03f, 0.05f);
            modelInstance.transform.localRotation = Quaternion.Euler(35f + 90f, 0f, 0f);
            modelInstance.transform.localScale = new Vector3(0.03f, torchSize * 0.5f, 0.03f);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit != null)
            {
                Material m = new Material(lit);
                m.SetColor("_BaseColor", new Color(0.45f, 0.3f, 0.12f));
                modelInstance.GetComponent<Renderer>().material = m;
            }
        }
    }

    void BuildSpotLight()
    {
        // Beam points where the hand points (controller forward)
        GameObject lightGO = new GameObject("TorchSpotLight");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 0.02f, 0.08f);
        lightGO.transform.localRotation = Quaternion.identity;

        spotLight = lightGO.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.range = lightRange;
        spotLight.spotAngle = lightSpotAngle;
        spotLight.innerSpotAngle = lightSpotAngle * 0.6f;
        spotLight.intensity = lightIntensity;
        spotLight.color = lightColor;
        spotLight.shadows = LightShadows.None; // cheap on Quest

        // Small glowing tip so the torch reads as lit even from behind
        flameGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flameGlow.name = "FlameGlow";
        Object.Destroy(flameGlow.GetComponent<Collider>());
        flameGlow.transform.SetParent(lightGO.transform, false);
        flameGlow.transform.localPosition = Vector3.zero;
        flameGlow.transform.localScale = Vector3.one * 0.045f;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (unlit != null)
        {
            Material m = new Material(unlit);
            Color c = new Color(1f, 0.85f, 0.45f);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            flameGlow.GetComponent<Renderer>().material = m;
        }
    }

    void Update()
    {
        // Toggle on left trigger press (edge-triggered)
        bool triggerPressed = false;
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid &&
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool tVal))
        {
            triggerPressed = tVal;
        }

        if (triggerPressed && !previousTriggerState)
        {
            SetTorch(!isOn);
        }
        previousTriggerState = triggerPressed;

        // Subtle flicker while on — sells 'real torch'
        if (isOn && spotLight != null)
        {
            spotLight.intensity = lightIntensity *
                (1f + (Mathf.PerlinNoise(Time.time * 7f, 0.37f) - 0.5f) * 0.18f);
        }
    }

    public void SetTorch(bool on)
    {
        isOn = on;
        if (spotLight != null) spotLight.enabled = on;
        if (flameGlow != null) flameGlow.SetActive(on);

        if (clickSound != null && audioSource != null)
            audioSource.PlayOneShot(clickSound, 0.35f);

        // Haptic tick so the toggle is felt
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
            device.SendHapticImpulse(0, 0.25f, 0.04f);
    }
}
