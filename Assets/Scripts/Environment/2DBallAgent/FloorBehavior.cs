using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorBehavior : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public event Action OnGameOver;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (OnGameOver != null)
        {
            OnGameOver();
        }
    }
}
