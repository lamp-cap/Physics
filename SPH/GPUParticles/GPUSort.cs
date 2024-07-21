using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MyMathUntility
{
    public class GPUSort
    {
        public static ComputeShader sortCompute;
        public static void Sort(ComputeBuffer values)
        {
            sortCompute.SetBuffer(0, "Values", values);
            sortCompute.SetInt("numValues", values.count);
            // Launch each step of the sorting algorithm (once the previous step is complete)
            //Number ofsteps=[log2(n)*(log2(n)+1)]/2
            // where n= nearest power of 2 that is greater or equal to the number of inputs
            int numPairs = Mathf.NextPowerOfTwo(values.count)/2;
            int numStages =(int)Mathf.Log(numPairs*2, 2);
            for(int stageIndex = 0; stageIndex<numStages; stageIndex++)
            for(int stepIndex = 0; stepIndex<stageIndex + 1; stepIndex++)
            {
                // Calculate some pattern stuff
                int groupWidth = 1<< (stageIndex-stepIndex);
                int groupHeight = 2 * groupWidth-1;
                
                sortCompute.SetInt("groupWidth", groupWidth);
                sortCompute.SetInt("groupHeight", groupHeight);
                sortCompute.SetInt("stepIndex", stepIndex);
                //Run the pair-wise sorting step on the GPU
                sortCompute.Dispatch(0, numPairs, 1, 1);
            }
        }
    }
}
