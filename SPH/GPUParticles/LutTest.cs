using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MyMathUntility;

[ExecuteInEditMode]
public class LutTest : MonoBehaviour
{
    // Start is called before the first frame update
    struct CellNode
    {
        public Vector3 position;
        public int clicked;
    }
    private HashCellLut lut;
    private Vector3 scale;
    private CellNode[] cells;
    void OnEnable()
    {
        scale = new Vector3(0.5f, 0.01f, 0.5f);
        int num = 400;
        lut = new HashCellLut(num);
        cells = new CellNode[num];

        lut.StartUpdateLut(0.5f);
        for(int i = 0; i < num; ++i)
        {
            cells[i] = new CellNode() { position = new Vector3(i / 20 * 0.5f, 0, i % 20 * 0.5f), clicked = 0 };
            lut.UpdateSpatialLookup(new Vector2(i/20*0.5f, i%20*0.5f), i);
        }
        lut.FinishUpdateLut();

        SceneView.duringSceneGui += OnSceneGUI;
        HandleUtility.AddDefaultControl(0);
    }
    void Update()
    {
        
    }

    void OnDrawGizmos()
    {
        for(int i = 0; i < cells.Length; ++i)
        {
            Gizmos.color = cells[i].clicked>0? Color.red : Color.green;
            Gizmos.DrawWireCube(cells[i].position + scale*0.5f, scale);
        }
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        
    }
    private int prevID = 0;
    private void OnSceneGUI(SceneView sceneView) 
    {
        if (Event.current.type == EventType.MouseDown)
        {
            Ray ray = Camera.current.ScreenPointToRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector2 coord = hit.textureCoord*20;
                int id = (int)coord.x*20+(int)coord.y;
                if(id != prevID) 
                {
                    prevID = id;
                    Debug.Log(coord +", " + id + ", " + hit.transform.name);
                    for(int i=0; i< cells.Length; ++i)
                    {
                        cells[i].clicked = 0;
                    }
                    cells[id].clicked = 1;
                    lut.GetPointsWithinRadius(coord*0.5f,
                        delegate (int index)
                        {
                            // if((new Vector2(cells[index].position.x, cells[index].position.z) - coord*0.5f).magnitude <= 1.5)
                                cells[index].clicked = 1;
                        }
                    );
                }
            }
        }
    }
}
