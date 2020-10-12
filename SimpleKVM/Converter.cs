namespace SimpleKVM
{
    public static class Converter
    {
        public static (int Feet, double Inches) CentimetresToFeetInches(double centimetres)
        {
            var feet = centimetres / 2.54 / 12;
            var iFeet = (int)feet;
            var inches = (feet - iFeet) * 12;

            return (iFeet, inches);
        }

        public static string CentimetresToFeetInchesString(double centimetres, string footSymbol = " foot", string inchesSymbol = " inches")
        {
            (var feet, var inches) = CentimetresToFeetInches(centimetres);
            return $"{feet:N0}{footSymbol}, {inches:N0}{inchesSymbol}";
        }
    }
}