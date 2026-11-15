using UnityEngine;
namespace TempleDesign {
 public abstract class TempleInteractable:MonoBehaviour {public string prompt="Взаимодействовать";public abstract void Use(TemplePlayer player);}
}
