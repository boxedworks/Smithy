using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem;

using Mirror;
using UnityEngine.VFX;
using Unity.VisualScripting;

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
    Init(ObjectType.ENTITY_PLAYER, -1, 0);

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
  new void OnDestroy()
  {

    // Drop on disconnect
    if (_holdData._IsHolding)
    {
      _holdData.Drop();
    }

    // Destroy model
    if (_playerModel != null)
      GameObject.Destroy(_playerModel.gameObject);

    //
    base.OnDestroy();
  }

  // Gather / Handle input
  void Update()
  {

    if (isLocalPlayer)
    {

      // Input vars
      var inputMovement = Vector2.zero;
      var inputInteract = false;

      //
      if (Input.GetKeyDown(KeyCode.Alpha1))
      {
        if (Input.GetKey(KeyCode.LeftShift))
          DimensionController.ToggleDimension(0, false, true);
        else
          DimensionController.ToggleDimension(0, true, !Input.GetKey(KeyCode.RightShift));
      }
      if (Input.GetKeyDown(KeyCode.Alpha2))
      {
        if (Input.GetKey(KeyCode.LeftShift))
          DimensionController.ToggleDimension(1, false, false);
        else
          DimensionController.ToggleDimension(1, true, !Input.GetKey(KeyCode.RightShift));
      }

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
        var input2 = gamepad.rightStick.ReadValue();
        if (input2.magnitude > 0.4f)
        {
          var input2_3 = new Vector3(input2.x, 0f, input2.y);
          if (DimensionController.s_dimensionLeftId > -1)
            DimensionController.IncrementDimensionOffset(DimensionController.s_dimensionLeftId, input2_3 * Time.deltaTime * 5f);
          if (DimensionController.s_dimensionRightId > -1)
            DimensionController.IncrementDimensionOffset(DimensionController.s_dimensionRightId, input2_3 * Time.deltaTime * 5f);
        }

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
    var targetPosition = transform.position;
    var modelDistance = (new Vector3(targetPosition.x, 0f, targetPosition.z) - new Vector3(_playerModel.transform.position.x, 0f, _playerModel.transform.position.z)) * Time.deltaTime * 11f;
    var modelDistanceMag = modelDistance.magnitude;

    _animDistance = Mathf.Clamp(_animDistance + modelDistanceMag * 1.3f - Time.deltaTime * 2f, 0f, 1f);
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

      PlayAudioSourceAt(
        SfxType.FOOTSTEPS,
        new SfxPlayData(transform.position)
        {
          Volume = 0.6f,
          PitchLower = 0.87f,
          PitchHigher = 1.13f
        });
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
      _holdData._Holdee._Rb.MovePosition(transform.position + new Vector3(0f, 2.5f, 0f));

    // Check dimension
    if (!_inDemension)
    {
      if (transform.position.z > 5.5f)
      {
        _inDemension = true;

        var dimensionLeft = transform.position.x < 0f;
        var dimensionId = dimensionLeft ? DimensionController.s_dimensionLeftId : DimensionController.s_dimensionRightId;
        if (dimensionId > -1)
          SetDimension(dimensionId);
      }
    }
    else
    {

      if (_dimensionIndex > -1)
      {
        var dimensionEntrance = DimensionController.GetDimension(_dimensionIndex).EntrancePosition;
        if (transform.position.z < dimensionEntrance.z)
        {
          _inDemension = false;

          SetDimension(-1);
        }
      }
    }

  }

  bool _inDemension;
  public override void SetDimension(int dimensionId)
  {

    // Rendering components
    var meshRenderer = _playerModel.transform.GetChild(1).GetComponent<SkinnedMeshRenderer>();
    var vfx0 = _playerModel.transform.GetChild(2).GetComponent<VisualEffect>();
    var vfx1 = _playerModel.transform.GetChild(3).GetComponent<VisualEffect>();

    //
    var savePos = transform.position;

    //
    var materials = meshRenderer.sharedMaterials;
    SetDimensionBase(dimensionId, ref materials, true);

    //
    _playerModel.transform.position += -(savePos - transform.position);

    //
    switch (dimensionId)
    {

      // Exit dimension
      case -1:

        vfx0.Play();
        vfx1.Stop();

        break;

      // Enter left / right dimension
      default:

        // Dimension data
        var dimensionData = DimensionController.GetDimension(dimensionId);
        var dimensionOrigin = dimensionData.Origin;
        var dimensionOffset = dimensionData.Offset;
        var dimensionLeft = dimensionData.DimensionLeft;

        //
        vfx1.SetBool("InDimensions", true);
        vfx1.SetBool("DimensionRight", !dimensionLeft);
        vfx1.SetVector3("Offset", new Vector3(dimensionOrigin.x, dimensionOrigin.y, dimensionOrigin.z));
        vfx1.SetVector3("InclusionOffset", dimensionOffset);

        vfx1.Play();
        vfx0.Stop();

        break;

    }

    // Check holding
    if (_holdData._IsHolding)
    {
      _holdData._Holdee._Rb.position = transform.position + new Vector3(0f, 2.5f, 0f);
      _holdData._Holdee.SetDimension(dimensionId);
    }
  }

  //
  public override void SetDimensionOffset(int dimension, Vector3 offset)
  {
    if (_dimensionIndex != dimension) return;

    var meshRenderer = _playerModel.transform.GetChild(1).GetComponent<SkinnedMeshRenderer>();
    var vfx1 = _playerModel.transform.GetChild(3).GetComponent<VisualEffect>();
    for (var i = 0; i < meshRenderer.sharedMaterials.Length; i++)
    {
      meshRenderer.sharedMaterials[i].SetVector("_InclusionOffset", offset);
    }
    vfx1.SetVector3("InclusionOffset", offset);

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

  //
  enum NetworkEvent
  {
    NONE,

    OBJECT_PICKUP,
    OBJECT_THROW
  }
  [ClientRpc]
  void RpcHandleNetworkEvent(NetworkEvent networkFlag)
  {

    switch (networkFlag)
    {

      case NetworkEvent.OBJECT_PICKUP:

        PlayAudioSourceAt(
          SfxType.OBJECT_PICKUP,
          new SfxPlayData(transform.position)
          {
            Volume = 0.7f,
          });

        break;

      case NetworkEvent.OBJECT_THROW:

        PlayAudioSourceAt(
          SfxType.OBJECT_THROW,
          new SfxPlayData(transform.position)
          {
            Volume = 1f,
          });

        break;

    }

  }

  // Sfx wrapper
  enum SfxType
  {
    FOOTSTEPS,
    OBJECT_PICKUP,
    OBJECT_THROW,
  }
  AudioSource PlayAudioSourceAt(SfxType sfxType, SfxPlayData sfxPlayData)
  {
    return PlayAudioSourceAt((int)sfxType, sfxPlayData);
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

    // Fx
    RpcHandleNetworkEvent(NetworkEvent.OBJECT_PICKUP);
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

    // Fx
    RpcHandleNetworkEvent(NetworkEvent.OBJECT_THROW);
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
      {
        Hold(other);
      }

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
