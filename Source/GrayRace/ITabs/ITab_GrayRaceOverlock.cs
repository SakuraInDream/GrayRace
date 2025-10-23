using RimWorld;
using UnityEngine;
using Verse;
namespace SD.GrayRace.ITabs
{
    // 未来会添加更多功能
    public class ITab_GrayRaceOverlock: ITab
    {
        private Pawn _pawn;
        private Vector2 _scrollPos;

        public ITab_GrayRaceOverlock()
        {
            size = new Vector2(UI.screenWidth * 0.75f, UI.screenHeight * 0.75f);
            labelKey = "超频";
        }

        protected override void FillTab()
        {
            if (_pawn != SelPawn)
            {
                CloseTab();
                return;
            }

            var font = Text.Font;
            var anchor = Text.Anchor;

            var rect = new Rect(Vector2.one * 20f, size - Vector2.one * 40f);
            var rect2 = new Rect(rect.x, rect.y, size.x * 0.25f, rect.height);
            rect2.xMin += size.x * 0.25f;

            float curY = rect2.y;

            var viewRect = new Rect();

            Listing_Standard listing_Standard = new Listing_Standard();
            // Widgets.BeginScrollView(, ref _scrollPos, viewRect);


        }

        public override void OnOpen()
        {
            base.OnOpen();
            _pawn = SelPawn;
        }

        public override bool IsVisible
        {
            get
            {
                if (SelPawn == null) return false;

                return SelPawn.IsGrayRace();
            }
        }
    }
}
