using System;
using System.ComponentModel;
using System.Globalization;

namespace Folio.Core;
// A simple pair of ints that supports serializing.
// Ratios are automatically simplified using GCD (e.g., 8/6 becomes 4/3).
[TypeConverter(typeof(RatioConverter))]
public class Ratio {
    public Ratio(int numerator, int denominator) : this(numerator, denominator, simplify: true) {
    }

    // Private constructor for special cases like Invalid
    private Ratio(int numerator, int denominator, bool simplify) {
        // Special case: 0/0 is invalid and only allowed when not simplifying (for Invalid constant)
        if (numerator == 0 && denominator == 0 && !simplify) {
            this.numerator = 0;
            this.denominator = 0;
            return;
        }

        if (denominator == 0)
            throw new ArgumentException("Denominator cannot be zero", nameof(denominator));

        if (simplify) {
            // Simplify the ratio using GCD
            int gcd = GCD(Math.Abs(numerator), Math.Abs(denominator));
            this.numerator = numerator / gcd;
            this.denominator = denominator / gcd;

            // Keep denominator positive (move negative sign to numerator if needed)
            if (this.denominator < 0) {
                this.numerator = -this.numerator;
                this.denominator = -this.denominator;
            }
        } else {
            // Don't simplify
            this.numerator = numerator;
            this.denominator = denominator;
        }
    }

    public readonly int numerator;
    public readonly int denominator;

    // Calculate Greatest Common Divisor using Euclidean algorithm
    private static int GCD(int a, int b) {
        while (b != 0) {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    // Outputs x/y, or just x if y=1
    public override string ToString() {
        if (denominator == 1)
            return numerator.ToString();
        else
            return numerator.ToString() + "/" + denominator.ToString();
    }

    // An out of band value and for when A ratio property is specified.
    // I.e., it's like null for ratios.
    // (We don't use an actual null so serialization is nice)
    public static readonly Ratio Invalid = new Ratio(0, 0, simplify: false);

    public bool IsValid {
        get { return !(numerator == 0 && denominator == 0); }
    }

    // for serialization and parsing
    // Supports both "4/3" and "4:3" formats
    public static Ratio Parse(string text) {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));

        // Try colon separator first (preferred format)
        if (text.Contains(":")) {
            var parts = text.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException("Invalid ratio format. Expected 'numerator:denominator'");
            int numerator = int.Parse(parts[0]);
            int denominator = int.Parse(parts[1]);
            return new Ratio(numerator, denominator);
        }

        // Try slash separator (legacy format)
        if (text.Contains("/")) {
            var parts = text.Split('/');
            if (parts.Length != 2)
                throw new ArgumentException("Invalid ratio format. Expected 'numerator/denominator'");
            int numerator = int.Parse(parts[0]);
            int denominator = int.Parse(parts[1]);
            return new Ratio(numerator, denominator);
        }

        // Single number (denominator is 1)
        int num = int.Parse(text);
        return new Ratio(num, 1);
    }

    // Equality comparison - ratios are equal if they represent the same value
    // Since ratios are automatically simplified, we can just compare numerator and denominator
    public override bool Equals(object? obj) {
        if (obj == null || GetType() != obj.GetType())
            return false;

        Ratio other = (Ratio)obj;
        return numerator == other.numerator && denominator == other.denominator;
    }

    public bool Equals(Ratio? other) {
        if (other == null)
            return false;

        return numerator == other.numerator && denominator == other.denominator;
    }

    // Hash code based on simplified numerator and denominator
    public override int GetHashCode() {
        unchecked {
            int hash = 17;
            hash = hash * 31 + numerator.GetHashCode();
            hash = hash * 31 + denominator.GetHashCode();
            return hash;
        }
    }
}

// Type converter to support XAML serialization and deserialization
public class RatioConverter : TypeConverter {
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
        if (value is string stringValue) {
            return Ratio.Parse(stringValue);
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) {
        if (destinationType == typeof(string) && value is Ratio ratio) {
            // Use colon format for output (4:3)
            return ratio.numerator + ":" + ratio.denominator;
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
