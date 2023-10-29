using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public class GameController : MonoBehaviour
{

  public static GameController s_Singleton;

  // Start is called before the first frame update
  void Start()
  {
    s_Singleton = this;

    // Init
    SfxManager.Init();
    SliderManager.Init();

    // Network
#if UNITY_EDITOR
    NetworkManager.singleton.StartHost();
#else
    NetworkManager.singleton.StartClient();
#endif

    //
    CustomNetworkObject.SpawnNetworkObject(CustomNetworkObject.ObjectType.OBJECT_ORE_IRON, new Vector3(0f, 20f, 0f));
    CustomNetworkObject.SpawnNetworkObject(CustomNetworkObject.ObjectType.OBJECT_ORE_IRON, new Vector3(2f, 20f, 0f));
  }

  // Update is called once per frame
  void Update()
  {

    SfxManager.Update();

  }

  //
  public static class SliderManager
  {

    // Hold all slider data
    struct SliderData
    {

      //
      public UnityEngine.UI.Slider Slider;

      //
      public void Toggle(bool toggle)
      {
        Slider.gameObject.SetActive(toggle);
      }

    }
    static SliderData _og;
    static List<SliderData> s_sliders;

    //
    public static void Init()
    {
      _og = new SliderData()
      {
        Slider = GameObject.Find("SliderOG").GetComponent<UnityEngine.UI.Slider>()
      };
      _og.Toggle(false);

      //
    }

    //
    public static UnityEngine.UI.Slider GetSlider()
    {
      var newSlider = new SliderData()
      {
        Slider = GameObject.Instantiate(_og.Slider.gameObject).GetComponent<UnityEngine.UI.Slider>()
      };
      newSlider.Slider.transform.parent = _og.Slider.transform.parent;
      newSlider.Slider.gameObject.SetActive(true);

      return newSlider.Slider;
    }

  }
}
