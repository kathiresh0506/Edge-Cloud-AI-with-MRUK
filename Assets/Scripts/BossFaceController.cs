using UnityEngine;

/// <summary>
/// Mount point for a runtime-provided boss face (e.g. the OnePlus 15 phone streaming
/// a 2D→3D .obj in real time). Attached to the boss; the head bone is the anchor.
/// </summary>
public class BossFaceController : MonoBehaviour
{
    [Tooltip("Head bone the face is mounted to. Auto-found if left null.")]
    public Transform head;

    [Header("Mount tuning")]
    public Vector3 faceLocalOffset = new Vector3(0f, 0.02f, 0.1f);
    public float faceScale = 0.2f;

    private GameObject currentFace;

    void Awake()
    {
        if (head == null) head = FindHead();
    }

    Transform FindHead()
    {
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("head") && !n.Contains("top")) return t;
        }
        return transform;
    }

    /// <summary>Mount a provided GameObject (e.g. an imported OBJ) as the boss face.</summary>
    public void SetFaceObject(GameObject faceObject)
    {
        if (faceObject == null) return;
        if (currentFace != null) Destroy(currentFace);

        currentFace = faceObject;
        Transform mount = head != null ? head : transform;
        faceObject.transform.SetParent(mount, false);
        faceObject.transform.localPosition = faceLocalOffset;
        faceObject.transform.localRotation = Quaternion.identity;
        faceObject.transform.localScale = Vector3.one * faceScale;
    }

    /// <summary>Mount a provided Mesh + Material as the boss face.</summary>
    public void SetFaceMesh(Mesh mesh, Material material)
    {
        if (mesh == null) return;
        GameObject go = new GameObject("BossFace");
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (material != null) mr.sharedMaterial = material;
        SetFaceObject(go);
    }

    public bool HasCustomFace => currentFace != null;
}
