using UnityEngine;
namespace TempleDesign {public class TemplePlaque:TempleInteractable{[TextArea]public string text;public override void Use(TemplePlayer p){p.Notify(text,12);}}}
