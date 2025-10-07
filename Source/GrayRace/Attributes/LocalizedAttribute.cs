using System;
using Verse;

namespace SD.GrayRace.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class LocalizedAttribute: Attribute
    {
        private string _text;

        public LocalizedAttribute(string key)
        {
            _text = key;
        }

        public string Text
        {
            get => _text;
            set => _text = value;
        }

        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (obj is LocalizedAttribute other)
            {
                return _text == other._text;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Text.GetHashCode();
        }
    }
}
