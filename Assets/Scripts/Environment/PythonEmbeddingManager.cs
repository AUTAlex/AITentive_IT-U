using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class PythonEmbeddingManager : MonoBehaviour
{
    public const int EmbeddingSize = 384;

    private static dynamic _embedder;
    private static bool _initialized = false;

    private Dictionary<string, float[]> _embeddingCache = new();


    [DllImport("kernel32", SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);


    public float[] GetEmbedding(string word)
    {
        if (_embeddingCache.TryGetValue(word, out var cached))
        {
            return cached;
        }

        using (Py.GIL())
        {
            dynamic result = _embedder.get_embedding(word);  // Python list
            PyObject pyList = (PyObject)result;

            int len = (int)pyList.Length();
            float[] embedding = new float[len];

            for (int i = 0; i < len; i++)
            {
                embedding[i] = (float)pyList[i].As<double>();
            }

            _embeddingCache[word] = embedding;

            return embedding;
        }
    }

    public float[][] GetEmbeddings(string[] words)
    {
        string[] toCompute = words.Distinct().Where(w => !_embeddingCache.ContainsKey(w)).ToArray();

        if (toCompute.Length > 0)
        {
            using (Py.GIL())
            {
                using (PyList pyList = new PyList())
                {
                    foreach (string word in toCompute)
                        pyList.Append(new PyString(word));

                    dynamic result = _embedder.get_embeddings(pyList);
                    PyObject pyResult = (PyObject)result;

                    for (int i = 0; i < pyResult.Length(); i++)
                    {
                        float[] emb = new float[pyResult[i].Length()];
                        for (int j = 0; j < emb.Length; j++)
                            emb[j] = (float)pyResult[i][j].As<double>();

                        _embeddingCache[toCompute[i]] = emb;
                    }
                }
            }
        }

        // Return array of float[] for all input words
        return words.Select(w => _embeddingCache[w]).ToArray();
    }


    private void OnApplicationQuit()
    {
        if (_initialized)
        {
            Debug.Log("Shutting down Python engine...");
            PythonEngine.Shutdown();
            Debug.Log("Python engine shutdown complete.");
        }
    }

    private void Start()
    {
        if (_initialized) return;

        // Paths setup
        string envZipPath = Path.Combine(Application.streamingAssetsPath, "PythonEnv/unity_embed_env.zip");
        string unpackPath = Path.Combine(Application.persistentDataPath, "py_env");

        // Extract environment if needed
        if (!Directory.Exists(unpackPath))
        {
            Debug.Log("Extracting Python environment...");
            try
            {
                ZipFile.ExtractToDirectory(envZipPath, unpackPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to extract Python environment: {e.Message}");
                return;
            }
        }

        // Python environment variables
        string dll = Path.Combine(unpackPath, "python39.dll");
        string lib = Path.Combine(unpackPath, "Lib");
        string sitePackages = Path.Combine(lib, "site-packages");
        string dllDir = Path.Combine(unpackPath, "Library", "bin");

        Environment.SetEnvironmentVariable("PYTHONHOME", unpackPath);
        Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", dll);
        Environment.SetEnvironmentVariable("PYTHONPATH", $"{lib};{sitePackages}");

        SetDllDirectory(dllDir);

        PythonEngine.Initialize();

        // Python script loading from StreamingAssets
        string pyScriptSource = Path.Combine(Application.streamingAssetsPath, "PythonScripts", "embedding_model.py");
        string pyScriptTargetDir = Path.Combine(Application.persistentDataPath, "py_scripts");
        string pyScriptTarget = Path.Combine(pyScriptTargetDir, "embedding_model.py");

        if (!Directory.Exists(pyScriptTargetDir))
            Directory.CreateDirectory(pyScriptTargetDir);

        if (!File.Exists(pyScriptTarget))
        {
            try
            {
                File.Copy(pyScriptSource, pyScriptTarget);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to copy Python script: {e.Message}");
                return;
            }
        }

        using (Py.GIL())
        {
            dynamic sys = Py.Import("sys");
            sys.path.append(pyScriptTargetDir);
            _embedder = Py.Import("embedding_model");
        }

        _initialized = true;
        Debug.Log("Python environment and embedding model initialized successfully.");
    }

}
