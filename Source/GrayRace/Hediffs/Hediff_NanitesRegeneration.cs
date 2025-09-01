using Verse;

namespace SD.GrayRace
{
    public class Hediff_NanitesRegeneration: HediffWithComps
    {
        public override void Notify_Regenerated(float hp)
        {
            base.Notify_Regenerated(hp);
        }

        public override string SeverityLabel => base.SeverityLabel;
    }
}
