using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PBDCloth : MonoBehaviour
{
    private struct Constraint
    {
        public int index0;
        public int index1;
        public float restLength;
    }
    private const float t = 0.04f;
    private const float invt = 25;
    private const float damping = 0.99f;
    private Vector3[] _velocity;
    Vector3 gravity = new Vector3(0, -5f, 0);
    public Mesh mesh;
    private Mesh _mesh;
    public GameObject sphere;
    private Vector3[] _vertices;
    private Constraint[] _constraints;

    private static readonly int ClothDataBuffer = Shader.PropertyToID("_ClothDataBuffer");

        Vector3[] sum_force;
        int[] sum_n;
    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }
    // Start is called before the first frame update
    
    void Start()
    {
        _mesh = Instantiate(mesh);
 
        _vertices = _mesh.vertices;

        sum_force = new Vector3[_vertices.Length];
        sum_n = new int[_vertices.Length];
        for (int i = 0; i<_vertices.Length; ++i)
        {
            sum_force[i] = Vector3.zero;
            sum_n[i] = 0;
        }
        //Resize the _mesh.
        int n = 21;
        Vector3[] X = new Vector3[n * n];
        Vector2[] UV = new Vector2[n * n];
        int[] triangles = new int[(n - 1) * (n - 1) * 6];
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
                triangles[t * 6 + 0] = j * n + i;
                triangles[t * 6 + 1] = j * n + i + 1;
                triangles[t * 6 + 2] = (j + 1) * n + i + 1;
                triangles[t * 6 + 3] = j * n + i;
                triangles[t * 6 + 4] = (j + 1) * n + i + 1;
                triangles[t * 6 + 5] = (j + 1) * n + i;
                t++;
            }
        _mesh.vertices = X;
        _mesh.triangles = triangles;
        _mesh.uv = UV;
        _mesh.RecalculateNormals();
 
        //Construct the original edge list
        int[] indices = new int[triangles.Length * 2];
        for (int i = 0; i < triangles.Length; i += 3)
        {
            indices[i * 2 + 0] = triangles[i + 0];
            indices[i * 2 + 1] = triangles[i + 1];
            indices[i * 2 + 2] = triangles[i + 1];
            indices[i * 2 + 3] = triangles[i + 2];
            indices[i * 2 + 4] = triangles[i + 2];
            indices[i * 2 + 5] = triangles[i + 0];
        }
        //Reorder the original edge list
        //Guarantee smaller index in front of bigger one
        for (int i = 0; i < indices.Length; i += 2)
            if (indices[i] > indices[i + 1])
                Swap(ref indices[i], ref indices[i + 1]);
        //Sort the original edge list using quicksort
        Quick_Sort(ref indices, 0, indices.Length / 2 - 1);
 
        int e_number = 0;
        for (int i = 0; i < indices.Length; i += 2)
            if (i == 0 || indices[i + 0] != indices[i - 2] || indices[i + 1] != indices[i - 1])
                e_number++;
 
        _constraints = new Constraint[e_number];
        for (int i = 0, e = 0; i < indices.Length; i += 2)
            if (i == 0 || indices[i + 0] != indices[i - 2] || indices[i + 1] != indices[i - 1])
            {
                _constraints[e] = new Constraint
                {
                    index0 = indices[i + 0],
                    index1 = indices[i + 1],
                    restLength = (X[indices[i + 1]] - X[indices[i + 0]]).magnitude
                };
                e++;
            }
 
        _velocity = new Vector3[X.Length];
        for (int i = 0; i < X.Length; i++)
            _velocity[i] = new Vector3(0, 0, 0);
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
        //Apply PBD here.
        for (int e = 0; e < _constraints.Length; e++)
        {
            int i = _constraints[e].index0;
            int j = _constraints[e].index1;
            Vector3 xij = _vertices[i] - _vertices[j];
            Vector3 force = xij.normalized * _constraints[e].restLength;
            sum_force[i] += 0.5f * (_vertices[i] + _vertices[j] + force);
            sum_force[j] += 0.5f * (_vertices[i] + _vertices[j] - force);
            sum_n[i]++;
            sum_n[j]++;
        }
        for (int i = 0; i < _vertices.Length; i++)
        {
            if (i == 0 || i == 20) continue;
            float k = 0.00001f;
            _velocity[i] += invt * ((k * _vertices[i] + sum_force[i]) / (k + sum_n[i]) - _vertices[i]);
            _vertices[i] = (k * _vertices[i] + sum_force[i]) / (k + sum_n[i]);
            // _velocity[i] += invt * sum_force[i] / Mathf.Max(sum_n[i], 1) - _vertices[i];
            // _vertices[i] = sum_force[i] / Mathf.Max(sum_n[i],1);
            
            sum_force[i] = Vector3.zero;
            sum_n[i] = 0;
        }
    }
 
    void Collision_Handling()
    {
        //For every vertex, detect collision and apply impulse if needed.
        float radius = 2.7f;
        Vector3 center = sphere.transform.position;
        for (int i = 0; i < _vertices.Length; i++)
        {
            if (i == 0 || i == 20)
                continue;
            Vector3 d = _vertices[i] - center;
            if (d.magnitude < radius)
            {
                Vector3 point = center + radius * d.normalized;
                _velocity[i] = _velocity[i] + (point - _vertices[i]) * invt;
                _vertices[i] = point;
            }
        }
    }
 
    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < _vertices.Length; i++)
        {
            if (i == 0 || i == 20) continue;
            //Initial Setup
            _velocity[i] = _velocity[i] * damping + gravity * t;
            _vertices[i] += _velocity[i] * t;
        }
 
        for (int l = 0; l < 16; l++)
            Strain_Limiting();
 
        Collision_Handling();

        _mesh.vertices = _vertices;
 
        _mesh.RecalculateNormals();
    }
}
