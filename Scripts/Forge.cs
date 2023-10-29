using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public class Forge : CustomNetworkObject
{

  bool _canRetrieve;

  List<ObjectType> _storedObjects;
  UnityEngine.UI.Slider _slider;

  // Start is called before the first frame update
  void Start()
  {
    Init(ObjectType.BLOCK_FORGE);

    _slider = GameController.SliderManager.GetSlider();
    SetSliderPos();

    //
    if (isServer)
    {

      _storedObjects = new();
      _OnNetworkCollision += (CustomNetworkObject networkObject) =>
      {

        Debug.Log($"Forge collision with {networkObject.gameObject.name}");

        // Make sure is material
        if (!(networkObject is BasicMaterial))
          return;

        //
        if (networkObject._ObjectType != ObjectType.OBJECT_ORE_IRON)
          return;

        // Store object
        if (_storedObjects.Count < 2)
        {
          _storedObjects.Add(networkObject._ObjectType);
          networkObject.NetworkDestroy();

          Debug.Log($"Stored: {networkObject._ObjectType}");
        }

        // Check full
        if (_storedObjects.Count == 2)
        {
          Debug.Log("Full");
          _canRetrieve = true;
        }

      };
    }
  }

  // Update is called once per frame
  void Update()
  {
    SetSliderPos();
  }

  //
  void SetSliderPos()
  {
    _slider.transform.position = Camera.main.WorldToScreenPoint(transform.position);
  }

  //

}
