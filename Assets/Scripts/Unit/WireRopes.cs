using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
[AddComponentMenu("Equipment/WireRopes")]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WireRopes : MonoBehaviour 
{
    private Mesh mesh;
    private MeshFilter filter;
    public Transform hoist;
    public float diameter = 0.03f;
    public int tessellation = 8;
    public float tenseDistance = 0.1f;
    public Vector3[] FromPosition;
    public Vector3[] JointPosition;
    
    private float lastValue;
    //Vector3[] normals;
    private Vector3[] vertices;
    private int[] triangles;
    private Quaternion trollyQ;
    void Awake()
    {
        if (transform.parent.gameObject != hoist.parent.gameObject)
            throw new UnityException(string.Format("Wireropes should be under the same gameobject with hois!"));
        if (FromPosition.Length != JointPosition.Length)
            throw new UnityException(string.Format("Quantity of FromPoints not equal to ToPoints in WireRopes"));
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        transform.localPosition = new Vector3(0, 0, 0);

        trollyQ = Quaternion.Inverse(hoist.parent.rotation);//
        filter = GetComponent<MeshFilter>();            
        mesh = new Mesh();
        //normals = new Vector3[tessellation * 2];
        vertices = new Vector3[tessellation * 2 * FromPosition.Length];
        triangles = new int[tessellation * 6 * FromPosition.Length];
    }

	// Use this for initialization
	void Start () 
    {
        this.lastValue = hoist.position.y;
        BuildMesh();
	}

    private void BuildMesh()
    {
        float radius = diameter / 2;
        for (int loop = 0; loop < FromPosition.Length; loop++)
        {
            for (int i = 0; i < tessellation; i++)
            {
                Vector3 normal = GetCircleVector(i);
                //normals[2 * i] = -normal;
                //normals[2 * i+1] = -normal;
                vertices[tessellation * 2 * loop + 2 * i] = trollyQ * (normal * radius + FromPosition[loop]);//   
                vertices[tessellation * 2 * loop + 2 * i + 1] = (trollyQ * (normal * radius + JointPosition[loop]) + hoist.localPosition);//hoistQ * 

                triangles[tessellation * 6 * loop + 6 * i] = tessellation * 2 * loop + i * 2;
                triangles[tessellation * 6 * loop + 6 * i + 1] = tessellation * 2 * loop + ((i * 2 + 2) % (tessellation * 2));
                triangles[tessellation * 6 * loop + 6 * i + 2] = (tessellation * 2 * loop + i * 2 + 1);

                triangles[tessellation * 6 * loop + 6 * i + 3] = (tessellation * 2 * loop + i * 2 + 1);
                triangles[tessellation * 6 * loop + 6 * i + 4] = tessellation * 2 * loop + ((i * 2 + 2) % (tessellation * 2));
                triangles[tessellation * 6 * loop + 6 * i + 5] = tessellation * 2 * loop + ((i * 2 + 3) % (tessellation * 2));
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        //mesh.normals = normals;
        filter.sharedMesh = mesh;
    }

	// Update is called once per frame
	void Update () 
    {
        if (Mathf.Abs(hoist.position.y - lastValue) > tenseDistance)
        {
            this.lastValue = hoist.position.y;
            this.BuildMesh();
        }
	}

    /// <summary>
    /// Helper method computes a point on a circle.
    /// </summary>
    private Vector3 GetCircleVector(int i)
    {
        float angle = i * Mathf.PI * 2 / tessellation;

        float dx = Mathf.Cos(angle);
        float dz = Mathf.Sin(angle);

        return new Vector3(dx, 0, dz);
    }
}
