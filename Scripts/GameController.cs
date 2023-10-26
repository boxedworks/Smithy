using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{

  public static GameController s_Singleton;
  Mirror.NetworkManager _networkManager;

  // Start is called before the first frame update
  void Start()
  {
    s_Singleton = this;

    _networkManager = GameObject.Find("Mirror").GetComponent<Mirror.NetworkManager>();
#if UNITY_EDITOR
    _networkManager.StartHost();
#else
    _networkManager.StartClient();
#endif
  }

  // Update is called once per frame
  void Update()
  {

  }
}
