// Assets/_Scripts/Core/GameFlowManager.cs
using UnityEngine;

public static class GameFlowManager
{
    public static int P1CharacterIndex = 0;   // 0=Kael 1=Sira 2=Hemdan 3=Ramez
    public static int P2CharacterIndex = 1;
    public static int SelectedMapIndex = 0;   // 0=RiftTemple 1=FractureCage 2=ApexHelipad
    public static bool IsMultiplayer = false;
    public static int RoundsToWin = 2;   // best-of-3
    public static int P1RoundWins = 0;
    public static int P2RoundWins = 0;
    public static int RoundTimerSeconds = 99;
    public static int AIDifficulty = 1;   // 0=Easy 1=Normal 2=Hard
    public static string WinnerName = "";

    public static void ResetMatch()
    {
        P1RoundWins = 0;
        P2RoundWins = 0;
        WinnerName = "";
    }

    public static string GetCharName(int idx) => idx switch
    {
        0 => "KAEL",
        1 => "SIRA",
        2 => "HEMDAN",
        3 => "RAMEZ",
        _ => "FIGHTER"
    };
}