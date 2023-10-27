using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem;

using Mirror;
using Unity.VisualScripting;

public class PlayerController : CustomNetworkObject, IHoldable
{

  Vector2 _inputMovement, _inputMovementLast;

  Animator _playerModel;

  new void Start()
  {
    base.Start();

    //
    _playerModel = transform.GetChild(1).GetComponent<Animator>();
    _playerModel.transform.parent = transform.parent;
  }

  // Clean up
  void OnDestroy()
  {

    // Destroy model
    GameObject.Destroy(_playerModel.gameObject);
  }

  // Gather / Handle input
  void Update()
  {

    if (isLocalPlayer)
    {

      // Input vars
      var inputMovement = Vector2.zero;
      var inputInteract = false;

      // Controller input
#if UNITY_EDITOR
      var gamepad = Gamepad.current;
      if (gamepad != null)
      {

        // Movement
        inputMovement = gamepad.leftStick.ReadValue();

        // Direction
        //var input2 = gamepad.rightStick.ReadValue();

        // Interact
        inputInteract = gamepad.buttonSouth.wasPressedThisFrame;
      }

#else

      // Keyboard input
      var keyboard = Keyboard.current;
      if (keyboard != null)
      {

        // Movement
        if (keyboard.leftArrowKey.isPressed)
          inputMovement.x += -1f;
        if (keyboard.rightArrowKey.isPressed)
          inputMovement.x += 1f;
        if (keyboard.upArrowKey.isPressed)
          inputMovement.y += 1f;
        if (keyboard.downArrowKey.isPressed)
          inputMovement.y += -1f;

        if (inputMovement.magnitude > 1f)
          inputMovement = inputMovement.normalized;

        // Interact
        inputInteract = keyboard.spaceKey.wasPressedThisFrame;
      }
#endif

      // Handle input
      if (_inputMovement != inputMovement)
      {
        _inputMovement = inputMovement;
        CmdSetInput(_inputMovement);
      }

      if (inputInteract)
        CmdInteract();

    }

    // Handle model
    var modelDistance = (transform.position - _playerModel.transform.position) * Time.deltaTime * 11f;
    var d = modelDistance.magnitude;
    if (d < 0.02f)
      d = 0f;
    d *= 15f;
    _anim = Mathf.Clamp(_anim + (d - _anim) * Time.deltaTime * 5f, 0f, 1f);
    _playerModel.SetFloat("MovementSpeed", _anim);
    _playerModel.transform.position += modelDistance;
    _playerModel.transform.rotation = Quaternion.Lerp(_playerModel.transform.rotation, _Rb.rotation, Time.deltaTime * 3f);
  }
  float _anim;

  void FixedUpdate()
  {

    if (isServer)
    {
      // Add force to player
      _Rb.AddForce(new Vector3(1f * _inputMovement.x, 0f, 1f * _inputMovement.y) * 17f);

      // Rotate
      if (_inputMovementLast != Vector2.zero)
        _Rb.MoveRotation(Quaternion.LookRotation(new Vector3(_inputMovementLast.x, 0f, _inputMovementLast.y)));

      // Check hold
      if (_holdData._IsHolding)
        _holdData._Holdee._Rb.MovePosition(_Rb.position + new Vector3(0f, 2.5f, 0f));
    }
  }

  // Set player input on server
  [Command]
  void CmdSetInput(Vector2 input)
  {
    if (input != Vector2.zero)
      _inputMovementLast = _inputMovement;
    _inputMovement = input;
  }

  [Command]
  void CmdInteract()
  {

    CustomNetworkObject interactOther = null;

    // Raycast to object
    gameObject.layer = 2;

    var raycastInfo = new RaycastHit();
    if (Physics.SphereCast(new Ray(_Rb.position, transform.forward * 5f), 0.2f, out raycastInfo, 1f))
    {

      var other = raycastInfo.collider.gameObject;
      var networkObject = other.transform.parent.GetComponent<CustomNetworkObject>();
      if (networkObject != null && networkObject is IPickupable)
      {
        interactOther = networkObject;
      }

    }

    gameObject.layer = 0;

    // Try interact
    ContextualInteract(interactOther);
  }

  // Hold info
  IHoldable _holdData { get { return this; } }
  CustomNetworkObject IHoldable._Holdee { get; set; }
  bool IHoldable._IsHolding { get { return _holdData._Holdee != null; } }
  void IHoldable.Hold(CustomNetworkObject other)
  {
    _holdData._Holdee = other;

    other._Rb.isKinematic = true;
    other.ToggleCollider(false);
  }

  void IHoldable.Throw()
  {
    _holdData._Holdee._Rb.isKinematic = false;
    _holdData._Holdee.ToggleCollider(true);
    _holdData._Holdee._Rb.AddForce(transform.forward * 250f);

    _holdData._Holdee = null;
  }

  void ContextualInteract(CustomNetworkObject other)
  {

    // Throw
    if (_holdData._IsHolding)
    {
      _holdData.Throw();
      return;
    }

    // Grab
    if (other != null)
      _holdData.Hold(other);
  }

}
