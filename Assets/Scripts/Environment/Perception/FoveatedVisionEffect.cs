using UnityEngine;

public class FoveatedVisionEffect : MonoBehaviour
{
    [field: SerializeField]
    public Vector2 GazeUV { get; set; }

    [field: SerializeField]
    public float FovealRadius { get; set; }

    [field: SerializeField]
    public Shader FoveatedShader { get; set; }


    private Material _foveatedMaterial;


    public void Apply(RenderTexture source, RenderTexture destination)
    {
        _foveatedMaterial = _foveatedMaterial != null ? _foveatedMaterial : new Material(FoveatedShader);

        _foveatedMaterial.SetVector("_Gaze", GazeUV);
        _foveatedMaterial.SetFloat("_Radius", FovealRadius);
        _foveatedMaterial.SetFloat("_BlurSize", 1.0f); // tune as needed

        Graphics.Blit(source, destination, _foveatedMaterial);
    }

}
