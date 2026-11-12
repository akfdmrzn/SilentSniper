using UnityEngine.UI;

namespace SilentContract
{
    /// <summary>A uGUI Text that shows every text in the chosen language (and redraws itself when the language changes).</summary>
    public class LocText : Text
    {
        string source, lang;
        bool upper;

        public override string text
        {
            get => base.text;
            set
            {
                lang = Loc.Lang;
                if (value != null && Loc.UpperSource(value, out var src)) { source = src; upper = true; base.text = value; }
                else { source = value; upper = false; base.text = Loc.Tr(value); }
            }
        }

        /// <summary>Shows the text as it is (already translated: the radio's typewriter shows a growing part of it).</summary>
        public void SetRaw(string s)
        {
            source = null;
            upper = false;
            base.text = s;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Loc.Changed += Redraw;
            if (source != null && lang != Loc.Lang) Redraw();
        }

        protected override void OnDisable()
        {
            Loc.Changed -= Redraw;
            base.OnDisable();
        }

        void Redraw()
        {
            lang = Loc.Lang;
            if (source != null) base.text = upper ? Loc.Up(Loc.Tr(source)) : Loc.Tr(source);
        }
    }
}
