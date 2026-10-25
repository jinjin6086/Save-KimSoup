namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    // Restore the authored pre-remaster notes and frequency sweeps, rather
    // than pitching every cue down by a guessed number of semitones. These
    // original generators remain deterministic 22,050 Hz mono PCM. The
    // shared clarity pass still runs afterwards, without resampling them.
    private void ApplyOriginalSkillSounds()
    {
        clips["round"]=MakeRoundedReward();
        clips["reward"]=MakeRoundedReward();
        clips["bossdefeat"]=MakeBossDefeat();

        clips["absorb"]=MakeSoftAbility(.32,95,228,.45,.17,411);
        clips["eat"]=MakeSoftAbility(.22,260,96,.62,.18,412);
        clips["dodge"]=MakeWhoosh();
        clips["clone"]=MakePlayerRelease(.56,.24);
        clips["reverse"]=MakePlayerRelease(.43,.28);
        clips["furiosostart"]=MakeEchoStart();
        clips["echoflight"]=MakeEchoFlight();
        clips["echohit"]=MakeEchoImpact();
        clips["focusenter"]=MakeFocusEntry();
        clips["cinderstart"]=MakeCinderSound(0);
        clips["cinderthrow"]=MakeCinderSound(2);
        clips["cinderhit"]=MakeCinderSound(3);
        clips["cinderbrand"]=MakeCinderSound(4);
        clips["inhale"]=MakeInhale();

        clips["phase"]=MakeBeam(.7,true);
        clips["phasebreak"]=MakeBeam(.7,false);
        clips["gravity"]=Make(.55,160,760,.25,.22);
        clips["counterbeam"]=MakeGrandBeam(.55,false,true);
        clips["meteorhit"]=Make(.16,130,43,.85,.22);
        clips["menhera"]=MakeBeam(.8,false);
        clips["dashwarn"]=Make(.07,1550,1700,.01,.17);
        clips["dash"]=Make(.24,900,85,.75,.24);
        clips["charge"]=MakeSoftAbility(.30,135,350,.15,.14,422);
        clips["beat"]=Make(.08,780,780,.02,.12);
        clips["final"]=Make(.15,1150,1150,.10,.17);
        clips["beat0"]=Make(.07,520,440,.06,.16);
        clips["beat1"]=Make(.07,660,540,.06,.16);
        clips["beat2"]=Make(.09,880,680,.08,.19);
        clips["beam"]=MakeGrandBeam(.88,false);
        clips["volley"]=Make(.12,180,80,.32,.15);
        for(int frames=26;frames<=62;frames++)
            clips["lasercharge"+frames]=MakeGrandBeam(frames/60.0,true);
    }
}
