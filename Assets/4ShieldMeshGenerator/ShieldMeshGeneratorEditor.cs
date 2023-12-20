using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShieldMeshGenerator))]
public class ShieldMeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ShieldMeshGenerator shieldMeshGenerator = (ShieldMeshGenerator)target;
        if(GUILayout.Button("Generate Shield Mesh"))
        {
            shieldMeshGenerator.GenerateShieldMesh();
        }
    }
}
