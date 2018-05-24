using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AssetToolEditor : EditorWindow {
    private GameObject _selected;
    private float _angle = 45.0f;
    private float _ignoreFactor = 0.5f;
    private int _meshCount;
    private bool _generateLightmapUVs = true;
    private bool _keepMatIDs;
    private bool _setStatic = true;
    private bool _recombineMeshes = true;
    private bool _keepOriginal = true;
    private bool _saveMeshes;
    private bool _merge = true;
    private string _savePath = "Assets/Meshes/";
    private List<GameObject> _selection;
    private List<MeshFilter> _meshFilters;
    private bool _recalculateNormals;

    [MenuItem("Window/AssetTool")]
    private static void Create() {
        AssetToolEditor window = (AssetToolEditor)GetWindow(typeof(AssetToolEditor));
    }

    private void OnGUI() {
        EditorGUILayout.LabelField("Parameters");

        EditorGUILayout.BeginVertical();

        _recalculateNormals = EditorGUILayout.Toggle("Recalculate Normals (Area weighted): ", _recalculateNormals);
        if (_recalculateNormals)
        {
            _angle = EditorGUILayout.FloatField("Smooth Face Angle: ", _angle);
            _ignoreFactor = EditorGUILayout.FloatField("Ignore Factor: ", _ignoreFactor);
        }

        _generateLightmapUVs = EditorGUILayout.Toggle("Generate Lightmap UVs: ", _generateLightmapUVs);
        //_keepMatIDs = EditorGUILayout.Toggle("Keep IDs: ", _keepMatIDs);
        _setStatic = EditorGUILayout.Toggle("Set Static: ", _setStatic);
        _recombineMeshes = EditorGUILayout.Toggle("Recombine Meshes: ", _recombineMeshes);
        _keepOriginal = EditorGUILayout.Toggle("Keep Original Mesh: ", _keepOriginal);
        _saveMeshes = EditorGUILayout.Toggle("Save Meshes: ", _saveMeshes);
        _savePath = EditorGUILayout.TextField("Save path: ", _savePath);
        _meshCount = Selection.GetFiltered<MeshFilter>(SelectionMode.Deep).Length;
        EditorGUILayout.LabelField("Selected Meshes: ", _meshCount.ToString());
        EditorGUILayout.LabelField("Selected Materials: ", GetSelectedMaterials().Count.ToString());
        EditorGUILayout.LabelField("Submesh Count: ", GetSubmeshCount().ToString());


        if (GUILayout.Button("Process Selected")) {
            _selection = new List<GameObject>();
            foreach (GameObject go in Selection.gameObjects) {
                _selected = go;
                Apply(go.transform);
            }

            DestroySelection();
            //_selected = Selection.gameObjects[0];
            //Apply(_selected.transform);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical();

        _merge = EditorGUILayout.Toggle("Combine To Submeshes: ", _merge);

        if (GUILayout.Button("Combine Selected")) {
            _selection = new List<GameObject>();
            CombineSelected();
            DestroySelection();
        }

        EditorGUILayout.EndVertical();
    }

    private void DestroySelection() {
        if (!_keepOriginal)
        {
            while (Selection.gameObjects.Length != 0)
            {
                DestroyImmediate(Selection.gameObjects[0].gameObject);
            }
        }
        if (_selection.Count > 0)
        {
            Selection.objects = _selection.ToArray();
        }
    }

    private void Apply(Transform t) {
        MeshFilter filter = t.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null) {
            _selected = t.gameObject;
            ModifyMesh(filter.sharedMesh);
        }

        // Recurse
        foreach (Transform child in t)
            Apply(child);
    }

    private void ModifyMesh(Mesh mesh) {
        GameObject parentObj = new GameObject(_selected.name);
        //parentObj.transform.position = Vector3.zero;
        parentObj.transform.position = _selected.transform.position;
        parentObj.transform.localScale = _selected.transform.localScale;

        /*
        _triangleList = new List<int[]>();

        for(int i=0; i < mesh.subMeshCount; i++) {
            _bufferedTriangles = mesh.GetTriangles(i);
            _triangleList.Add(_bufferedTriangles);
        }        

        mesh.SetTriangles(mesh.triangles, 0);
        NormalSolver.RecalculateNormals(mesh, _angle, _ignoreFactor);
        Unwrapping.GenerateSecondaryUVSet(mesh);
        Debug.Log("Asset processed: " + mesh.name);
        Debug.Log("Submeshes: " + mesh.subMeshCount);

        if (_keepMatIDs) {
            for (int i = 0; i < _triangleList.Count - 1; i++) {
                mesh.SetTriangles(_triangleList[i], i);
            }
        }

        /*Vector3[] newVertices = new Vector3[_triangleList[0].Length];
        for (int i= 0; i< _triangleList[0].Length; i++) {
            newVertices[i] = mesh.vertices[i];
        } */


        for (int i = 0; i < mesh.subMeshCount; i++) {
            Mesh newMesh = new Mesh {
                vertices = mesh.vertices,
                triangles = mesh.GetTriangles(i),
                uv = mesh.uv
            };


            if (_recalculateNormals) newMesh.RecalculateNormals(_angle, _ignoreFactor);
            //_selected.GetComponent<MeshFilter>().mesh = newMesh;

            GameObject newObj = new GameObject(_selected.GetComponent<MeshRenderer>().sharedMaterials[i].name);

            if(_generateLightmapUVs) Unwrapping.GenerateSecondaryUVSet(newMesh);

            newObj.AddComponent<MeshFilter>().sharedMesh = newMesh;
            newMesh.RecalculateBounds();
            newMesh.RecalculateTangents();

            if (_recombineMeshes) {
                newObj.transform.position = Vector3.zero;
            } else {
                newObj.transform.position = _selected.transform.position;
                SavePrefab(newObj, newObj.name);
            }

            newObj.transform.localScale = _selected.transform.localScale;
            newObj.transform.rotation = _selected.transform.rotation;

            newObj.AddComponent<MeshRenderer>().material = _selected.GetComponent<MeshRenderer>().sharedMaterials[i];
            newObj.isStatic = _setStatic;
            newObj.transform.parent = parentObj.transform;
        }

        parentObj.isStatic = _setStatic;

        if (_recombineMeshes) {
            //CombineMeshes(combinelist);

            parentObj.AddComponent<MeshFilter>();
            parentObj.AddComponent<MeshRenderer>();

            //if (parentObj.GetComponentsInChildren<MeshFilter>().Length == 0)
            //    return;

            MeshFilter[] meshFilters = parentObj.GetComponentsInChildren<MeshFilter>();

            CombineInstance[] combine = new CombineInstance[meshFilters.Length - 1];

            int index = 0;
            for (int j = 1; j < meshFilters.Length; j++) {
                combine[index].mesh = meshFilters[j].sharedMesh;
                combine[index].transform = meshFilters[j].transform.localToWorldMatrix;
                index++;
            }

            parentObj.transform.GetComponent<MeshFilter>().sharedMesh = new Mesh();
            parentObj.GetComponent<MeshFilter>().sharedMesh.CombineMeshes(combine, false);
            parentObj.GetComponent<MeshRenderer>().sharedMaterials =
                _selected.GetComponent<MeshRenderer>().sharedMaterials;

            while (parentObj.transform.childCount != 0) DestroyImmediate(parentObj.transform.GetChild(0).gameObject);

            parentObj.SetActive(true);
            parentObj.GetComponent<MeshFilter>().sharedMesh.RecalculateBounds();
            parentObj.GetComponent<MeshFilter>().sharedMesh.RecalculateTangents();
            _selection.Add(parentObj);

            SavePrefab(parentObj, parentObj.name);
        }
    }

    private void SaveMesh(Mesh mesh, string name) {
        if (!_saveMeshes) return;

        AssetDatabase.CreateAsset(mesh, _savePath + name + ".asset");
        AssetDatabase.SaveAssets();
    }

    private void SavePrefab(GameObject obj, string name) {
        if (!_saveMeshes) return;

        SaveMesh(obj.GetComponent<MeshFilter>().sharedMesh, name);
        PrefabUtility.CreatePrefab(_savePath + name + ".prefab", obj);
        AssetDatabase.SaveAssets();
    }

    public static void SelectParents() {
        GameObject[] objs = Selection.gameObjects;
        List<GameObject> parents = new List<GameObject>();
        foreach (GameObject obj in objs) {
            if (obj.transform.parent != null) {
                parents.Add(obj.transform.parent.gameObject);
            }
        }

        Selection.objects = parents.ToArray();
    }

    public static List<Material> GetSelectedMaterials() {
        List<Material> materials = new List<Material>();

        MeshRenderer[] renderer = Selection.GetFiltered<MeshRenderer>(SelectionMode.Deep);

        foreach (MeshRenderer r in renderer)
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (!materials.Contains(m)) materials.Add(m);
            }
        }

        return materials;
    }

    public static int GetSubmeshCount() {
        MeshFilter[] filters = Selection.GetFiltered<MeshFilter>(SelectionMode.Deep);
        int submeshCount = 0;

        foreach (MeshFilter f in filters)
        {
            if (f.sharedMesh.subMeshCount > 0)
            {
                submeshCount += f.sharedMesh.subMeshCount;
            }
        }

        return submeshCount;
    }

    private class MeshData {
        public readonly Mesh mesh;
        public readonly Material material;
        private int materialID;
        public readonly Transform transform;

        public MeshData(Mesh me, Material mat, Transform t) {
            mesh = me;
            material = mat;
            transform = t;
        }
    }

    private void CombineSelected() {
        List<MeshData> meshData = new List<MeshData>();
        _selection.Clear();

        // Store data of selection in MeshData Class list
        foreach (Transform t in Selection.GetTransforms(SelectionMode.Deep))
        {
            if (!t.GetComponent<MeshFilter>())
            {
                continue;
            }

            Mesh baseMesh = t.GetComponent<MeshFilter>().sharedMesh;
            for (int i = 0; i < baseMesh.subMeshCount; i++)
            {
                Mesh sMesh = new Mesh
                {
                    vertices = baseMesh.vertices,
                    triangles = baseMesh.GetTriangles(i),
                    uv = baseMesh.uv,
                    normals = baseMesh.normals
                };

                Material mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");

                if (t.GetComponent<MeshRenderer>()) mat = t.GetComponent<MeshRenderer>().sharedMaterials[i];

                MeshData mData = new MeshData(sMesh, mat, t.GetComponent<MeshFilter>().transform);
                meshData.Add(mData);
            }
        }

        // Build Mesh and Material list
        List<Material> materials = new List<Material>();
        List<List<Mesh>> meshes = new List<List<Mesh>>();
        List<List<Transform>> transforms = new List<List<Transform>>();

        foreach (MeshData data in meshData)
            if (!materials.Contains(data.material)) {
                materials.Add(data.material);

                List<Mesh> newMeshList = new List<Mesh>();
                newMeshList.Add(data.mesh);
                meshes.Add(newMeshList);

                List<Transform> newTransformList = new List<Transform>();
                newTransformList.Add(data.transform);
                transforms.Add(newTransformList);
            } else {
                int index = materials.IndexOf(data.material);
                meshes[index].Add(data.mesh);
                transforms[index].Add(data.transform);
            }

        GameObject combinedObj = new GameObject(Selection.transforms[0].name + "_combined");
        if (!_merge)
        {
            combinedObj.transform.position = Selection.transforms[0].position;
        }

        // Combine meshes with same material
        for (int i = 0; i < meshes.Count; i++) {
            List<Mesh> meshList = meshes[i];
            CombineInstance[] combine = new CombineInstance[meshList.Count];

            GameObject combinedMesh = new GameObject(materials[i].name);

            combinedMesh.AddComponent<MeshFilter>();
            combinedMesh.AddComponent<MeshRenderer>();

            for (int j = 0; j < meshList.Count; j++) {
                combine[j].mesh = meshList[j];
                combine[j].transform = transforms[i][j].localToWorldMatrix;
            }

            Mesh mesh = new Mesh();
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            if(_recalculateNormals)
            {
                mesh.RecalculateNormals(_angle, _ignoreFactor);
            }

            if(_generateLightmapUVs) Unwrapping.GenerateSecondaryUVSet(mesh);

            combinedMesh.GetComponent<MeshFilter>().sharedMesh = mesh;
            combinedMesh.GetComponent<MeshRenderer>().sharedMaterial = materials[i];

            combinedMesh.transform.parent = combinedObj.transform;
            combinedMesh.isStatic = _setStatic;

            _selection.Add(combinedMesh);
        }


        if (_merge) {
            MeshFilter[] filters = combinedObj.GetComponentsInChildren<MeshFilter>();
            CombineInstance[] combine = new CombineInstance[filters.Length];
            Material[] uniqueMaterials = new Material[filters.Length];

            for (int i = 0; i < filters.Length; i++) {
                combine[i].mesh = filters[i].sharedMesh;
                combine[i].transform = filters[i].transform.localToWorldMatrix;
                uniqueMaterials[i] = filters[i].transform.GetComponent<MeshRenderer>().sharedMaterial;
            }

            combinedObj.AddComponent<MeshFilter>();
            combinedObj.AddComponent<MeshRenderer>();

            Mesh newMesh = new Mesh();
            newMesh.CombineMeshes(combine, false);
            //newMesh.CombineMeshes(combine, true, true);
            newMesh.RecalculateBounds();
            newMesh.RecalculateTangents();
            Unwrapping.GenerateSecondaryUVSet(newMesh);

            combinedObj.GetComponent<MeshFilter>().sharedMesh = newMesh;
            combinedObj.GetComponent<MeshRenderer>().sharedMaterials = uniqueMaterials;
            combinedObj.isStatic = _setStatic;

            while (combinedObj.transform.childCount != 0)
            {
                DestroyImmediate(combinedObj.transform.GetChild(0).gameObject);
            }

            _selection.Add(combinedObj);
        }

        SavePrefab(combinedObj, combinedObj.name);
    }
}