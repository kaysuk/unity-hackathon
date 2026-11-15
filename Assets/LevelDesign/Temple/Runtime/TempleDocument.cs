using UnityEngine;
namespace TempleDesign {
 public class TempleDocument:TempleInteractable {public int number;public TempleDoor access;[TextArea]public string text;public override void Use(TemplePlayer p){if(access&&!access.open){p.Notify("Документ защищён решёткой.");return;}p.documents.Add(number);p.Notify(text,12);gameObject.SetActive(false);}}
}
