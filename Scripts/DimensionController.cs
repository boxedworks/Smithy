using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DimensionController
{

  //
  public struct Dimension
  {

    public int Id;

    public Vector3 Origin, Offset;
    public Vector2 BoundsX;

    public bool DimensionLeft;

    public Material[] MapMaterials;
    public Dictionary<int, CustomNetworkObject> NetworkObjects;

    //
    public Vector3 EntrancePosition { get { return new Vector3(Origin.x + 3f, 1.2f, Origin.z + 5.5f - 0.5f); } }

    //
  }

  // Available dimensions
  static Dictionary<int, Dimension> s_dimensions;
  static Dimension s_dimensionForest, s_dimensionMine;

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

      MapMaterials = new Material[] {
        dimensionMap.GetChild(0).GetChild(0).GetComponent<MeshRenderer>().sharedMaterials[0],
        dimensionMap.GetChild(2).GetChild(0).GetComponent<MeshRenderer>().sharedMaterials[0]
      },
      NetworkObjects = new(),

      BoundsX = new Vector2(-9f, 0f)
    };
    s_dimensions.Add(s_dimensionForest.Id, s_dimensionForest);

    //
    dimensionMap = GameObject.Find("Mine").transform;
    s_dimensionMine = new Dimension()
    {
      Id = s_dimensions.Count,

      Origin = new Vector3(27f, 0f, 0f),
      Offset = Vector3.zero,

      MapMaterials = new Material[] {
        dimensionMap.GetChild(0).GetChild(0).GetComponent<MeshRenderer>().sharedMaterials[0],
        dimensionMap.GetChild(0).GetChild(1).GetComponent<MeshRenderer>().sharedMaterials[0],
      },
      NetworkObjects = new(),

      BoundsX = new Vector2(-7f, 0f)
    };
    s_dimensions.Add(s_dimensionMine.Id, s_dimensionMine);
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

    var dimensionData = s_dimensions[dimensionId];
    var dimensionOffset = dimensionData.Offset;
    var dimensionBounds = dimensionData.BoundsX;

    dimensionOffset += new Vector3(offset.x, offset.y, offset.z);
    dimensionOffset.x = Mathf.Clamp(dimensionOffset.x, dimensionBounds.x, dimensionBounds.y);
    dimensionOffset.z = Mathf.Clamp(dimensionOffset.z, -0f, 0f);

    SetDimensionOffset(dimensionId, dimensionOffset);
  }

  //
  public static void SetDimensionOffset(int dimensionId, Vector3 offset)
  {

    // Set offset
    var dimensionData = s_dimensions[dimensionId];
    dimensionData.Offset = offset;
    s_dimensions[dimensionId] = dimensionData;

    // Set map / objects offset
    var dimensionMap = dimensionData.MapMaterials;
    for (var i = 0; i < dimensionMap.Length; i++)
    {
      var material = dimensionMap[i];
      material.SetVector("_InclusionOffset", offset);
    }

    var dimensionObects = dimensionData.NetworkObjects;
    foreach (var networkObjectData in dimensionObects)
      networkObjectData.Value.SetDimensionOffset(dimensionId, offset);

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

  //
  public static void AddToDimension(int dimensionId, CustomNetworkObject networkObject)
  {
    var dimensionData = s_dimensions[dimensionId];
    dimensionData.NetworkObjects.Add(networkObject._Id, networkObject);
    s_dimensions[dimensionId] = dimensionData;
  }
  public static void RemoveFromDimension(int dimensionId, CustomNetworkObject networkObject)
  {
    var dimensionData = s_dimensions[dimensionId];
    dimensionData.NetworkObjects.Remove(networkObject._Id);
    s_dimensions[dimensionId] = dimensionData;
  }

  //
  public static void ToggleDimension(int dimensionId, bool toggle, bool left)
  {

    var currentDimension = left ? s_dimensionLeftId : s_dimensionRightId;
    var otherDimension = !left ? s_dimensionLeftId : s_dimensionRightId;
    if (toggle && currentDimension > -1)
    {
      Debug.LogError($"Trying to occupy occupied dimension [{currentDimension}] with [{dimensionId}] {left}");
      return;
    }
    if (!toggle && currentDimension < 0)
    {
      Debug.LogError($"Trying to remove un-occupied dimension [{currentDimension}] with [{dimensionId}] {left}");
      return;
    }
    if (toggle && otherDimension == dimensionId)
    {
      Debug.LogError($"Trying to duplicate add dimension [{currentDimension}] {left}");
      return;
    }

    //
    var dimensionData = s_dimensions[dimensionId];
    var dimensionMap = dimensionData.MapMaterials;
    for (var i = 0; i < dimensionMap.Length; i++)
    {
      var material = dimensionMap[i];
      material.SetInt("_InDimensions", toggle ? 1 : 0);
      material.SetInt("_DimensionRight", left ? 0 : 1);
    }

    var dimensionObects = dimensionData.NetworkObjects;
    foreach (var networkObjectData in dimensionObects)
      networkObjectData.Value.ToggleDimension(toggle, left);

    //
    if (left)
    {
      dimensionData.DimensionLeft = true;
      if (toggle)
        s_dimensionLeftId = dimensionId;
      else
        s_dimensionLeftId = -1;
    }
    else
    {
      dimensionData.DimensionLeft = false;
      if (toggle)
        s_dimensionRightId = dimensionId;
      else
        s_dimensionRightId = -1;
    }
    s_dimensions[dimensionId] = dimensionData;
  }

}
