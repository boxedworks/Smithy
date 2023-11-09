using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;
using System.Runtime.InteropServices;

public class GameController : MonoBehaviour
{

  public static GameController s_Singleton;
  public SfxProfile[] _SfxProfiles;

  // Start is called before the first frame update
  void Start()
  {
    s_Singleton = this;

    // Init
    DimensionController.Init();
    SfxManager.Init();
    SliderManager.Init();
    OrderManager.Init();

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
    OrderManager.Update();
    DimensionController.Update();
  }

  //
  public static class ParticleManager
  {


    //
    public enum ParticleType
    {
      NONE,

      CLOUD_SIMPLE,
    }

    //
    public static void PlayParticlesAt(Vector3 posAt, ParticleType particleType)
    {

      var particles = GameObject.Find("Particles").transform.GetChild(((int)particleType) - 1).GetComponent<ParticleSystem>();
      particles.transform.position = posAt;
      particles.Play();

    }

  }

  //
  public class SliderManager
  {

    //
    static SliderManager s_singleton;

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
    SliderData _og;
    List<SliderData> _sliders;

    //
    public static void Init()
    {
      new SliderManager();
    }

    public SliderManager()
    {

      //
      s_singleton = this;

      //
      _og = new SliderData()
      {
        Slider = GameObject.Find("SliderOG").GetComponent<UnityEngine.UI.Slider>()
      };
      _og.Toggle(false);

      //
      //s_sliders = new();
    }

    //
    public static UnityEngine.UI.Slider GetSlider()
    {
      var newSlider = new SliderData()
      {
        Slider = GameObject.Instantiate(s_singleton._og.Slider.gameObject).GetComponent<UnityEngine.UI.Slider>()
      };
      newSlider.Slider.transform.parent = s_singleton._og.Slider.transform.parent;
      newSlider.Slider.gameObject.SetActive(true);

      //s_sliders.Add(newSlider);

      return newSlider.Slider;
    }

    //
    public static void RemoveSlider(UnityEngine.UI.Slider slider)
    {
      GameObject.Destroy(slider.gameObject);
    }

  }

  // Order management
  public class OrderManager
  {

    // Static members
    static OrderManager s_singleton;
    static int s_orderId;

    // Order data
    List<Order> _orders;
    struct Order
    {
      public int ID;

      public float TimeCreated, TimeExpired;
      public CustomNetworkObject.ObjectType DesiredResult;

      // UI
      public UnityEngine.UI.Slider UiSlider;
    }

    // Ui
    GameObject _orderBase;

    // Initialize
    public static void Init()
    {
      new OrderManager();
    }
    public void Reset()
    {
      s_singleton = this;

      _orders = new();
      _orderBase = GameObject.Find("OrderQueue").transform.GetChild(0).GetChild(0).gameObject;
    }
    public OrderManager()
    {
      Reset();
    }

    //
    public static void RegisterOrder(CustomNetworkObject.ObjectType result, float duration)
    {

      // Rpc order to clients
      NetworkEventManager.s_Singleton.RpcRegisterOrder(result, duration);
    }
    public static void HandleRpcOrder(CustomNetworkObject.ObjectType result, float duration)
    {

      // Create a new order
      s_singleton._orders.Add(
        new Order()
        {
          ID = s_orderId++,

          TimeCreated = (float)NetworkTime.time,
          TimeExpired = (float)NetworkTime.time + duration,

          DesiredResult = result,

          UiSlider = GameObject.Instantiate(s_singleton._orderBase, s_singleton._orderBase.transform.parent).transform.GetChild(1).GetComponent<UnityEngine.UI.Slider>()
        });
      s_singleton._orders[^1].UiSlider.transform.parent.gameObject.SetActive(true);
    }


    // Update orders
    public static void Update()
    {

      foreach (var orderData in s_singleton._orders)
      {

        var duration = orderData.TimeExpired - orderData.TimeCreated;
        orderData.UiSlider.value = (float)(1f - (orderData.TimeExpired - NetworkTime.time) / duration);

      }

    }

  }
}
