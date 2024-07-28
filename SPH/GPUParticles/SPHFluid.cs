using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class SPHFluid : MonoBehaviour
{
    struct Particles
    {
        public Vector3 position;
        public Vector3 velocity;
        public float density;
    }
    public bool enable = false;
    public float gravity = -9.8f;
    [Range(0,1)]
    public float collisionDamping = 0.9f;
    public GameObject bounds;
    public Mesh particleMesh;
    public Material material;
    private Particles[] particles;
    private Vector3 scale;
    public float smoothRadius = 0.7f;
    [Range(1,1000)]
    public float targetDensity = 1;
    public int pressureMultiplier;
    [Range(0,5)]
    public float mass = 1;
    [Range(0,1)]
    public float viscosityStrength = 0.9f;

    public GameObject go;
    
    const int width = 64;
    private Camera mainCam;
    private const int MatrixWidth = 256;
    private const int MatrixHeight = 128;
    const int Count = 256*128;
    private ComputeBuffer _inputBuffer;
    private ComputeBuffer _dataBuffer;
    private ComputeBuffer _spatialBuffer;
    private ComputeBuffer _spatialBuffer2;
    private ComputeBuffer _indicesBuffer;
    private ComputeBuffer _neighborBuffer;

    public ComputeShader sort;
    public ComputeShader spatial;
    public ComputeShader sph;
    private int _spatialKernel;
    private int _densityKernel;
    private int _forceKernel;
    private int _sortKernel;
    private int _indicesKernel;
    private int _transposeKernel;
    private int _lutKernel;
    private Vector3 _waterBound;

    #region ShaderParams
    
    private const int SortBlockSize = 512;
    private const int TransposeBlockSize = 16;
    private const int CopyBlockSize = 256;
    private const int SphBlockSize = 512;
    
    private readonly int _particleBuffer = Shader.PropertyToID("_Particles");
    private readonly int _spatialLut = Shader.PropertyToID("_SpatialLut");
    private readonly int _startIndices = Shader.PropertyToID("_StartIndices");
    private readonly int _count = Shader.PropertyToID("_Count");
    private readonly int _radius = Shader.PropertyToID("_Radius");
    private readonly int _length = Shader.PropertyToID("_Length");
    private readonly int _mass = Shader.PropertyToID("_Mass");
    private readonly int _smoothRadius = Shader.PropertyToID("_SmoothRadius");
    private readonly int _detalTime = Shader.PropertyToID("_DeltaTime");
    private readonly int _bound = Shader.PropertyToID("_Bound");
    private readonly int _collisionDamping = Shader.PropertyToID("_CollisionDamping");
    private readonly int _viscosityStrength = Shader.PropertyToID("_ViscosityStrength");
    private readonly int _pressureMultiplier = Shader.PropertyToID("_PressureMultiplier");
    private readonly int _targetDensity = Shader.PropertyToID("_TargetDensity");
    private readonly int _gravity = Shader.PropertyToID("_Gravity");
    
    private static readonly int Level = Shader.PropertyToID("_Level");
    private static readonly int LevelMask = Shader.PropertyToID("_LevelMask");
    private static readonly int Width = Shader.PropertyToID("_Width");
    private static readonly int Height = Shader.PropertyToID("_Height");
    private static readonly int Data = Shader.PropertyToID("_Data");
    private static readonly int Input = Shader.PropertyToID("_Input");
    private static readonly int Result = Shader.PropertyToID("_Result");

    #endregion
    
    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }
    void Start()
    {
        particles = new Particles[Count];
        
        Vector3 bound = bounds.transform.localScale * 0.6f;
        for (int i = 0; i < Count; i++)
        {
            particles[i].position = new Vector3( Random.Range(-bound.x, bound.x), Random.Range(-bound.y, bound.y), Random.Range(-bound.z, bound.z));
            particles[i].density = 1;
            particles[i].velocity = Vector3.zero;
        }

        _waterBound = bound;
        _inputBuffer = new ComputeBuffer(Count, 28);
        _dataBuffer = new ComputeBuffer(Count, 28);
        _spatialBuffer = new ComputeBuffer(Count, 8);
        _spatialBuffer2 = new ComputeBuffer(Count, 8);
        _indicesBuffer = new ComputeBuffer(Count, 4);
        _neighborBuffer = new ComputeBuffer(128, 4);
        
        _dataBuffer.SetData(particles);
        
        mainCam = Camera.main;
        _spatialKernel = spatial.FindKernel("SpatialLUT");
        _densityKernel = sph.FindKernel("ParticleDensity");
        _forceKernel = sph.FindKernel("ParticlePressure");
        _sortKernel = sort.FindKernel("BitonicSort");
        _indicesKernel = sort.FindKernel("GetIndices");
        _transposeKernel = sort.FindKernel("MatrixTranspose");
        _lutKernel = sph.FindKernel("Lut");
        
        material.SetBuffer(_particleBuffer, _dataBuffer);
        // material.SetBuffer(Result, _neighborBuffer);
        
        spatial.SetBuffer(_spatialKernel, _particleBuffer, _dataBuffer);
        spatial.SetBuffer(_spatialKernel, _spatialLut, _spatialBuffer);
        
        sort.SetBuffer(_indicesKernel, _spatialLut, _spatialBuffer);
        sort.SetBuffer(_indicesKernel, _startIndices, _indicesBuffer);
        
        sph.SetBuffer(_forceKernel, _particleBuffer, _dataBuffer);
        sph.SetBuffer(_forceKernel, _spatialLut, _spatialBuffer);
        sph.SetBuffer(_forceKernel, _startIndices, _indicesBuffer);
        
        sph.SetBuffer(_densityKernel, _particleBuffer, _dataBuffer);
        sph.SetBuffer(_densityKernel, _startIndices, _indicesBuffer);
        sph.SetBuffer(_densityKernel, _spatialLut, _spatialBuffer);
        
        sph.SetBuffer(_lutKernel, _particleBuffer, _dataBuffer);
        sph.SetBuffer(_lutKernel, _startIndices, _indicesBuffer);
        sph.SetBuffer(_lutKernel, _spatialLut, _spatialBuffer);
        sph.SetBuffer(_lutKernel, Result, _neighborBuffer);
    }
    public void Initialize()
    {
        Vector3 bound = bounds.transform.localScale * 0.6f;
        for (int i = 0; i < Count; i++)
        {
            particles[i].position = new Vector3( Random.Range(-bound.x, bound.x), Random.Range(-bound.y, bound.y), Random.Range(-bound.z, bound.z));
            particles[i].density = 1;
            particles[i].velocity = new Vector3( Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0);
            particles[i].velocity = Vector3.zero;
        }
        _dataBuffer.SetData(particles);
    }
    public void Dispatch()
    {
        // MyUpdate();
        Vector2Int[] sortedData = new Vector2Int[Count];
        _spatialBuffer.GetData(sortedData);
            
        string msg = "";
        for (int y = 0; y < MatrixWidth; y+=8)
        {
            for (int x = 0; x < MatrixHeight; x+=8)
            {
                msg += sortedData[y * MatrixHeight + x].x + ", ";
            }
            msg += "\n";
        }
        Debug.Log(msg);
        
        int[] sortedIndices = new int[Count];
        _indicesBuffer.GetData(sortedIndices);
            
        msg = "";
        for (int y = 0; y < MatrixWidth; y+=8)
        {
            for (int x = 0; x < MatrixHeight; x+=8)
            {
                msg += sortedIndices[y * MatrixHeight + x] + ", ";
            }
            msg += "\n";
        }
        Debug.Log(msg);
        
        int[] neighbors = new int[128];
        _neighborBuffer.GetData(neighbors);
            
        msg = "";
        for (int i = 0; i < 128; i++)
        {
            msg += neighbors[i] + ", ";
        }
        Debug.Log(msg);
    }
    
    void Update()
    {
        var localScale = bounds.transform.localScale;
        // Add to Spatial Lut
        spatial.SetFloat(_detalTime, Time.deltaTime * 2);
        spatial.SetFloat(_gravity, gravity);
        spatial.SetFloat(_radius, Mathf.Max(smoothRadius, 0.02f));
        spatial.SetInt(_length, Count);
        spatial.SetVector(_bound, localScale);
        spatial.Dispatch(_spatialKernel, Count / SphBlockSize, 1, 1);
        // sort by cellkey
        Sort();
        // get start indices
        sort.SetInt(Width, Count);
        sort.Dispatch(_indicesKernel, Count / SphBlockSize, 1, 1);
        
        sph.SetFloat(_radius, Mathf.Max(smoothRadius, 0.01f));
        sph.SetInt(_length, Count);
        sph.SetFloat(_mass, mass);
        sph.SetFloat(_detalTime, Time.deltaTime * 2);
        sph.SetFloat(_viscosityStrength, viscosityStrength);
        sph.SetInt(_pressureMultiplier, pressureMultiplier);
        sph.SetFloat(_targetDensity, targetDensity);
        sph.SetFloat(_collisionDamping, collisionDamping);
        sph.SetVector(_bound, localScale);
        
        // sph.SetVector("_Pos", go.transform.position);
        // sph.Dispatch(_lutKernel, 1, 1, 1);
        sph.Dispatch(_densityKernel, Count / SphBlockSize, 1, 1);
        
        sph.Dispatch(_forceKernel, Count / SphBlockSize, 1, 1);
        
        Graphics.DrawMeshInstancedProcedural(particleMesh, 0, material, new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f)), Count);
    }
    private void Sort()
    {
        int totalSize = MatrixWidth * MatrixHeight;
        // copy data
        // sort.SetInt(Width, totalSize);
        // sort.SetBuffer(_copyKernel, Input, _inputBuffer);
        // sort.SetBuffer(_copyKernel, Data, _dataBuffer);
        // sort.Dispatch(_copyKernel,totalSize / CopyBlockSize, 1, 1);
        
        // sort
        sort.SetBuffer(_sortKernel, Data, _spatialBuffer);
        for(int i = 2; i <= SortBlockSize; i <<= 1)
        {
            sort.SetInt(Level, i);
            sort.SetInt(LevelMask, i);
            sort.Dispatch(_sortKernel, totalSize / SortBlockSize, 1, 1);
        }
        for (int i = SortBlockSize * 2; i <= totalSize; i <<= 1)
        {
            // 如果达到最高等级，则为全递增序列
            if (i == totalSize)
            {
                sort.SetInt(Level, i / MatrixWidth);
                sort.SetInt(LevelMask, i);
            }
            else
            {
                sort.SetInt(Level, i / MatrixWidth);
                sort.SetInt(LevelMask, i / MatrixWidth);
            }
            
            // transpose
            sort.SetInt(Width, MatrixWidth);
            sort.SetInt(Height, MatrixHeight);
            sort.SetBuffer(_transposeKernel, Input, _spatialBuffer);
            sort.SetBuffer(_transposeKernel, Data, _spatialBuffer2);
            sort.Dispatch(_transposeKernel, 
                MatrixWidth / TransposeBlockSize, MatrixHeight / TransposeBlockSize, 1);
            
            // 对Buffer2排序列数据
            sort.SetBuffer(_sortKernel, Data, _spatialBuffer2);
            sort.Dispatch(_sortKernel, totalSize / SortBlockSize, 1, 1);
            
            // 接着转置回来，并把数据输出到Buffer1
            sort.SetInt(Width, MatrixHeight);
            sort.SetInt(Height, MatrixWidth);
            sort.SetBuffer(_transposeKernel, Input, _spatialBuffer2);
            sort.SetBuffer(_transposeKernel, Data, _spatialBuffer);
            sort.Dispatch(_transposeKernel, 
                MatrixHeight / TransposeBlockSize, MatrixWidth / TransposeBlockSize, 1);

            sort.SetInt(Level, MatrixWidth);
            sort.SetInt(LevelMask, i);
            // 对Buffer1排序剩余行数据
            sort.SetBuffer(_sortKernel, Data, _spatialBuffer);
            sort.Dispatch(_sortKernel, totalSize / SortBlockSize, 1, 1);
        }
        // _sw.Stop();
        // Debug.Log("Array Length:" + particles.Length + $", 使用时间 {_sw.ElapsedMilliseconds} ms");
        // _sw.Reset();
    }

    private void OnDestroy()
    {
        _inputBuffer.Release();
        _dataBuffer.Release();
        _spatialBuffer.Release();
        _spatialBuffer2.Release();
        _indicesBuffer.Release();
        _neighborBuffer.Release();
    }
}

    [CustomEditor(typeof(SPHFluid))]  
    public class SPHFluidEditor : Editor  
    {
        public override void OnInspectorGUI()  
        {  
            DrawDefaultInspector();
            SPHFluid script = (SPHFluid)target;  
  
            if (GUILayout.Button("reset"))  
            {
                script.Initialize();  
            }  
        }  
    }