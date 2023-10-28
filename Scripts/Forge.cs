using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public class Forge : CustomNetworkObject
{
  // Start is called before the first frame update
  void Start()
  {

    //
    if (isServer)
    {

      _OnNetworkCollision += (CustomNetworkObject networkObject) =>
      {

        Debug.Log($"Forge collision with {networkObject.gameObject.name}");

      };
    }

  }

  // Update is called once per frame
  void Update()
  {

  }
}
