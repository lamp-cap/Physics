
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PhysicsLearning.Water
{
    public class GPUParticles : MonoBehaviour
    {
        struct Particle
        {
            public Vector3 Position;     //起始位置
            public Vector3 Velocity;      //更新位置
        }

        public float gravity;
        [Range(0, 1)] public float damping;
        public GameObject bound;
        
        int ThreadBlockSize = 512;  //线程组大小
        int blockPerGrid;       //每个组
        private ComputeBuffer _particleBuffer;
        private int _number;  //粒子数目
        public int width, height; //设置长宽范围
        public int interval;        //间隔距离
        [SerializeField]
        private Mesh particleMesh;         //粒子网格
        [SerializeField] private ComputeShader computeShader;       //申明computeshader
        [SerializeField]
        private Material material;     //粒子材质

        private int _kernelID;

        private static readonly int Gravity = Shader.PropertyToID("_Gravity");
        private static readonly int Damping = Shader.PropertyToID("_Damping");
        private static readonly int Buffer = Shader.PropertyToID("_ParticleBuffer");
        private static readonly int Bound = Shader.PropertyToID("_Bound");

        public void Initialize()
        {
            Particle[] particles = new Particle[_number];    //创建粒子数组
            //粒子的开始位置设0
            for (int i = 0; i < width; ++i)         //遍历设置粒子位置
            {
                for (int j = 0; j < height; ++j)
                {
                    int id = i * height + j;
                    float x = (float)i / (width - 1);
                    float y = (float)j / (height - 1);
                    particles[id].Position = new Vector3(x * interval + Random.Range(0, 1f) - interval*0.5f, y * interval + Random.Range(0, 1f), 1);
                    particles[id].Velocity = Vector3.zero;
                }
            }
            //setdata
            _particleBuffer.SetData(particles);
        }
        private void Start()
        {
            _kernelID = computeShader.FindKernel("CSMain");
            _number = width * height;
            
            blockPerGrid = (_number + ThreadBlockSize - 1) / ThreadBlockSize;    
            _particleBuffer = new ComputeBuffer(_number, 24);
            
            Initialize();
            
            material.SetBuffer(Buffer, _particleBuffer);
            computeShader.SetBuffer(_kernelID, Buffer, _particleBuffer);
        }
        
        void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            UpdateComputeShader();
            // Graphics.DrawMeshInstancedIndirect(particleMesh, 0, _material, new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f)), argsBuffer);
            Graphics.DrawMeshInstancedProcedural(particleMesh, 0, material, new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f)), _number);
        }

        private void UpdateComputeShader()
        {
            computeShader.SetFloat(Gravity, gravity);
            computeShader.SetFloat(Damping, damping);
            computeShader.SetVector(Bound, bound.transform.localScale);
            computeShader.Dispatch(_kernelID, blockPerGrid, 1, 1);
        }

        private void OnDisable()
        {
            _particleBuffer.Dispose();
            
        }
    }

    [CustomEditor(typeof(GPUParticles))]  
    public class MainEditor : Editor  
    {
        public override void OnInspectorGUI()  
        {  
            DrawDefaultInspector(); // 保留默认的Inspector面板  
  
            GPUParticles script = (GPUParticles)target;  
  
            if (GUILayout.Button("reset"))  
            {  
                // 当按钮被点击时执行的操作  
                script.Initialize();  
            }  
        }  
    }
}
