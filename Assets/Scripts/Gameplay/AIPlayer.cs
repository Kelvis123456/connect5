using System.Collections.Generic;
using UnityEngine;

public static class AIPlayer
{
    public static int GetMove(Board board, CellState aiPlayer, AIDifficulty difficulty)
    {
        CellState opponent = aiPlayer == CellState.Player1 ? CellState.Player2 : CellState.Player1;

        var validCols = new List<int>();
        for (int c = 0; c < Board.Size; c++)
            if (!board.IsColumnFull(c)) validCols.Add(c);

        if (validCols.Count == 0) return -1;

        return difficulty switch
        {
            AIDifficulty.Easy   => EasyMove(validCols),
            AIDifficulty.Medium => MediumMove(board, validCols, aiPlayer, opponent),
            AIDifficulty.Hard   => HardMove(board, validCols, aiPlayer, opponent),
            _                   => EasyMove(validCols),
        };
    }

    // ── Easy: random valid column ───────────────────────────────────────────
    private static int EasyMove(List<int> cols)
        => cols[Random.Range(0, cols.Count)];

    // ── Medium: win > block > random ───────────────────────────────────────
    private static int MediumMove(Board board, List<int> cols, CellState ai, CellState opp)
    {
        int win = FindWin(board, cols, ai);
        if (win >= 0) return win;

        int block = FindWin(board, cols, opp);
        if (block >= 0) return block;

        return EasyMove(cols);
    }

    // ── Hard: win > block > heuristic score ────────────────────────────────
    private static int HardMove(Board board, List<int> cols, CellState ai, CellState opp)
    {
        int win = FindWin(board, cols, ai);
        if (win >= 0) return win;

        int block = FindWin(board, cols, opp);
        if (block >= 0) return block;

        int bestCol   = cols[cols.Count / 2]; // prefer centre as tie-break
        int bestScore = int.MinValue;

        foreach (int c in cols)
        {
            var clone = board.Clone();
            clone.PlacePiece(c, ai, out _);
            int score = EvaluateBoard(clone, ai, opp);
            if (score > bestScore)
            {
                bestScore = score;
                bestCol   = c;
            }
        }

        return bestCol;
    }

    // Returns the first column where `player` can win in one move, or -1.
    private static int FindWin(Board board, List<int> cols, CellState player)
    {
        foreach (int c in cols)
        {
            int row = board.GetDropRow(c);
            if (row < 0) continue;
            var clone = board.Clone();
            clone.PlacePiece(c, player, out _);
            if (clone.CheckWin(c, row, player, out _)) return c;
        }
        return -1;
    }

    // Slides a window of WinLength across all rows, columns and diagonals.
    private static int EvaluateBoard(Board board, CellState ai, CellState opp)
    {
        int score = 0;
        // dc[d,0] = column delta, dc[d,1] = row delta
        int[,] dir = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };

        for (int startC = 0; startC < Board.Size; startC++)
        {
            for (int startR = 0; startR < Board.Size; startR++)
            {
                for (int d = 0; d < 4; d++)
                {
                    var window = new CellState[Board.WinLength];
                    bool valid = true;

                    for (int i = 0; i < Board.WinLength; i++)
                    {
                        int c = startC + dir[d, 0] * i;
                        int r = startR + dir[d, 1] * i;
                        if (c < 0 || c >= Board.Size || r < 0 || r >= Board.Size)
                        {
                            valid = false;
                            break;
                        }
                        window[i] = board.GetCell(c, r);
                    }

                    if (valid) score += ScoreWindow(window, ai, opp);
                }
            }
        }

        return score;
    }

    private static int ScoreWindow(CellState[] window, CellState ai, CellState opp)
    {
        int aiCnt = 0, oppCnt = 0;
        foreach (var cell in window)
        {
            if      (cell == ai)  aiCnt++;
            else if (cell == opp) oppCnt++;
        }

        if (aiCnt > 0 && oppCnt > 0) return 0; // blocked line — no value

        if (aiCnt  > 0) return aiCnt  switch { 4 => 1000, 3 => 100, 2 => 10, _ => 1 };
        if (oppCnt > 0) return oppCnt switch { 4 => -2000, 3 => -200, 2 => -20, _ => 0 };
        return 0;
    }
}
