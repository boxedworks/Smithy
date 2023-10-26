using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem;

using Mirror;

public class PlayerController : NetworkBehaviour
{

  Rigidbody _rb;

  Vector2 _input;

  void Start()
  {
    _rb = GetComponent<Rigidbody>();
  }

  void Update()
  {



    // Handle input
    if (isLocalPlayer)
    {

      // Controller input
#if UNITY_EDITOR
      var gamepad = Gamepad.current;
      if (gamepad != null)
      {

        _input = gamepad.leftStick.ReadValue();
        CmdSetInput(_input);

        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
          CmdMoveBox();
        }
      }

      return;
#endif

      // Keyboard input
      var keyboard = Keyboard.current;
      if (keyboard != null)
      {

        var input = Vector2.zero;
        if (keyboard.leftArrowKey.isPressed)
          input.x += -1f;
        if (keyboard.rightArrowKey.isPressed)
          input.x += 1f;
        if (keyboard.upArrowKey.isPressed)
          input.y += 1f;
        if (keyboard.downArrowKey.isPressed)
          input.y += -1f;

        if (input.magnitude > 1f)
          input = input.normalized;

        _input = input;
        CmdSetInput(_input);

        if (keyboard.spaceKey.wasPressedThisFrame)
          CmdMoveBox();
      }
    }

  }




  void FixedUpdate()
  {

    if (isServer)
    {
      _rb.AddForce(new Vector3(1f * _input.x, 0f, 1f * _input.y) * 20f);
    }

  }

  [Command]
  void CmdSetInput(Vector2 input)
  {
    _input = input;
  }

  [Command]
  void CmdMoveBox()
  {
    GameObject.Find("Box").GetComponent<Rigidbody>().MovePosition(transform.position + transform.forward * 1.5f);
  }

}
