using UnityEngine;

using Mirror;

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


}