using UnityEngine;
using System.Collections;

/// <summary>
/// Destructible wooden crate used as defensive cover. Blocks shots (raycasts stop
/// on it) and can be shot apart into debris. Textured with the wood PBR maps.
/// </summary>
public class WoodenCrate : MonoBehaviour
{
    public int health = 6;

    private Renderer rend;
    private static Material _woodMat;

    public static GameObject Create(Vector3 pos, float size, Quaternion rot)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); // has a BoxCollider
        go.name = "WoodenCrate";
        go.transform.position = pos;
        go.transform.rotation = rot;
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<WoodenCrate>();
        return go;
    }

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = GetWoodMaterial();
    }

    static Material GetWoodMaterial()
    {
        if (_woodMat != null) return _woodMat;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _woodMat = new Material(lit) { name = "CrateWood" };

        Texture2D albedo = Resources.Load<Texture2D>("Models/ThompsonTextures/Wood_Albedo");
        Texture2D normal = Resources.Load<Texture2D>("Models/ThompsonTextures/Wood_Normal");
        if (albedo != null) { _woodMat.SetTexture("_BaseMap", albedo); _woodMat.SetTexture("_MainTex", albedo); }
        else _woodMat.SetColor("_BaseColor", new Color(0.45f, 0.3f, 0.13f));
        if (normal != null) { _woodMat.SetTexture("_BumpMap", normal); _woodMat.EnableKeyword("_NORMALMAP"); }
        if (_woodMat.HasProperty("_Smoothness")) _woodMat.SetFloat("_Smoothness", 0.2f);
        _woodMat.enableInstancing = true;
        return _woodMat;
    }

    public void TakeDamage(int dmg)
    {
        health -= dmg;
        StartCoroutine(HitFlash());
        if (health <= 0) Break();
    }

    IEnumerator HitFlash()
    {
        if (rend == null) yield break;
        var mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", new Color(1f, 0.85f, 0.55f));
        rend.SetPropertyBlock(mpb);
        yield return new WaitForSeconds(0.05f);
        rend.SetPropertyBlock(new MaterialPropertyBlock());
    }

    void Break()
    {
        // Splinter into a few physics shards
        for (int i = 0; i < 6; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.transform.position = transform.position + Random.insideUnitSphere * 0.2f;
            piece.transform.rotation = Random.rotation;
            piece.transform.localScale = transform.localScale * Random.Range(0.15f, 0.3f);
            piece.GetComponent<Renderer>().sharedMaterial = GetWoodMaterial();
            Rigidbody rb = piece.AddComponent<Rigidbody>();
            rb.AddExplosionForce(3f, transform.position, 1.5f);
            Destroy(piece, 2.5f);
        }

        AudioClip s = ProceduralAudioGenerator.GenerateHitImpact();
        if (s != null) AudioSource.PlayClipAtPoint(s, transform.position, 0.7f);

        Destroy(gameObject);
    }
}
