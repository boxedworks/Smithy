using UnityEngine;

using Mirror;
using System.Runtime.InteropServices;

public class NetworkEventManager : NetworkBehaviour
{

  public static NetworkEventManager s_Singleton;

  //
  public NetworkEventManager()
  {
    s_Singleton = this;
  }

  // Network handling
  [ClientRpc]
  public void RpcRegisterOrder(CustomNetworkObject.ObjectType result, float duration)
  {
    GameController.OrderManager.HandleRpcOrder(result, duration);
  }

  //
  [ClientRpc]
  public void RpcSetDimensionOffsetDesired(int dimensionId, Vector3 offset)
  {
    DimensionController.SetDimensionOffsetDesired(dimensionId, offset);
  }

  //
  [ClientRpc]
  public void RpcShowDimension(int dimensionId, bool left)
  {
    DimensionController.ShowSmooth(dimensionId, left);
  }
  [ClientRpc]
  public void RpcHideDimension(int dimensionId)
  {
    DimensionController.HideSmooth(dimensionId);
  }


}