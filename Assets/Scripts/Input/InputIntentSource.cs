using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputIntentSource : MonoBehaviour
{
    public float lookSensitivity = 1.0f;

    public InputIntent Current { get; private set; }

    void Update()
    {
        var intent = new InputIntent();

        intent.Move = new Vector2(
            (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
            (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f)
        );
        
        intent.Look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * lookSensitivity;

        intent.ActionDown = Input.GetMouseButtonDown(1);
        intent.ActionHeld = Input.GetMouseButton(1);

        intent.SelectDown = Input.GetMouseButtonDown(0);
        intent.SelectHeld = Input.GetMouseButton(0);

        intent.PointerScreenPos = Input.mousePosition;

        Current = intent;
    }
}
