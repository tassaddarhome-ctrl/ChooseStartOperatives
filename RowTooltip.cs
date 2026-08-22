using System;
using MGSC;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChooseStartOperatives
{
    /// <summary>
    /// Тултип при наведении на строку окна выбора. Показ и скрытие задаются
    /// делегатами: для наёмников — родной BuildMercenaryTooltip(null, profile),
    /// для классов — простой текстовый тултип со списком перков.
    /// </summary>
    public class RowTooltip : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
    {
        private Action _show;
        private Action _hide;
        private bool _shown;

        public static void Attach(GameObject target, Action show, Action hide)
        {
            RowTooltip tooltip = target.AddComponent<RowTooltip>();
            tooltip._show = show;
            tooltip._hide = hide;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_shown && _show != null)
            {
                _shown = true;
                _show();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void Hide()
        {
            if (_shown)
            {
                _shown = false;
                if (_hide != null)
                {
                    _hide();
                }
            }
        }
    }
}
