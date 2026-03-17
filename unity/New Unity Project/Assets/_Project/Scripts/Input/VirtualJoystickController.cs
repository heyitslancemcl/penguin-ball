using UnityEngine;
using UnityEngine.EventSystems;

namespace PenguineBall.Input
{
    /// <summary>
    /// Canvas-based virtual joystick. Handles touch drag and outputs a
    /// normalised Vector2 in the same [-1, 1] range as GyroscopeInputProvider.
    /// Attach to the joystick background Image in the VirtualJoystick prefab.
    /// ADR-001: Input Abstraction Layer.
    /// </summary>
    public class VirtualJoystickController : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _handle;
        [SerializeField] private float _radius = 75f;

        private Vector2 _input = Vector2.zero;
        private RectTransform _background;

        /// <summary>
        /// Normalised joystick input in [-1, 1] on each axis.
        /// Consumed by JoystickInputProvider.GetMovementInput().
        /// </summary>
        public Vector2 Input => _input;

        private void Awake()
        {
            _background = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _background, eventData.position,
                    eventData.pressEventCamera, out Vector2 localPoint))
                return;

            Vector2 clamped = Vector2.ClampMagnitude(localPoint, _radius);
            _handle.localPosition = clamped;
            _input = clamped / _radius; // Normalise to [-1, 1]
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _input = Vector2.zero;
            _handle.localPosition = Vector2.zero;
        }
    }
}
