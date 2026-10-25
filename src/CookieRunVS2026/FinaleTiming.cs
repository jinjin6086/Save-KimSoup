namespace CookieRunVS2026;

// Rendering, simulation and the generated audio share the same edit points.
internal static class FinaleTiming
{
    public const int Impact=112;
    public const int WhiteEnd=124;
    public const int NegativeEnd=134;
    public const int Silence=148;
    public const int Explosion=160;
    public const int Finish=400;
    public const double MetalSeconds=(Explosion-Impact)/60.0;
    public const double MetalAudibleSeconds=(Silence-Impact)/60.0;
    public const double ExplosionSeconds=4.8;
}
