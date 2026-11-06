using Stylemeter.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stylemeter.Game.Menu
{
    internal enum RowKind
    {
        Multiplier,
        Section,
        WholeSection,
        Bonus,
    }

    // One line of the panel. The game's input module sends the selected object submit, cancel and move events, so
    // gamepad, keyboard and mouse all arrive here; up and down travel the explicit navigation the panel sets.
    internal sealed class PanelRow : Selectable, ISubmitHandler, ICancelHandler, IPointerClickHandler
    {
        public ModPanel Panel;
        public RowKind Kind;
        public BonusGroup Group;
        public BonusDef Bonus;
        public RectTransform Rect;
        public Image Glow;
        public Text Label;
        public Text Value;
        public Text Points;
        public Image LeftArrow;
        public Image RightArrow;
        public Image Chevron;
        public float Height;
        public float Top;

        public void OnSubmit(BaseEventData eventData) => Panel.Activate(this);

        public void OnCancel(BaseEventData eventData) => Panel.Close();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Panel.Click(this, eventData);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                Panel.Adjust(this, eventData.moveDir == MoveDirection.Right ? 1 : -1);
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            Panel.OnRowSelected(this, eventData);
        }
    }
}
