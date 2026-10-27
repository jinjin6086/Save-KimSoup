namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    private const int SoundVoiceLimit=12;
    private static int SoundPriority(string key)
    {
        if(key.StartsWith("talk",StringComparison.Ordinal))return 2;
        if(key.StartsWith("clash",StringComparison.Ordinal))return key=="clashtap"?5:7;
        if(key.StartsWith("lasercharge",StringComparison.Ordinal))return 6;
        return key switch
        {
            "beam" or "counterbeam" or "dashwarn" or "timestopstart" or "timestoprelease"=>6,
            "perfect" or "parry" or "echohit" or "hurt" or "bossdefeat" or "down"=>5,
            "dash" or "reverse" or "furiosostart" or "cinderstart" or "focusenter"=>4,
            "slidebody" or "step00" or "step01" or "step10" or "step11" or "step20" or "step21"=>1,
            _=>3
        };
    }
    private static float SoundGain(string key)
    {
        if(key.StartsWith("talk",StringComparison.Ordinal))return .43f;
        if(key.StartsWith("step",StringComparison.Ordinal))return .26f;
        return key switch
        {
            "slidebody"=>.28f,"slidein" or "slideout"=>.65f,
            "clashtap"=>.70f,"clashsustain"=>.44f,"clashheart"=>.54f,
            "clashexplosion" or "clashbreak"=>.95f,
            "beam" or "counterbeam"=>.76f,
            "dashwarn" or "perfect" or "parry"=>.88f,
            "echoflight"=>.52f,
            _=>.78f
        };
    }
    private static int SoundRepeatInterval(string key)
    {
        if(key.StartsWith("talk",StringComparison.Ordinal))return 65;
        if(key.StartsWith("lasercharge",StringComparison.Ordinal))return 35;
        if(key.StartsWith("step",StringComparison.Ordinal))return 55;
        return key switch
        {
            "impact" or "crash" or "volley" or "absorb" or "eat" or "meteorhit"=>40,
            "parry" or "perfect" or "clashtap"=>24,
            "beam" or "counterbeam" or "dashwarn" or "dash"=>30,
            "slidebody"=>70,
            _=>0
        };
    }
    // A footstep never steals a warning, reflected beam or cinematic accent.
    // The larger bounded pool permits their natural decay tails to overlap.
    private bool ReserveSoundVoice(string key)
    {
        if(voices.Count<SoundVoiceLimit)return true;
        int candidate=-1,priority=SoundPriority(key);
        for(int i=0;i<voices.Count;i++)
            if(SoundPriority(voices[i].Key)<priority&&
                (candidate<0||SoundPriority(voices[i].Key)<SoundPriority(voices[candidate].Key)))candidate=i;
        if(candidate<0)return false;
        voices[candidate].Dispose();voices.RemoveAt(candidate);return true;
    }
}
