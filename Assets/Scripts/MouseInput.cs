// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using UnityEngine;

public sealed class MouseInput : MonoBehaviour
{
    private void Start()
    {
        if (InputManager.Instance == null)
        {
            Debug.LogError("No InputManager available. Disabling");
            enabled = false;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            InputManager.Instance.TriggerTapPress();
        }
        else if (Input.GetMouseButtonUp(1))
        {
            InputManager.Instance.TriggerTapRelease();
        }
    }
}
