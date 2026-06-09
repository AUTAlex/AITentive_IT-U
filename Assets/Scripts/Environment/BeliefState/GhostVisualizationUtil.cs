using UnityEngine;

public static class GhostVisualizationUtil
{
    public static GameObject InstantiateBeliefStateVisualization(GameObject original, Material ghostMaterial, Transform parent)
    {
        GameObject ghost = new GameObject(original.name + "_Ghost");
        ghost.transform.SetParent(parent.transform, false);

        ghost.transform.SetPositionAndRotation(
            original.transform.position,
            original.transform.rotation
        );

        ghost.transform.localScale = original.transform.localScale;

        CopyVisualsOnly(original.transform, ghost.transform, ghostMaterial);

        return ghost;
    }


    private static void CopyVisualsOnly(Transform source, Transform target, Material material)
    {
        CopyMeshRenderer(source, target, material);

        foreach (Transform sourceChild in source)
        {
            GameObject targetChild = new GameObject(sourceChild.name);

            targetChild.transform.SetParent(target, false);
            targetChild.transform.localPosition = sourceChild.localPosition;
            targetChild.transform.localRotation = sourceChild.localRotation;
            targetChild.transform.localScale = sourceChild.localScale;

            CopyVisualsOnly(sourceChild, targetChild.transform, material);
        }
    }

    private static void CopyMeshRenderer(Transform source, Transform target, Material material)
    {
        MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();
        MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();

        if (sourceMeshFilter == null || sourceMeshRenderer == null)
        {
            return;
        }

        MeshFilter targetMeshFilter = target.gameObject.AddComponent<MeshFilter>();
        targetMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

        MeshRenderer targetMeshRenderer = target.gameObject.AddComponent<MeshRenderer>();
        targetMeshRenderer.sharedMaterials = CreateMaterialArray(
            sourceMeshRenderer.sharedMaterials.Length,
            material
        );

        targetMeshRenderer.enabled = sourceMeshRenderer.enabled;
    }

    private static Material[] CreateMaterialArray(int length, Material material)
    {
        Material[] materials = new Material[length];

        for (int i = 0; i < length; i++)
        {
            materials[i] = material;
        }

        return materials;
    }
}
