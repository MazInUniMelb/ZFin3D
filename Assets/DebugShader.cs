using UnityEngine;

public class SimpleTest : MonoBehaviour
{
    public Material testMaterial;
    public Mesh sphereMesh;

    void Start()
    {
        // Create a simple sphere GameObject
        GameObject sphere = new GameObject("TestSphere");
        MeshFilter meshFilter = sphere.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = sphere.AddComponent<MeshRenderer>();

        // Assign the sphere mesh and test material
        meshFilter.mesh = sphereMesh;
        meshRenderer.material = testMaterial;

        // Position the sphere in front of the camera
        sphere.transform.position = new Vector3(0, 0, 5);
    }
}
