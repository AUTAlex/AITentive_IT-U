using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

public class BallBehavior : MonoBehaviour
{
    // Start is called before the first frame update
    public Rigidbody2D MyRb { get; private set; }
    
    
    private void Start()
    {
        MyRb = this.transform.GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    private float resTime = 0.0f;
    void FixedUpdate()
    {
        resTime += Time.deltaTime;
        if (resTime > 3)
        {
            MyRb.angularDamping /= 2;
            resTime = 0.0f;
        }
        if(MyRb.linearVelocity.magnitude<0.2f)
        {
            MyRb.AddForce(new Vector2(Random.Range(-100f, 100f), 0f));
        }        
    }
}
