using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utils;
using Random = UnityEngine.Random;

public class OverheadSign : MonoBehaviour {
    public OverheadSignSingle[] signList = { };

    public Lane Lane { get; private set; } = Lane.Center;

    public event Action<Lane> TargetLaneChanged;


    private Road _road;

    
    private void Awake() 
    {
        _road = transform.parent.GetComponent<Road>();
    }
    
    
    public List<SignState> GetSignStates() 
    {
        return signList.Select(overheadSignSingle => overheadSignSingle.signState).ToList();
    }

    /*
     * From left to right 0 - X
     */
    private Lane GetActiveLine() 
    {
        int index = signList.ToList().FindIndex(e => e.signState == SignState.Free);
        if (Enum.IsDefined(typeof(Lane), index)) {
            return (Lane)index;
        }

        Debug.LogError("FAILED TO GET ACTIVE LANE CORRECTLY");
        return Lane.Center;
    }

    public Lane AssignRandomSign(Lane previousLane) 
    {
        Lane freeLane = EnumHelper.GetRandomEnumValue<Lane>();

        while (freeLane == previousLane) {
            freeLane = EnumHelper.GetRandomEnumValue<Lane>();
        }
        
        switch (freeLane) {
            case Lane.Left:
                signList[0].SetSignState(SignState.Free);
                signList[1].SetSignState(SignState.Blocked);
                signList[2].SetSignState(SignState.Blocked);
                Lane = Lane.Left;
                break;
            case Lane.Center:
                signList[0].SetSignState(SignState.Blocked);
                signList[1].SetSignState(SignState.Free);
                signList[2].SetSignState(SignState.Blocked);
                Lane = Lane.Center;
                break;
            case Lane.Right:
                signList[0].SetSignState(SignState.Blocked);
                signList[1].SetSignState(SignState.Blocked);
                signList[2].SetSignState(SignState.Free);
                Lane = Lane.Right;
                break;
        }
        
        return Lane;
        
    }

    public Lane SetSigns(SignState[] newSignStates) 
    {
        if (signList.Length < newSignStates.Length) {
            return 0;
        }

        for (int i = 0; i < signList.Length; i++) {
            signList[i].SetSignState(newSignStates[i]);
        }

        Lane = GetActiveLine();
        
        return Lane;
        
    }


    private void OnTriggerEnter(Collider other) 
    {
        // Debug.Log("LANE CHANGE");
        if (other.CompareTag("PlayerCar")) {
            TargetLaneChanged?.Invoke(Lane);

            _road.LoggedOptimalLaneChangeEnd = false;            
        }
    }
}