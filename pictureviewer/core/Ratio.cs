using System;

namespace pictureviewer
{
    // A simple pair of ints that supports serializing.
    //  Absolutely nothing smart is done -- ratios are not normalized, and +-*/= operators are not provided.
    public class Ratio
    {
        public Ratio(int numerator, int denominator)
        {
            this.numerator = numerator;
            this.denominator = denominator;
        }

        public readonly int numerator;
        public readonly int denominator;
        
        // Outputs x/y, or just x if y=1
        public override string ToString()
        {
            if (denominator == 1)
                return numerator.ToString();
            else
                return numerator.ToString() + "/" + denominator.ToString();
        }

        // An out of band value and for when A ratio property is specified. 
        // I.e., it's like null for ratios.
        // (We don't use an actual null so serialization is nice)
        public static readonly Ratio Invalid = new Ratio(-1, -1);

        public bool IsValid
        {
            get { return numerator != -1 && denominator != -1; }
        }

        // for serialization
        public static Ratio Parse(string text)
        {
            var parts = text.Split('/');
            if (parts.Length == 0 || parts.Length > 2)
                throw new ArgumentException("not the right number of /'s");
            int numerator = int.Parse(parts[0]);
            int denominator = 1;
            if (parts.Length > 1)
            {
                denominator = int.Parse(parts[1]);
            }
            return new Ratio(numerator, denominator);
        }
    }
}
