using UnityEngine;
namespace TempleDesign {
 public class TempleDoor:TempleInteractable {
  public Transform[] panels; public Vector3[] offsets;public Vector3[] angles;
  public bool locked,insideOnly,open,permanent;public Vector3 insideDirection;
  Vector3[] starts;Quaternion[] rotations;float blend;
  void Awake(){Cache();}
  void Cache(){if(starts!=null)return;starts=new Vector3[panels.Length];rotations=new Quaternion[panels.Length];for(int i=0;i<panels.Length;i++){starts[i]=panels[i].localPosition;rotations[i]=panels[i].localRotation;}}
  public override void Use(TemplePlayer p){
   if(locked){p.Notify("Проход закрыт механизмом.");return;}
   if(insideOnly&&!open&&Vector3.Dot(p.transform.position-transform.position,insideDirection)<0){p.Notify("Заперто с другой стороны.");return;}
   SetOpen(permanent||!open);
  }
  public void SetOpen(bool value){Cache();open=value;if(value)insideOnly=false;}
  void Update(){Cache();blend=Mathf.MoveTowards(blend,open?1:0,Time.deltaTime*.8f);float t=Mathf.SmoothStep(0,1,blend);for(int i=0;i<panels.Length;i++){panels[i].localPosition=starts[i]+offsets[i]*t;panels[i].localRotation=rotations[i]*Quaternion.Euler(angles[i]*t);}}
 }
}
