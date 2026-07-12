public static class GameSettings
{
    public static GameMode     Mode             = GameMode.Local;
    public static AIDifficulty Difficulty       = AIDifficulty.Medium;
    public static bool         LaunchedFromMenu = false;
    public static string       Player1Name      = "Jugador 1";
    public static string       Player2Name      = "Jugador 2";
    public static string       RoomCode         = "";
    public static int          Player1Score     = 0;
    public static int          Player2Score     = 0;

    public static void ResetScores()
    {
        Player1Score = 0;
        Player2Score = 0;
    }
}
