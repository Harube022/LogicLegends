using UnityEngine;
using System.Collections;

public class DissolveController : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;

    private Material material;

    private void Awake()
    {
        material = targetRenderer.material; // creates its own material instance
    }

    public void StartDissolve()
    {
        StartCoroutine(Dissolve());
    }

    IEnumerator Dissolve()
    {
        float value = 0;

        while (value < 1)
        {
            value += Time.deltaTime;
            material.SetFloat("_DissolveStrength", value);
            yield return null;
        }

        Destroy(gameObject);
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartDissolve();
        }
    }
}