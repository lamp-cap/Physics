using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MyMathUntility;

// [ExecuteAlways]
public class Main : MonoBehaviour
{
    struct Particles
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 positionPredicted;
        public float density;
    }
    public bool enable = false;
    public float gravity = -9.8f;
    [Range(0,1)]
    public float collisionDamping = 0.9f;
    public GameObject bounds;
    public Mesh mesh;
    public Material material;
    private Matrix4x4[] matrices;
    private Particles[] particles;
    private Vector3 scale;
    const int count = 1500;
    public float smoothRadius = 0.7f;
    [Range(0,100)]
    public float targetDensity = 1;
    public float pressureMultiplier;
    [Range(0,5)]
    public float mass = 1;
    [Range(0,1)]
    public float viscosityStrength = 0.9f;
    public float temp = 0.1f;
    const int width = 64;
    private HashCellLut lut;
    private Camera mainCam;
    // Start is called before the first frame update
    void Start()
    {
        matrices = new Matrix4x4[count];
        particles = new Particles[count];
        lut = new HashCellLut(count);
        scale = new Vector3(0.5f, 0.5f, 0.5f);
        // rt = new Texture2D(width, width);
        // planeMat.mainTexture = rt;
        Vector3 bound = bounds.transform.localScale * 0.9f;
        for (int i = 0; i < count; i++)
        {
            particles[i].position = new Vector3( Random.Range(-bound.x, bound.x), Random.Range(-bound.y, bound.y), 0);
            particles[i].positionPredicted = particles[i].position;
            particles[i].velocity = Vector3.zero;
            matrices[i] = Matrix4x4.TRS(particles[i].position, Quaternion.identity, scale);  
        }
        mainCam = Camera.main;
        // Initialize();
    }
    void Awake()
    {
        //垂直同步计数设置为0，才能锁帧，否则锁帧代码无效。
        QualitySettings.vSyncCount = 0;
        //设置游戏帧数
        Application.targetFrameRate = 60;
    }

    public void Initialize()
    {
        for (int i = 0; i < count; i++)
        {
            particles[i].position = new Vector3( i / 30 * 0.12f - 2, i % 30 * 0.12f - 2, 0);
            particles[i].positionPredicted = particles[i].position;
            particles[i].velocity = Vector3.zero;
            matrices[i] = Matrix4x4.TRS(particles[i].position, Quaternion.identity, scale);  
        }

        /* Vector3 bound = bounds.transform.localScale;
        for(int i = 0; i < width; ++i)
        for(int j = 0; j < width; ++j)
        {
            Vector3 point = new Vector3((float)i/width*bound.x*2-bound.x, (float)j/width*bound.y*2-bound.y, 0);
            // float density = CalculateProperty(-1*point)/20;
            // rt.SetPixel(i, j, new Color(density, density, density, 1));
            Vector3 res = CalculatePressureForce(point) * temp;
            rt.SetPixel(i, j, new Color(res.x, res.y, -res.x));
        }
        rt.Apply();
        */
    }

    float ConvertDensityToPressure(float density)
    {
        float densityError = density - targetDensity;
        float pressure = densityError * pressureMultiplier;
        // float nearPressure = densityError * nearPressureMultiplier;
        return pressure;
    }

    // Update is called once per frame
    void Update()
    {
        if(!enable) return;

        if(Input.GetMouseButton(0))
        {
            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500))
            {
                Vector3 bound = bounds.transform.localScale;
                Vector2 coord = (Vector2.one - hit.textureCoord) * new Vector2(bound.x, bound.y) * 2 - new Vector2(bound.x, bound.y);
                lut.GetPointsWithinRadius(coord,
                    delegate (int id)
                    {
                        float radius = 1;
                        Vector3 dir = new Vector3(coord.x, coord.y, 0) - particles[id].position;
                        float dst = dir.magnitude;
                        if(dst < 0.001f || dst >= radius) return;

                        particles[id].velocity -= 5 * (radius - dst) * dir / dst;
                    } 
                );
            }
        }

        UpdateParticles();

        Graphics.DrawMeshInstanced(mesh, 0, material, matrices);
    }

    private void UpdateDensity()
    {
        for(int i = 0; i < count; ++i)
        {
            Vector3 pos = particles[i].positionPredicted;
            float density = 0;
            lut.GetPointsWithinRadius(new Vector2(pos.x, pos.y), 
                delegate(int id)
                {
                    if(i == id) return;
                    
                    float dst = (particles[id].positionPredicted - pos).magnitude;
                    float influence = SmoothKernel(smoothRadius, dst);
                    density += mass * influence;
                }
            );

            particles[i].density = Mathf.Max(0.001f, density);
        }
    }
    
    private void UpdatePressureForce()
    {
        for(int i = 0; i < count; ++i)
        {
            Vector2 pos = new Vector2(particles[i].positionPredicted.x, particles[i].positionPredicted.y);
            Vector3 force = Vector3.zero;
            lut.GetPointsWithinRadius(pos,
                delegate (int id)
                {
                    if(i == id) return;
                    
                    Vector3 dir = particles[id].positionPredicted - particles[i].positionPredicted;
                    float dst = dir.magnitude;

                    if(dst > smoothRadius * smoothRadius * 1.1f) return;

                    dir = dst < 0.0001f ? new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0)
                                        : dir / dst;
                    
                    float influence = ViscositySmoothingKernel(smoothRadius, dst);
                    Vector3 viscosityForce = (particles[i].velocity - particles[id].velocity) * influence;

                    float slope = SmoothKernelDerivative(smoothRadius, dst);
                    float density = particles[id].density;
                    float sharedPressure = (ConvertDensityToPressure(density) + ConvertDensityToPressure(particles[i].density)) / 2;
                    force += (sharedPressure * dir + viscosityForce * viscosityStrength * pressureMultiplier) * mass * slope / density;
                }
            );
            particles[i].velocity = particles[i].velocity + force / particles[i].density * Time.deltaTime;
        }
    }

    private void UpdateParticles()
    {
        lut.StartUpdateLut(smoothRadius);
        for (int i = 0; i < count; i++)
        {
            particles[i].velocity += Vector3.down * (gravity * Time.deltaTime);
            particles[i].positionPredicted = particles[i].position + particles[i].velocity * 0.01f;

            lut.UpdateSpatialLookup(new Vector2(particles[i].positionPredicted.x, particles[i].positionPredicted.y), i);
        }
        lut.FinishUpdateLut();

        UpdateDensity();
        UpdatePressureForce();

        for (int i = 0; i < count; i++)
        {
            float th = 2;
            particles[i].velocity.x = Mathf.Min(particles[i].velocity.x, th);
            particles[i].velocity.y = Mathf.Min(particles[i].velocity.y, th);
            particles[i].velocity.z = Mathf.Min(particles[i].velocity.z, th);

            particles[i].position += particles[i].velocity * Time.deltaTime;
            
            ResolveCollisions(ref particles[i]);

            matrices[i] = Matrix4x4.TRS(particles[i].position, Quaternion.identity, scale);  
        }
    }

    private void ResolveCollisions(ref Particles particles)
    {
        Vector3 bound = bounds.transform.localScale;
        if(Mathf.Abs(particles.position.x) > bound.x)
        {
            particles.position.x = bound.x * Mathf.Sign(particles.position.x);
            particles.velocity.x *= -1 * collisionDamping;
        }
        if(Mathf.Abs(particles.position.y) > bound.y)
        {
            particles.position.y = bound.y * Mathf.Sign(particles.position.y);
            particles.velocity.y *= -1 * collisionDamping;
        }
        if(Mathf.Abs(particles.position.z) > bound.z)
        {
            particles.position.z = bound.z * Mathf.Sign(particles.position.z);
            particles.velocity.z *= -1 * collisionDamping;
        }
    }

    private float SmoothKernel(float radius, float dst)
    {
        if(dst >= radius) return 0;

        float r4 = radius * radius * radius * radius;
        float volume = Mathf.PI * r4 / 6;
        float value = radius - dst;
        return value * value / volume;
    }

    private float ViscositySmoothingKernel(float radius, float dst)
    {
        if(dst >= radius) return 0;

        float r4 = radius * radius * radius * radius;
        float volume = Mathf.PI * r4 * r4 / 4;
        float value = radius * radius - dst * dst;
        return value * value * value / volume;
    }
    private float SmoothKernelDerivative(float radius, float dst)
    {
        if(dst >= radius) return 0;
        
        float r4 = radius * radius * radius * radius;
        float scale = 12 / (Mathf.PI * r4);
        return scale * (dst - radius);
    }
}

[CustomEditor(typeof(Main))]  
public class MainEditor : Editor  
{  
    public override void OnInspectorGUI()  
    {  
        DrawDefaultInspector(); // 保留默认的Inspector面板  
  
        Main script = (Main)target;  
  
        if (GUILayout.Button("reset"))  
        {  
            // 当按钮被点击时执行的操作  
            script.Initialize();  
        }  
    }  
}