using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace TempleDesign {
 [RequireComponent(typeof(CharacterController))]
 public class TemplePlayer:MonoBehaviour {
  public Camera view;public float speed=7,reach=6;public HashSet<int> documents=new HashSet<int>();float pitch,fall;TempleInteractable target;string notice="";float until;
  void OnEnable(){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
  void OnDisable(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
  public void Notify(string text,float seconds=4){notice=text;until=Time.time+seconds;}
  void Update(){
   Vector2 move=Vector2.zero,look=Vector2.zero;bool use=false,esc=false,click=false,sprint=false;
#if ENABLE_INPUT_SYSTEM
   var k=Keyboard.current;var m=Mouse.current;if(k!=null){move=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));use=k.eKey.wasPressedThisFrame;esc=k.escapeKey.wasPressedThisFrame;sprint=k.leftShiftKey.isPressed;}if(m!=null){look=m.delta.ReadValue()*.09f;click=m.leftButton.wasPressedThisFrame;}
#else
   move=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));look=new Vector2(Input.GetAxis("Mouse X"),Input.GetAxis("Mouse Y"))*2;use=Input.GetKeyDown(KeyCode.E);esc=Input.GetKeyDown(KeyCode.Escape);click=Input.GetMouseButtonDown(0);sprint=Input.GetKey(KeyCode.LeftShift);
#endif
   if(esc){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}if(click){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
   if(Cursor.lockState!=CursorLockMode.Locked)return;
   transform.Rotate(0,look.x,0);pitch=Mathf.Clamp(pitch-look.y,-85,85);view.transform.localRotation=Quaternion.Euler(pitch,0,0);
   var controller=GetComponent<CharacterController>();if(controller.isGrounded&&fall<0)fall=-2;fall+=Physics.gravity.y*Time.deltaTime;
   var velocity=transform.TransformDirection(new Vector3(move.x,0,move.y).normalized)*speed*(sprint?1.7f:1);velocity.y=fall;controller.Move(velocity*Time.deltaTime);
   target=null;if(Physics.Raycast(view.transform.position,view.transform.forward,out var hit,reach,~0,QueryTriggerInteraction.Ignore))target=hit.collider.GetComponentInParent<TempleInteractable>();if(use&&target)target.Use(this);
  }
  void OnGUI(){var style=new GUIStyle(GUI.skin.label){fontSize=18,alignment=TextAnchor.MiddleCenter,wordWrap=true};GUI.Label(new Rect(Screen.width/2-10,Screen.height/2-15,20,30),"·",style);if(target)GUI.Box(new Rect(Screen.width/2-260,Screen.height-115,520,40),"E — "+target.prompt);if(Time.time<until)GUI.Box(new Rect(Screen.width/2-360,25,720,120),notice,style);GUI.Label(new Rect(20,Screen.height-45,650,30),"WASD — движение • мышь — взгляд • E — действие • Shift — бег • Esc — курсор    Документы: "+documents.Count+"/6");}
 }
}
