using System.Collections.Generic;
using UnityEngine;

public class PBD_model : MonoBehaviour
{
    private const float t = 0.0333f;
    private const float damping = 0.99f;
    int[] E;
    float[] L;
    Vector3[] V;
    Vector3 gravity = new Vector3(0, -9.8f, 0);
    private Mesh _mesh;
    public GameObject sphere;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }
    // Use this for initialization
    void Start()
    {
        _mesh = GetComponent<MeshFilter>().sharedMesh;
 
        //Resize the mesh.
        int n = 21;
        Vector3[] X = new Vector3[n * n];
        Vector2[] UV = new Vector2[n * n];
        int[] T = new int[(n - 1) * (n - 1) * 6];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                X[j * n + i] = new Vector3(5 - 10.0f * i / (n - 1), 0, 5 - 10.0f * j / (n - 1));
                UV[j * n + i] = new Vector3(i / (n - 1.0f), j / (n - 1.0f));
            }
        int t = 0;
        for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                T[t * 6 + 0] = j * n + i;
                T[t * 6 + 1] = j * n + i + 1;
                T[t * 6 + 2] = (j + 1) * n + i + 1;
                T[t * 6 + 3] = j * n + i;
                T[t * 6 + 4] = (j + 1) * n + i + 1;
                T[t * 6 + 5] = (j + 1) * n + i;
                t++;
            }
        _mesh.vertices = X;
        _mesh.triangles = T;
        _mesh.uv = UV;
        _mesh.RecalculateNormals();
 
        //Construct the original edge list
        int[] _E = new int[T.Length * 2];
        for (int i = 0; i < T.Length; i += 3)
        {
            _E[i * 2 + 0] = T[i + 0];
            _E[i * 2 + 1] = T[i + 1];
            _E[i * 2 + 2] = T[i + 1];
            _E[i * 2 + 3] = T[i + 2];
            _E[i * 2 + 4] = T[i + 2];
            _E[i * 2 + 5] = T[i + 0];
        }
        //Reorder the original edge list
        //Guarantee smaller index in front of bigger one
        for (int i = 0; i < _E.Length; i += 2)
            if (_E[i] > _E[i + 1])
                Swap(ref _E[i], ref _E[i + 1]);
        //Sort the original edge list using quicksort
        Quick_Sort(ref _E, 0, _E.Length / 2 - 1);
 
        int e_number = 0;
        for (int i = 0; i < _E.Length; i += 2)
            if (i == 0 || _E[i + 0] != _E[i - 2] || _E[i + 1] != _E[i - 1])
                e_number++;
 
        E = new int[e_number * 2];
        for (int i = 0, e = 0; i < _E.Length; i += 2)
            if (i == 0 || _E[i + 0] != _E[i - 2] || _E[i + 1] != _E[i - 1])
            {
                E[e * 2 + 0] = _E[i + 0];
                E[e * 2 + 1] = _E[i + 1];
                e++;
            }
 
        L = new float[E.Length / 2];
        for (int e = 0; e < E.Length / 2; e++)
        {
            int i = E[e * 2 + 0];
            int j = E[e * 2 + 1];
            L[e] = (X[i] - X[j]).magnitude;
        }
 
        V = new Vector3[X.Length];
        for (int i = 0; i < X.Length; i++)
            V[i] = new Vector3(0, 0, 0);
    }
 
    void Quick_Sort(ref int[] a, int l, int r)
    {
        int j;
        if (l < r)
        {
            j = Quick_Sort_Partition(ref a, l, r);
            Quick_Sort(ref a, l, j - 1);
            Quick_Sort(ref a, j + 1, r);
        }
    }
 
    int Quick_Sort_Partition(ref int[] a, int l, int r)
    {
        int i, j;
        int pivot0 = a[l * 2 + 0];
        int pivot1 = a[l * 2 + 1];
        i = l;
        j = r + 1;
        while (true)
        {
            do ++i; while (i <= r && (a[i * 2] < pivot0 || a[i * 2] == pivot0 && a[i * 2 + 1] <= pivot1));
            do --j; while (a[j * 2] > pivot0 || a[j * 2] == pivot0 && a[j * 2 + 1] > pivot1);
            if (i >= j) break;
            Swap(ref a[i * 2], ref a[j * 2]);
            Swap(ref a[i * 2 + 1], ref a[j * 2 + 1]);
        }
        Swap(ref a[l * 2 + 0], ref a[j * 2 + 0]);
        Swap(ref a[l * 2 + 1], ref a[j * 2 + 1]);
        return j;
    }
 
    void Swap(ref int a, ref int b)
    {
        (a, b) = (b, a);
    }
 
    void Strain_Limiting()
    {
        Vector3[] vertices = _mesh.vertices;
        Vector3[] sum_x = new Vector3[vertices.Length];
        int[] sum_n = new int[vertices.Length];
        //Apply PBD here.
        for (int i = 0; i < vertices.Length; i++)
        {
            sum_x[i] = new Vector3(0, 0, 0);
            sum_n[i] = 0;
        }
        for (int e = 0; e < L.Length; e++)
        {
            int i = E[e * 2];
            int j = E[e * 2 + 1];
            Vector3 xij = vertices[i] - vertices[j];
            sum_x[i] += 0.5f * (vertices[i] + vertices[j] + xij * (L[e] * (1.0f / xij.magnitude)));
            sum_x[j] += 0.5f * (vertices[i] + vertices[j] - xij * (L[e] * (1.0f / xij.magnitude)));
            sum_n[i]++;
            sum_n[j]++;
        }
        for (int i = 0; i < vertices.Length; i++)
        {
            if (i == 0 || i == 20) continue;
            V[i] += (1.0f / t) * ((0.2f * vertices[i] + sum_x[i]) / (0.2f + (float)sum_n[i]) - vertices[i]);
            vertices[i] = (0.2f * vertices[i] + sum_x[i]) / (0.2f + (float)sum_n[i]);
        }
        _mesh.vertices = vertices;
    }
 
    void Collision_Handling()
    {
        Vector3[] X = _mesh.vertices;
 
        //For every vertex, detect collision and apply impulse if needed.
        float radius = 2.7f;
        Vector3 center = sphere.transform.position;
        for (int i = 0; i < X.Length; i++)
        {
            if (i == 0 || i == 20)
                continue;
            Vector3 d = X[i] - center;
            if (d.magnitude < radius)
            {
                Vector3 A = center + radius * d.normalized;
                V[i] = V[i] + (A - X[i]) / t;
                X[i] = A;
            }
        }
 
        _mesh.vertices = X;
    }
 
    // Update is called once per frame
    void Update()
    {
        Vector3[] X = _mesh.vertices;
 
        for (int i = 0; i < X.Length; i++)
        {
            if (i == 0 || i == 20) continue;
            //Initial Setup
            V[i] = V[i] * damping;
            V[i] = V[i] + gravity * t;
            X[i] = X[i] + V[i] * t;
        }
        _mesh.vertices = X;
 
        for (int l = 0; l < 32; l++)
            Strain_Limiting();
 
        Collision_Handling();
 
        _mesh.RecalculateNormals();
 
    }
 
 
}
