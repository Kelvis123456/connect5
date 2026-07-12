using System.Collections.Generic;
using UnityEngine;

public class Board
{
    public const int Size      = 10;
    public const int WinLength = 5;

    private readonly CellState[,] _grid = new CellState[Size, Size];

    public CellState GetCell(int col, int row) => _grid[col, row];

    public bool IsColumnFull(int col) => _grid[col, 0] != CellState.Empty;

    public bool IsFull()
    {
        for (int c = 0; c < Size; c++)
            if (!IsColumnFull(c)) return false;
        return true;
    }

    // Returns the row index where the piece would land (-1 if column is full).
    public int GetDropRow(int col)
    {
        for (int r = Size - 1; r >= 0; r--)
            if (_grid[col, r] == CellState.Empty) return r;
        return -1;
    }

    // Places piece and returns the row it landed on. Returns false if column is full.
    public bool PlacePiece(int col, CellState player, out int row)
    {
        row = GetDropRow(col);
        if (row < 0) return false;
        _grid[col, row] = player;
        return true;
    }

    // Checks for a win at (col, row) after placing player's piece.
    public bool CheckWin(int col, int row, CellState player, out List<Vector2Int> winCells)
    {
        // 4 axis directions; each scanned both ways from the placed piece.
        int[] dc = { 1, 0,  1,  1 };
        int[] dr = { 0, 1,  1, -1 };

        for (int d = 0; d < 4; d++)
        {
            var line = new List<Vector2Int> { new Vector2Int(col, row) };
            line.AddRange(Scan(col, row,  dc[d],  dr[d], player));
            line.AddRange(Scan(col, row, -dc[d], -dr[d], player));

            if (line.Count >= WinLength)
            {
                winCells = line;
                return true;
            }
        }

        winCells = null;
        return false;
    }

    private List<Vector2Int> Scan(int col, int row, int dc, int dr, CellState player)
    {
        var result = new List<Vector2Int>();
        int c = col + dc, r = row + dr;
        while (c >= 0 && c < Size && r >= 0 && r < Size && _grid[c, r] == player)
        {
            result.Add(new Vector2Int(c, r));
            c += dc;
            r += dr;
        }
        return result;
    }

    public Board Clone()
    {
        var b = new Board();
        System.Array.Copy(_grid, b._grid, _grid.Length);
        return b;
    }
}
