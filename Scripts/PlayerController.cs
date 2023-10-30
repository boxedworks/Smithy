using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem;

using Mirror;

public class PlayerController : CustomNetworkObject, ICanHold
{

  public static PlayerController _LocalPlayer;
  static System.Action _onLocalPlayerConnected;
  public static void OnLocalPlayerConnected(System.Action onConnected)
  {
    if (_LocalPlayer == null)
      _onLocalPlayerConnected += onConnected;
    else
      onConnected?.Invoke();
  }

  Vector2 _inputMovement, _inputMovementLast;
  Animator _playerModel;

  void Start()
  {

    //
    Init(ObjectType.ENTITY_PLAYER);

    //
    _playerModel = transform.GetChild(1).GetComponent<Animator>();
    _playerModel.transform.parent = transform.parent;

    //
    _inputMovementLast = new Vector2(0f, 1f);

    //
    if (isServer)
    {

      //
      _OnNetworkCollision += (CustomNetworkObject networkObject) =>
      {
        Debug.Log($"Collided with network object: {networkObject.gameObject.name}");

        if (networkObject is IPickupable && networkObject._IsJustThrown)
          ContextualInteract(networkObject, false);
      };
    }

    // Register local player
    if (isLocalPlayer)
    {
      _LocalPlayer = this;

      _onLocalPlayerConnected?.Invoke();
      _onLocalPlayerConnected = null;
    }
  }

  // Clean up
  bool _destroyed;
  void OnDestroy()
  {

    // Drop on disconnect
    if (_holdData._IsHolding)
    {
      _holdData.Drop();
    }

    // Destroy model
    GameObject.Destroy(_playerModel?.gameObject);
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
        if (inputMovement.magnitude < 0.4f)
          inputMovement = Vector2.zero;

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
        if (inputMovement != Vector2.zero)
          _inputMovementLast = inputMovement;
        _inputMovement = inputMovement;

        CmdSetInput(_inputMovement);
      }

      if (inputInteract)
        CmdInteract();

    }

    // Handle model
    var modelDistance = (new Vector3(transform.position.x, 0f, transform.position.z) - new Vector3(_playerModel.transform.position.x, 0f, _playerModel.transform.position.z)) * Time.deltaTime * 11f;
    _animDistance = Mathf.Clamp(_animDistance + modelDistance.magnitude * 1.3f - Time.deltaTime * 2f, 0f, 1f);
    _playerModel.SetFloat("MovementSpeed", _animDistance);
    _playerModel.transform.position += modelDistance;
    if (isLocalPlayer)
      _playerModel.transform.rotation = Quaternion.Lerp(_playerModel.transform.rotation, Quaternion.LookRotation(new Vector3(_inputMovementLast.x, 0f, _inputMovementLast.y)), Time.deltaTime * 3f);
    else
      _playerModel.transform.rotation = Quaternion.Lerp(_playerModel.transform.rotation, _Rb.rotation, Time.deltaTime * 3f);

    // Sfx
    _footstepDistance += _animDistance * 0.04f;
    if (_footstepDistance > 1f)
    {
      _footstepDistance -= 1f;

      SfxManager.PlayAudioSourceSimple(
        transform.position,
        GameController.s_Singleton._SfxProfiles[0]._audioClips[0],
        0.6f,
        0.87f, 1.13f,
        SfxManager.AudioPriority.NORMAL
      );
    }
  }
  float _animDistance, _footstepDistance;

  void FixedUpdate()
  {

    //
    if (!isServer)
      return;

    // Add force to player
    _Rb.AddForce(new Vector3(1f * _inputMovement.x, 0f, 1f * _inputMovement.y) * 17f);

    // Rotate
    if (_inputMovementLast != Vector2.zero)
      _Rb.MoveRotation(Quaternion.LookRotation(new Vector3(_inputMovementLast.x, 0f, _inputMovementLast.y)));

    // Check hold
    if (_holdData._IsHolding)
      _holdData._Holdee._Rb.MovePosition(_Rb.position + new Vector3(0f, 2.5f, 0f));

  }

  // Set player input on server
  [Command]
  void CmdSetInput(Vector2 input)
  {
    if (input != Vector2.zero)
      _inputMovementLast = input;
    _inputMovement = input;
  }

  //
  [Command]
  void CmdInteract()
  {

    CustomNetworkObject interactOther = null;

    // Raycast to object
    gameObject.layer = 2;

    var raycastInfo = new RaycastHit();
    if (Physics.SphereCast(new Ray(_Rb.position + new Vector3(0f, 0.4f, 0f) + transform.forward * 0.1f, transform.forward * 5f), 0.2f, out raycastInfo, 2f))
      if (GetNetworkObjectFrom(raycastInfo.collider) == null)
        raycastInfo = new();
    //Debug.DrawRay(_Rb.position + new Vector3(0f, 1.5f, 0f) + transform.forward * 0.1f, transform.forward * 5f, Color.red, 3f);

    // Recast if no found
    if (raycastInfo.collider == null)
    {
      if (!Physics.SphereCast(new Ray(_Rb.position + new Vector3(0f, 1.5f, 0f) + transform.forward * 0.1f, transform.forward * 5f), 0.2f, out raycastInfo, 2f))
        raycastInfo = new();

      //Debug.DrawRay(_Rb.position + new Vector3(0f, 0.6f, 0f) + transform.forward * 0.1f, transform.forward * 5f, Color.blue, 3f);
    }

    // Handle
    if (raycastInfo.collider != null)
    {

      //Debug.Log($"{raycastInfo.collider.name} {raycastInfo.distance}");
      //Debug.DrawLine(_Rb.position, raycastInfo.collider.transform.position, Color.magenta, 3f);

      var networkObject = GetNetworkObjectFrom(raycastInfo.collider);
      if (networkObject != null)
      {
        interactOther = networkObject;
      }

    }

    gameObject.layer = 0;

    // Try interact
    ContextualInteract(interactOther);
  }

  // Hold info
  ICanHold _holdData { get { return this; } }
  CustomNetworkObject ICanHold._Holdee { get; set; }
  bool ICanHold._IsHolding { get { return _holdData._Holdee != null; } }
  void ICanHold.Hold(CustomNetworkObject other)
  {
    _holdData._Holdee = other;

    other._Rb.isKinematic = true;
    other.ToggleCollider(false);

    IgnoreCollisionsWith(other);
  }
  public void Hold(CustomNetworkObject other)
  {
    _holdData.Hold(other);
  }

  void ICanHold.Throw()
  {
    _holdData._Holdee._Rb.isKinematic = false;
    _holdData._Holdee.ToggleCollider(true);
    _holdData._Holdee._Rb.AddForce(transform.forward * 250f);

    var thrownComponent = _holdData._Holdee.gameObject.AddComponent<ThrownPickupable>();
    thrownComponent._Self = _holdData._Holdee;
    thrownComponent._Thrower = this;

    _holdData._Holdee = null;
  }

  void ICanHold.Drop()
  {
    _holdData._Holdee.ToggleCollider(true);
    _holdData._Holdee._Rb.isKinematic = false;

    _holdData._Holdee = null;
  }

  //
  [Server]
  public void SpawnAndHold(ObjectType objectType)
  {
    var newObject = SpawnNetworkObject(objectType, new Vector3(0f, 20f, 0f));
    newObject._OnInit += () =>
    {
      Hold(newObject);
    };
  }

  // Do something depending on what is in front of player
  void ContextualInteract(CustomNetworkObject other, bool tryThrow = true)
  {

    // Throw
    if (_holdData._IsHolding)
    {
      if (tryThrow)
        _holdData.Throw();
      return;
    }

    // Interact
    if (other != null)
    {

      // Hold
      if (other is IPickupable)
        Hold(other);

      // Forge
      else if (other is Forge)
      {
        (other as Forge).TryRetrieveObject(this);
      }

      // Unhandled tnteraction
      else
      {
        Debug.Log($"Unhandled interaction between {_ObjectType} and {other._ObjectType}");
      }
    }
  }

}
