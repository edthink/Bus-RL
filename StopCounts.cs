using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StopCounts : MonoBehaviour
{
    public int stopId;
	public int capacity;
	public int waiting;
	public float pAdd;


    // Start is called before the first frame update
    void Start()
    {
    	// initiate number of waiting passengers
        waiting = UnityEngine.Random.Range(0, 8);
    }

    // Update is called once per frame
    void Update()
    {
    	if (UnityEngine.Random.value < pAdd && waiting < capacity) {
            // print ("Passenger arrived at stop " + stopId );
    		waiting += UnityEngine.Random.Range(0, 4);  
    	} 
    }
}
