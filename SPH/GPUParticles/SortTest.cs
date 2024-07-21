using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PhysicsLearning.Math
{
    [ExecuteInEditMode]
    public class SortTest : MonoBehaviour
    {
        public ComputeShader compute;
        private ComputeBuffer _inputBuffer;
        private ComputeBuffer _dataBuffer1;
        private ComputeBuffer _dataBuffer2;
        private int _sortKernel;
        private int _transposeKernel;
        private int _fillKernel;
        private int _copyKernel;
        private const int SortBlockSize = 512;
        private const int TransposeBlockSize = 16;
        private const int CopyBlockSize = 256;
        
        private static readonly int Level = Shader.PropertyToID("_Level");
        private static readonly int LevelMask = Shader.PropertyToID("_LevelMask");
        private static readonly int Width = Shader.PropertyToID("_Width");
        private static readonly int Height = Shader.PropertyToID("_Height");
        private static readonly int Data = Shader.PropertyToID("_Data");
        private static readonly int Input = Shader.PropertyToID("_Input");

        private const int MatrixWidth = 256;
        private const int MatrixHeight = 256;
        private System.Diagnostics.Stopwatch _sw;

        public void OnEnable()
        {
            _sortKernel = compute.FindKernel("BitonicSort");
            _transposeKernel = compute.FindKernel("MatrixTranspose");
            _fillKernel = compute.FindKernel("Fill");
            _copyKernel = compute.FindKernel("Copy");

            _inputBuffer = new ComputeBuffer(MatrixWidth * MatrixHeight, 4);
            _dataBuffer1 = new ComputeBuffer(MatrixWidth * MatrixHeight, 4);
            _dataBuffer2 = new ComputeBuffer(MatrixWidth * MatrixHeight, 4);
            Debug.Log("OnEnable");
            _sw = new System.Diagnostics.Stopwatch();
        }

        public void OnDisable()
        {
            _inputBuffer.Dispose();
            _dataBuffer1.Dispose();
            _dataBuffer2.Dispose();
        }

        public void Dispatch()
        {
            if (compute == null) return;
            Sort();
        }

        private void Sort()
        {
            int totalSize = MatrixWidth * MatrixHeight;
            int[] particles = new int[totalSize];
            // GenerateBitonicArray(particles);
            for (int i = 0; i < totalSize; ++i)
            {
                particles[i] = Random.Range(0, 32768);
            }
            string msg = "";
            _sw.Start();
            _inputBuffer.SetData(particles);
            // copy data
            compute.SetInt(Width, totalSize);
            compute.SetBuffer(_copyKernel, Input, _inputBuffer);
            compute.SetBuffer(_copyKernel, Data, _dataBuffer1);
            compute.Dispatch(_copyKernel,totalSize / CopyBlockSize, 1, 1);
            // sort
            compute.SetBuffer(_sortKernel, Data, _dataBuffer1);
            for(int i = 2; i <= SortBlockSize; i <<= 1)
            {
                compute.SetInt(Level, i);
                compute.SetInt(LevelMask, i);
                compute.Dispatch(_sortKernel, totalSize / SortBlockSize, 1, 1);
            }
            for (int i = SortBlockSize * 2; i <= totalSize; i <<= 1)
            {
                // 如果达到最高等级，则为全递增序列
                if (i == totalSize)
                {
                    compute.SetInt(Level, i / MatrixWidth);
                    compute.SetInt(LevelMask, i);
                }
                else
                {
                    compute.SetInt(Level, i / MatrixWidth);
                    compute.SetInt(LevelMask, i / MatrixWidth);
                }
                
                // transpose
                compute.SetInt(Width, MatrixWidth);
                compute.SetInt(Height, MatrixHeight);
                compute.SetBuffer(_transposeKernel, Input, _dataBuffer1);
                compute.SetBuffer(_transposeKernel, Data, _dataBuffer2);
                compute.Dispatch(_transposeKernel, 
                    MatrixWidth / TransposeBlockSize, MatrixHeight / TransposeBlockSize, 1);
                
                // 对Buffer2排序列数据
                compute.SetBuffer(_sortKernel, Data, _dataBuffer2);
                compute.Dispatch(_sortKernel, totalSize / SortBlockSize, 1, 1);
                
                // 接着转置回来，并把数据输出到Buffer1
                compute.SetInt(Width, MatrixHeight);
                compute.SetInt(Height, MatrixWidth);
                compute.SetBuffer(_transposeKernel, Input, _dataBuffer2);
                compute.SetBuffer(_transposeKernel, Data, _dataBuffer1);
                compute.Dispatch(_transposeKernel, 
                    MatrixHeight / TransposeBlockSize, MatrixWidth / TransposeBlockSize, 1);

                compute.SetInt(Level, MatrixWidth);
                compute.SetInt(LevelMask, i);
                // 对Buffer1排序剩余行数据
                compute.SetBuffer(_sortKernel, Data, _dataBuffer1);
                compute.Dispatch(_sortKernel, totalSize / SortBlockSize, 1, 1);
            }
            _dataBuffer1.GetData(particles);
            _sw.Stop();
            Debug.Log("Array Length:" + particles.Length + string.Format(", 使用时间 {0} ms", _sw.ElapsedMilliseconds));
            _sw.Reset();
            
            msg = "";
            for (int y = 0; y < MatrixWidth; y+=8)
            {
                for (int x = 0; x < MatrixHeight; x+=8)
                {
                    msg += particles[y * MatrixHeight + x] + ", ";
                }
                msg += "\n";
            }
            Debug.Log(msg);
            msg = "Completed";
            for(int i = 0; i < particles.Length - 1; i++)
            {
                if (particles[i] > particles[i+1]) 
                {
                    msg = "Failed";
                    break;
                }
            }
            Debug.Log("Sort Result: " + msg);
        }
    }

    [CustomEditor(typeof(SortTest))]  
    public class MainEditor : Editor  
    {
        public override void OnInspectorGUI()  
        {  
            DrawDefaultInspector();
            SortTest script = (SortTest)target;  
  
            if (GUILayout.Button("reset"))  
            {
                script.Dispatch();  
            }  
        }  
    }
}
