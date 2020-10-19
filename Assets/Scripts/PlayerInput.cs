using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerInput : MonoBehaviour
{
    //[SerializeField]
    //float speed = 0.1f;

    public bool isDropped;

    void Start()
    {

    }
    void Update()
    {
        if (Input.GetKey(KeyCode.UpArrow))
        {
            transform.Translate(Vector3.forward / 36);
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            transform.Translate(-Vector3.forward / 36);
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Translate(Vector3.left / 36);
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            transform.Translate(-Vector3.left / 36);
        }
    }

}