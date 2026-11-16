using UnityEngine;
using UnityEngine.EventSystems;

namespace Hackathon.Menu
{
    public sealed class MenuCancelHandler : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private MainMenuController menu;

        public void OnCancel(BaseEventData eventData)
        {
            menu.GoBack();
            eventData.Use();
        }
    }
}
