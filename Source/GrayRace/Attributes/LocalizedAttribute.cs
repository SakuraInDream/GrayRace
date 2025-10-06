using System;
using Verse;

namespace SD.GrayRace.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class LocalizedAttribute: Attribute
    {
        private string text;

        public LocalizedAttribute(string key)
        {
            text = key;
        }

        public string Text
        {
            get => text;
            set => text = value;
        }

        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (obj is LocalizedAttribute other)
            {
                return text == other.text;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Text.GetHashCode();
        }
    }
}
