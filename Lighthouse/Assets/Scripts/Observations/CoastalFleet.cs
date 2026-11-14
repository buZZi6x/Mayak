using System.Linq;
using UnityEngine;
namespace LighthouseGame
{
 public class CoastalFleet:MonoBehaviour
 {
  public Transform[] Vessels;
  public int Selected;
  public string VesselName => new[]{"Лоцманский катер","Фрегат «Меридиан»","Барк «Пилигрим»","Пароход «Северный»"}[Mathf.Clamp(Selected,0,3)];
  public string PhotoKey => PhotoKeys[Mathf.Clamp(Selected,0,3)];
  public int PhotoPoints => 60 + 20 * Mathf.Clamp(Selected,0,3);
  private static readonly string[] PhotoKeys = { "boat", "boat-frigate", "boat-barque", "boat-steamer" };
  Transform[] sails;Quaternion[] rest;
  void Awake(){sails=GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("ANIM_Sail")||t.name.StartsWith("ANIM_Ensign")).ToArray();rest=sails.Select(t=>t.localRotation).ToArray();Select(Selected);}
  public void Select(int kind){Selected=(kind+Vessels.Length)%Vessels.Length;for(int i=0;i<Vessels.Length;i++)Vessels[i].gameObject.SetActive(i==Selected);if(TryGetComponent<OceanBuoyancy>(out var b)){b.Length=Selected==0?14:Selected==1?44:Selected==2?51:56;b.Width=Selected==0?4:11;}}
  void Update(){if(sails==null)return;for(int i=0;i<sails.Length;i++)sails[i].localRotation=rest[i]*Quaternion.Euler(Mathf.Sin(Time.time*1.4f+i)*1.7f,Mathf.Sin(Time.time*.8f+i)*2.2f,0);}
 }
}
