using System;
using System.Collections.Generic;
using System.Text;

namespace pictureviewer
{
    public class Ratio
    {
        public Ratio(int numerator, int denominator)
        {
            this.numerator = numerator;
            this.denominator = denominator;
        }

        public readonly int numerator;
        public readonly int denominator;
        
        public override string ToString()
        {
            if (denominator == 1)
                return numerator.ToString();
            else
                return numerator.ToString() + "/" + denominator.ToString();
        }

        public static Ratio Invalid = new Ratio(-1, -1);

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
