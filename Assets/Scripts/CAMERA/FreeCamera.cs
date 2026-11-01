using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

[AddComponentMenu("Camera/FreeCamera")]
public class FreeCamera : MonoBehaviour
{
    private Vector3 oldMousePos;
    private Vector3 newMosuePos;
    private Camera bindCamera;
    private PhysicsRaycaster raycaster;

    [SerializeField]
    private float minimumY = 0.2f;

    [SerializeField]
    private float zoomSpeed = 30.0f;
    [SerializeField]
    private float keyBoardMoveSpeed = 1f;
    [SerializeField]
    private float rotSpeed = 0.05f;
    [SerializeField]
    private float mouseMoveSpeed = 0.05f; // 鼠标中键控制相机的速度

    private Transform followedObject;
    private float distance = 5;
    private Vector3 centerOffset = Vector3.zero;
    private bool isOn;

    public bool IsOn
    {
        set
        {
            if (this.isOn == value)
                return;

            this.isOn = value;
            if (value)
            {
                this.oldMousePos = this.newMosuePos = Input.mousePosition;
                this.raycaster.enabled = true;
            }
            else
            {
                this.UnFocus();
                this.raycaster.enabled = false;
            }
        }
    }

    protected virtual void Awake()
    {
        this.bindCamera = GetComponent<Camera>();
        raycaster = this.gameObject.GetComponent<PhysicsRaycaster>();
        if (!raycaster)
            raycaster = this.gameObject.AddComponent<PhysicsRaycaster>();
    }

    protected virtual void Start()
    {
        this.IsOn = false;
    }

    protected virtual void Update()
    {
        if (!this.isOn)
            return;

        MoveCameraKeyBoard();
        ZoomCamera();
        SuperViewMouse();

        oldMousePos = Input.mousePosition;
    }

    void LateUpdate()
    {
        if (followedObject && isOn)
        {
            Vector3 targetPositon = this.followedObject.transform.position + this.centerOffset + transform.forward * -distance;
            targetPositon.y = Mathf.Max(targetPositon.y, this.minimumY);
            this.transform.position = Vector3.Lerp(this.transform.position, targetPositon, Time.unscaledDeltaTime * 15);
        }
    }

    private void MoveCameraKeyBoard()
    {
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Translate(new Vector3(-keyBoardMoveSpeed, 0, 0), Space.Self);
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            transform.Translate(new Vector3(keyBoardMoveSpeed, 0, 0), Space.Self);
        }
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            transform.Translate(new Vector3(0, 0, keyBoardMoveSpeed), Space.Self);
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            transform.Translate(new Vector3(0, 0, -keyBoardMoveSpeed), Space.Self);
        }
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Translate(new Vector3(0, keyBoardMoveSpeed, 0), Space.World);
        }
        if (Input.GetKey(KeyCode.E))
        {
            if (transform.transform.position.y - keyBoardMoveSpeed >= this.minimumY)
                transform.Translate(new Vector3(0, -keyBoardMoveSpeed, 0), Space.World);
        }
    }

    private void ZoomCamera()
    {
        float offset = Input.GetAxis("Mouse ScrollWheel");
        if (offset != 0)
        {
            offset *= zoomSpeed;
            this.distance -= offset;
            transform.Translate(Vector3.forward * offset, Space.Self);
        }
    }

    private void SuperViewMouse()
    {
        if (Input.GetMouseButton(1))
        {
            newMosuePos = Input.mousePosition;
            Vector3 dis = newMosuePos - oldMousePos;
            float angleX = dis.x * rotSpeed;
            float angleY = dis.y * rotSpeed;
            transform.Rotate(new Vector3(-angleY, 0, 0), Space.Self);
            transform.Rotate(new Vector3(0, angleX, 0), Space.World);
        }

        if (Input.GetMouseButton(2))
        {
            newMosuePos = Input.mousePosition;
            Vector3 dis = newMosuePos - oldMousePos;
            float angleX = dis.x * mouseMoveSpeed;
            float angleY = dis.y * mouseMoveSpeed;
            transform.Translate(new Vector3(-angleX, 0, 0), Space.Self);
            if (transform.position.y - angleY >= minimumY)
                transform.Translate(new Vector3(0, -angleY, 0), Space.Self);
        }
    }

    public bool CheckContainMouse()
    {
        bool isIn = this.bindCamera.pixelRect.Contains(Input.mousePosition);
        return isIn;
    }

    public void ChangeCameraRect(Rect newRect)
    {
        this.bindCamera.rect = newRect;
    }

    public void FocusOn(Transform obj, Vector3? offset, Vector3? rot)
    {
        if (!this.isOn)
            return;

        this.followedObject = obj;

        var renderers = obj.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds();
        bool initBounds = false;
        foreach (Renderer r in renderers)
        {
            if (!((r is TrailRenderer) || (r is ParticleSystemRenderer)))
            {
                if (!initBounds)
                {
                    initBounds = true;
                    bounds = r.bounds;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
        }
        if (!offset.HasValue)
        {
            float max = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            distance = max * 1.5f / Mathf.Tan(Mathf.Deg2Rad * bindCamera.fieldOfView / 2);
            this.centerOffset = bounds.center - this.followedObject.position;
        }
        else
        {
            this.distance = 0;
            this.centerOffset = offset.Value;
        }
        if (rot.HasValue)
        {
            this.transform.rotation = Quaternion.Euler(rot.Value);
        }

        this.CreateBoundBox(obj.gameObject);
    }

    public void UnFocus()
    {
        this.followedObject = null;
    }

    Vector3[] boundsVertex;
    static Material lineMaterial;

    static void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }

    void OnRenderObject()
    {
        if (this.followedObject != null)
        {
            if (this.boundsVertex == null || this.boundsVertex.Length < 8)
                return;

            if (!lineMaterial)
                CreateLineMaterial();
            lineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.MultMatrix(this.followedObject.transform.localToWorldMatrix);
            GL.Begin(GL.LINES);
            GL.Color(Color.green);

            GL.Vertex(this.boundsVertex[0]);
            GL.Vertex(this.boundsVertex[1]);
            GL.Vertex(this.boundsVertex[1]);
            GL.Vertex(this.boundsVertex[2]);
            GL.Vertex(this.boundsVertex[2]);
            GL.Vertex(this.boundsVertex[3]);
            GL.Vertex(this.boundsVertex[3]);
            GL.Vertex(this.boundsVertex[0]);

            GL.Vertex(this.boundsVertex[4]);
            GL.Vertex(this.boundsVertex[5]);
            GL.Vertex(this.boundsVertex[5]);
            GL.Vertex(this.boundsVertex[6]);
            GL.Vertex(this.boundsVertex[6]);
            GL.Vertex(this.boundsVertex[7]);
            GL.Vertex(this.boundsVertex[7]);
            GL.Vertex(this.boundsVertex[4]);

            GL.Vertex(this.boundsVertex[0]);
            GL.Vertex(this.boundsVertex[4]);
            GL.Vertex(this.boundsVertex[1]);
            GL.Vertex(this.boundsVertex[5]);
            GL.Vertex(this.boundsVertex[2]);
            GL.Vertex(this.boundsVertex[6]);
            GL.Vertex(this.boundsVertex[3]);
            GL.Vertex(this.boundsVertex[7]);

            GL.End();
            GL.PopMatrix();
        }
    }

    private void CreateBoundBox(GameObject obj)
    {
        Bounds bounds;
        MeshFilter filter = obj.GetComponent<MeshFilter>();
        if (filter != null)
            bounds = filter.mesh.bounds;
        else
            throw new UnityException("Can't find the MeshFilter in " + obj.ToString());

        bounds.Expand(0.02f);
        if (this.boundsVertex == null)
            this.boundsVertex = new Vector3[8];

        this.boundsVertex[0] = bounds.extents;
        this.boundsVertex[1] = new Vector3(-bounds.extents.x, bounds.extents.y, bounds.extents.z);
        this.boundsVertex[2] = new Vector3(-bounds.extents.x, bounds.extents.y, -bounds.extents.z);
        this.boundsVertex[3] = new Vector3(bounds.extents.x, bounds.extents.y, -bounds.extents.z);
        this.boundsVertex[4] = new Vector3(bounds.extents.x, -bounds.extents.y, bounds.extents.z);
        this.boundsVertex[5] = new Vector3(-bounds.extents.x, -bounds.extents.y, bounds.extents.z);
        this.boundsVertex[6] = -bounds.extents;
        this.boundsVertex[7] = new Vector3(bounds.extents.x, -bounds.extents.y, -bounds.extents.z);

        for (int i = 0; i < this.boundsVertex.Length; i++)
        {
            this.boundsVertex[i] += bounds.center;
        }
    }
}
