using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

using System.Linq;
using UnityEngine.VFX;
using Mirror;
using UnityEngine.Networking.Types;

public static class DimensionController
{

  //
  public struct Dimension
  {

    public int Id;
    public bool Visible { get { return s_dimensionLeftId == Id || s_dimensionRightId == Id; } }

    public Vector3
      Origin,
      Offset, OffsetDesired;
    public Vector2 BoundsX;

    public bool DimensionLeft;

    public Transform Transform;
    public Material[] Materials;
    public VisualEffect[] Effects;
    public Dictionary<int, CustomNetworkObject> NetworkObjects;

    //
    public Vector3 EntrancePosition { get { return new Vector3(Origin.x + 3f, 1.2f, Origin.z + 5.5f - 0.5f); } }

    //
  }

  // Available dimensions
  static Dictionary<int, Dimension> s_dimensions;
  static Dimension s_dimensionForest, s_dimensionMine, s_dimensionStoreroom;

  //
  public static void Init()
  {
    s_dimensions = new();

    //
    var dimensionMap = GameObject.Find("Forest").transform;
    s_dimensionForest = new Dimension()
    {
      Id = s_dimensions.Count,

      Origin = new Vector3(27f, 0f, -15f),
      Offset = Vector3.zero,

      Transform = dimensionMap,
      Materials = new Material[] {
        dimensionMap.GetChild(0).GetChild(0).GetComponent<MeshRenderer>().sharedMaterials[0],
        dimensionMap.GetChild(2).GetChild(0).GetComponent<MeshRenderer>().sharedMaterials[0]
      },
      NetworkObjects = new(),

      BoundsX = new Vector2(-9f, 0f)
    };
    s_dimensions.Add(s_dimensionForest.Id, s_dimensionForest);
    s_dimensionForest.SetOffset(s_dimensionForest.Offset);
    s_dimensionForest.Hide();

    //
    dimensionMap = GameObject.Find("Mine").transform;
    s_dimensionMine = new Dimension()
    {
      Id = s_dimensions.Count,

      Origin = new Vector3(27f, 0f, 0f),
      Offset = Vector3.zero,

      Transform = dimensionMap,
      Materials = new Material[] {
        dimensionMap.GetChild(0).GetChild(0).GetComponent<MeshRenderer>().sharedMaterials[0],
        dimensionMap.GetChild(0).GetChild(1).GetComponent<MeshRenderer>().sharedMaterials[0],
      },
      NetworkObjects = new(),

      BoundsX = new Vector2(-7f, 0f)
    };
    s_dimensions.Add(s_dimensionMine.Id, s_dimensionMine);
    s_dimensionMine.SetOffset(s_dimensionMine.Offset);
    s_dimensionMine.Hide();

    //
    dimensionMap = GameObject.Find("StoreRoom").transform;
    s_dimensionStoreroom = new Dimension()
    {
      Id = s_dimensions.Count,

      Origin = new Vector3(27f, 0f, -30f),
      Offset = Vector3.zero,

      Transform = dimensionMap,
      Materials = new Material[] {
        dimensionMap.GetChild(0).GetChild(0).GetComponent<MeshRenderer>().sharedMaterials[0],
      },
      Effects = new VisualEffect[] {
        dimensionMap.GetChild(3).GetChild(0).GetComponent<VisualEffect>()
      },
      NetworkObjects = new(),

      BoundsX = new Vector2(-9f, 0f)
    };
    s_dimensions.Add(s_dimensionStoreroom.Id, s_dimensionStoreroom);
    s_dimensionStoreroom.SetOffset(s_dimensionStoreroom.Offset);
    s_dimensionStoreroom.Hide();
  }

  //
  public static void Decompose()
  {
    foreach (var dimensionData in s_dimensions)
    {
      var dimension = dimensionData.Value;
      if (dimension.Visible)
        dimension.Hide();
    }
  }

  //
  public static void Update()
  {

    for (var i = 0; i < s_dimensions.Keys.Count; i++)
    {

      var dimension = s_dimensions[i];
      if (dimension.Offset == dimension.OffsetDesired) continue;

      var offsetDiff = dimension.OffsetDesired - dimension.Offset;
      if (offsetDiff.magnitude < 0.02f)
      {
        dimension.Offset = dimension.OffsetDesired;
      }
      else
      {
        dimension.Offset += offsetDiff * Time.deltaTime * 5f;
      }

      s_dimensions[i] = dimension;
      dimension.SetOffset(dimension.Offset);
    }

  }

  //
  public static Dimension GetDimension(int dimensionId)
  {
    return s_dimensions[dimensionId];
  }

  // Currently display dimension
  public static int s_dimensionLeftId = -1, s_dimensionRightId = -1;
  public static Dimension s_DimensionLeft { get { return s_dimensions[s_dimensionLeftId]; } set { s_dimensions[s_dimensionLeftId] = value; } }
  public static Dimension s_DimensionRight { get { return s_dimensions[s_dimensionRightId]; } set { s_dimensions[s_dimensionRightId] = value; } }

  //
  public static void IncrementDimensionOffset(int dimensionId, Vector3 offset)
  {

    var dimension = s_dimensions[dimensionId];
    var dimensionOffset = dimension.OffsetDesired;
    var dimensionBounds = dimension.BoundsX;

    dimensionOffset += new Vector3(offset.x, offset.y, offset.z);
    dimensionOffset.x = Mathf.Clamp(dimensionOffset.x, dimensionBounds.x, dimensionBounds.y);
    dimensionOffset.z = Mathf.Clamp(dimensionOffset.z, -0f, 0f);

    CmdSetDimensionOffsetDesired(dimensionId, dimensionOffset);
  }

  //
  [Command]
  public static void CmdSetDimensionOffsetDesired(int dimensionId, Vector3 offset)
  {
    NetworkEventManager.s_Singleton.RpcSetDimensionOffsetDesired(dimensionId, offset);
  }
  public static void SetDimensionOffsetDesired(int dimensionId, Vector3 offset)
  {

    // Set offset
    var dimension = s_dimensions[dimensionId];
    dimension.OffsetDesired = offset;
    s_dimensions[dimensionId] = dimension;
  }
  public static void SetDimensionOffset(int dimensionId, Vector3 offset)
  {

    // Set offset
    var dimension = s_dimensions[dimensionId];
    dimension.Offset = offset;
    s_dimensions[dimensionId] = dimension;

    // Set map / objects offset
    foreach (var material in dimension.Materials)
      material.SetVector("_InclusionOffset", offset);
    if (dimension.Effects != null)
      foreach (var effect in dimension.Effects)
        effect.SetVector3("InclusionOffset", offset);

    var dimensionObects = dimension.NetworkObjects;
    foreach (var networkObjectData in dimensionObects)
      networkObjectData.Value.SetDimensionOffset(offset);

    //
    UpdateRoomCollider(dimensionId);
  }
  public static void SetOffset(this Dimension dimension, Vector3 offset)
  {
    SetDimensionOffset(dimension.Id, offset);
  }

  //
  static void UpdateRoomCollider(int dimensionId)
  {
    var dimension = s_dimensions[dimensionId];
    var offset = dimension.Offset;

    // Check left/right collider
    if (dimensionId == s_dimensionLeftId)
    {
      var dimensionCollider = GameObject.Find("RoomCollider0").transform;
      var dimensionOrigin = s_DimensionLeft.Origin;
      dimensionCollider.position = new Vector3(dimensionOrigin.x, 0f, dimensionOrigin.z) - offset;
    }

    else if (dimensionId == s_dimensionRightId)
    {
      var dimensionCollider = GameObject.Find("RoomCollider1").transform;
      var dimensionOrigin = s_DimensionRight.Origin;
      dimensionCollider.position = new Vector3(dimensionOrigin.x, 0f, dimensionOrigin.z) - offset;
    }
  }
  static void HideRoomCollider(int dimensionId)
  {
    // Check left/right collider
    if (dimensionId == s_dimensionLeftId)
    {
      var dimensionCollider = GameObject.Find("RoomCollider0").transform;
      dimensionCollider.position = new Vector3(200f, 0f, 0f);
    }

    else if (dimensionId == s_dimensionRightId)
    {
      var dimensionCollider = GameObject.Find("RoomCollider1").transform;
      dimensionCollider.position = new Vector3(200f, 0f, 0f);
    }
  }

  //
  public static void AddToDimension(int dimensionId, CustomNetworkObject networkObject)
  {
    var dimension = s_dimensions[dimensionId];
    dimension.NetworkObjects.Add(networkObject._Id, networkObject);
    s_dimensions[dimensionId] = dimension;

    // Check hiden
    if (!dimension.Visible)
      dimension.Hide();
  }
  public static void RemoveFromDimension(int dimensionId, CustomNetworkObject networkObject)
  {
    var dimension = s_dimensions[dimensionId];
    dimension.NetworkObjects.Remove(networkObject._Id);
    s_dimensions[dimensionId] = dimension;
  }

  //
  static bool CanToggleDimension(int dimensionId, bool toggle, bool left)
  {
    var currentDimension = left ? s_dimensionLeftId : s_dimensionRightId;
    var otherDimension = !left ? s_dimensionLeftId : s_dimensionRightId;
    var dirText = left ? "left" : "right";

    if (toggle && currentDimension > -1)
    {
      Debug.LogError($"Trying to occupy occupied {dirText} dimension [{currentDimension}] with [{dimensionId}]");
      return false;
    }
    if (!toggle && currentDimension < 0)
    {
      Debug.LogError($"Trying to remove un-occupied {dirText} dimension [{currentDimension}] with [{dimensionId}]");
      return false;
    }
    if (toggle && otherDimension == dimensionId)
    {
      Debug.LogError($"Trying to duplicate {dirText} add dimension [{currentDimension}]");
      return false;
    }
    return true;
  }
  public static void ToggleDimension(int dimensionId, bool toggle, bool left)
  {

    //
    var dimension = s_dimensions[dimensionId];
    foreach (var material in dimension.Materials)
    {
      material.SetInt("_InDimensions", toggle ? 1 : 0);
      material.SetInt("_DimensionRight", left ? 0 : 1);
    }
    if (dimension.Effects != null)
      foreach (var effect in dimension.Effects)
      {
        effect.SetBool("InDimensions", toggle);
        effect.SetBool("DimensionRight", !left);
      }

    // Change map bounds
    var meshFilters = dimension.Transform.GetChild(0).GetComponentsInChildren<MeshFilter>()
      .Concat(dimension.Transform.GetChild(2).GetComponentsInChildren<MeshFilter>())
      .ToArray();
    for (var i = 0; i < meshFilters.Length; i++)
    {
      var meshFilter = meshFilters[i];
      var bounds = meshFilter.mesh.bounds;
      bounds.Expand(200f * (toggle ? 1f : -1f));
      meshFilter.mesh.bounds = bounds;
    }

    // Objects
    var dimensionObects = dimension.NetworkObjects;
    foreach (var networkObjectData in dimensionObects)
      networkObjectData.Value.ToggleDimension(toggle, left);

    // Hide room collider
    if (!toggle)
      HideRoomCollider(dimensionId);

    // Store new dimension data
    if (left)
    {
      dimension.DimensionLeft = true;
      if (toggle)
        s_dimensionLeftId = dimensionId;
      else
        s_dimensionLeftId = -1;
    }
    else
    {
      dimension.DimensionLeft = false;
      if (toggle)
        s_dimensionRightId = dimensionId;
      else
        s_dimensionRightId = -1;
    }
    s_dimensions[dimensionId] = dimension;

    //
    UpdateRoomCollider(dimensionId);
  }
  public static void ToggleDimension(int dimensionId, bool toggle)
  {
    var dimension = s_dimensions[dimensionId];
    ToggleDimension(dimensionId, toggle, dimension.DimensionLeft);
  }
  public static void ToggleDimension(this Dimension dimension, bool toggle)
  {
    ToggleDimension(dimension.Id, toggle);
  }

  //
  public static void HideDimension(int dimensionId)
  {
    //
    var dimension = s_dimensions[dimensionId];
    foreach (var material in dimension.Materials)
      material.SetInt("_InDimensions", 0);
    if (dimension.Effects != null)
      foreach (var effect in dimension.Effects)
        effect.SetBool("InDimensions", false);

    foreach (var networkObjectData in dimension.NetworkObjects)
      networkObjectData.Value.ToggleDimension(false, false);
  }
  public static void Hide(this Dimension dimension)
  {
    HideDimension(dimension.Id);
  }

  //
  public static void SetDimensionMagic(int dimensionId, float magic)
  {
    //
    var dimension = s_dimensions[dimensionId];
    foreach (var material in dimension.Materials)
      material.SetFloat("_Magic", magic);
    if (dimension.Effects != null)
      foreach (var effect in dimension.Effects)
        effect.SetFloat("Magic", magic);

    var dimensionObects = dimension.NetworkObjects;
    foreach (var networkObjectData in dimensionObects)
      networkObjectData.Value.SetDimensionMagic(magic);
  }

  [Command]
  public static void CmdHideSmooth(int dimensionId)
  {
    if (!CanToggleDimension(dimensionId, false, s_dimensions[dimensionId].DimensionLeft)) return;
    NetworkEventManager.s_Singleton.RpcHideDimension(dimensionId);
  }
  public static void HideSmooth(int dimensionId)
  {

    IEnumerator HideSmoothCo()
    {

      var t = 1f;
      while (t > 0f)
      {

        yield return new WaitForSeconds(0.01f);
        t -= 0.016f;

        SetDimensionMagic(dimensionId, EasingOut(t / 1f));
      }

      SetDimensionMagic(dimensionId, 0f);
      ToggleDimension(dimensionId, false);
    }

    SetDimensionMagic(dimensionId, 1f);
    GameController.s_Singleton.StartCoroutine(HideSmoothCo());

    SfxManager.PlayAudioSourceSimple(
      new Vector3(10f * (s_dimensions[dimensionId].DimensionLeft ? -1f : 1f), 0f, 0f),
      GameController.s_Singleton._SfxProfiles[0]._audioClips[1],

      0.6f,
      0.9f, 1.1f,

      SfxManager.AudioPriority.HIGH
    );
  }
  [Command]
  public static void CmdShowSmooth(int dimensionId, bool left)
  {
    if (!CanToggleDimension(dimensionId, true, left)) return;
    NetworkEventManager.s_Singleton.RpcShowDimension(dimensionId, left);
  }
  public static void ShowSmooth(int dimensionId, bool left)
  {

    IEnumerator ShowSmoothCo()
    {

      var t = 1f;
      while (t > 0f)
      {

        yield return new WaitForSeconds(0.01f);
        t -= 0.012f;

        SetDimensionMagic(dimensionId, EasingIn(1f - t / 1f));
      }

      SetDimensionMagic(dimensionId, 1f);
    }

    ToggleDimension(dimensionId, true, left);
    SetDimensionMagic(dimensionId, 0f);
    GameController.s_Singleton.StartCoroutine(ShowSmoothCo());
    SfxManager.PlayAudioSourceSimple(
      new Vector3(10f * (s_dimensions[dimensionId].DimensionLeft ? -1f : 1f), 0f, 0f),
      GameController.s_Singleton._SfxProfiles[0]._audioClips[0],

      0.6f,
      0.9f, 1.1f,

      SfxManager.AudioPriority.HIGH
    );
  }

  static float EasingIn(float x)
  {
    var c4 = 2f * Mathf.PI / 3f;
    return x == 0
      ? 0
      : x == 1
      ? 1
      : Mathf.Pow(2, -10 * x) * Mathf.Sin((x * 10f - 0.75f) * c4) + 1;
  }

  static float EasingOut(float x)
  {
    return Mathf.Sqrt(1 - Mathf.Pow(x - 1, 2));

    /*var c4 = 2f * Mathf.PI / 3f;

    return x == 0
      ? 0
      : x == 1
      ? 1
      : -Mathf.Pow(2, 10 * x - 10) * Mathf.Sin((x * 10f - 10.75f) * c4);*/
  }

}
