using UnityEngine;
namespace TempleDesign {
 public class TempleLever:TempleInteractable {
  public TempleDoor[] targets;public TempleDoor alternate;public Transform handle;public bool selector;public bool activated;
  public override void Use(TemplePlayer p){activated=selector?!activated:true;foreach(var d in targets){d.locked=false;d.SetOpen(activated);}if(alternate){alternate.locked=true;alternate.SetOpen(!activated);}if(handle)handle.localRotation=Quaternion.Euler(activated?-35:35,0,0);p.Notify(selector?(activated?"Узел: открыт проход в 5. Возврат в 6 свободен.":"Узел: открыт проход в 4. Возврат в 6 свободен."):"Решётка открыта.");}
 }
}
