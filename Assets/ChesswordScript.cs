using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using System.Text.RegularExpressions;

public class ChesswordScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable[] SquareSels;
    public KMSelectable StarBtnSel;
    public KMSelectable ResetBtnSel;
    public GameObject[] DiamondObjs;
    public GameObject[] KnightObjs;
    public Material[] DiamondMats;
    public TextMesh InputText;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private static readonly string[] _possibleLetterBoards = new string[2] { "WG.JORHBEZI.MF.QDTP.AKL..XS.VYU.C.N.", "B.DQ.FPY.CX..IU.ZWLONKVT.HA.J.S.RGME" };
    private string _letterBoard;
    private int _lastDigit;
    private int _pieceCount;
    private int[] _piecePositions;
    private ChessPiece[] _chosenPieces;
    private bool[] _occupiedPositions = new bool[36];
    private List<char>[] _pieceLetters;
    private string _solutionWord;
    private int _currentPosition = 0;
    private bool _hasPlaced;
    private string _input = "";
    private bool _isAnimating;

    private static readonly string[][] _wordList = new string[3][]
    {
        new string[]{ "ARTS", "APPS", "BOAT", "BRAT", "CODE", "EGGS", "ECHO", "FAUX", "FOOL", "FREE", "GLEE", "ISLE", "KING", "LOAF", "LOVE", "LAMB", "MIKE", "MINI", "NADA", "NINE", "PILE", "POLL", "QUIZ", "RAID", "ROAD", "RIND", "SUMS", "SANS", "TREE", "THEY", "TANG", "TRAP", "VICE", "WAVE", "YANK", "ZOOS" },
        new string[]{ "ANGST", "AGONY", "ALBUM", "BRINE", "BENDS", "CRACK", "CHOKE", "DRUMS", "FIRED", "GROAN", "GRAPE", "HOUSE", "INFIX", "KARMA", "LUNGS", "LINKS", "MARES", "MORSE", "NEEDY", "OPERA", "PILOT", "PINGS", "PRESS", "PLANE", "QUEST", "REIGN", "SAUCE", "SOILS", "STAMP", "TANGO", "TARTS", "UNDER", "WOMAN", "WHERE", "YODEL" },
        new string[]{ "AGENTS", "BRAWNY", "CRYING", "DETAIL", "ENDING", "FAILED", "GHOULS", "HAMMER", "INDIGO", "JUMPER", "KEVLAR", "LOVERS", "MORGUE", "NATURE", "OCTAVE", "PEANUT", "QUARTZ", "REWARD", "SUMMON", "TRAVEL", "UNEVEN", "VICTOR", "WISHED", "YAWNED", "ZEALOT" }
    };

    private enum ChessPiece
    {
        Knight,
        Bishop,
        Rook,
        Queen,
        King
    }

    private void Start()
    {
        for (int i = 0; i < SquareSels.Length; i++)
        {
            KnightObjs[i].SetActive(false);
            SquareSels[i].OnInteract += SquarePress(i);
        }
        StarBtnSel.OnInteract += StarBtnPress;
        ResetBtnSel.OnInteract += ResetBtnPress;
        _moduleId = _moduleIdCounter++;
        _lastDigit = BombInfo.GetSerialNumber()[5] - '0';
        _letterBoard = _lastDigit % 2 == 0 ? _possibleLetterBoards[0] : _possibleLetterBoards[1];
        GenerateBoard();
    }

    private KMSelectable.OnInteractHandler SquarePress(int btn)
    {
        return delegate ()
        {
            SquareSels[btn].AddInteractionPunch(0.1f);
            if (_moduleSolved || _isAnimating)
                return false;
            if (!_hasPlaced)
            {
                Audio.PlaySoundAtTransform("Startup", transform);
                _hasPlaced = true;
                _currentPosition = btn;
                KnightObjs[_currentPosition].SetActive(true);
                return false;
            }
            var moves = GetKnightPositions(_currentPosition);
            if (!moves.Contains(btn))
            {
                Module.HandleStrike();
                Debug.LogFormat("[Chessword #{0}] Attempted to move from {1} to {2}, which is not a valid knight movement. Strike.", _moduleId, GetCoord(_currentPosition), GetCoord(btn));
                return false;
            }
            Audio.PlaySoundAtTransform("KnightMove", transform);
            KnightObjs[_currentPosition].SetActive(false);
            _currentPosition = btn;
            KnightObjs[_currentPosition].SetActive(true);
            return false;
        };
    }

    private bool StarBtnPress()
    {
        StarBtnSel.AddInteractionPunch(0.5f);
        if (_moduleSolved || _isAnimating)
            return false;
        Audio.PlaySoundAtTransform("Check", transform);
        if (_letterBoard[_currentPosition] == '.' || !_hasPlaced)
        {
            Module.HandleStrike();
            Debug.LogFormat("[Chessword #{0}] Pressed the star button when the knight was not on a letter. Strike.", _moduleId);
            return false;
        }
        _input += _letterBoard[_currentPosition];
        InputText.text += "-";
        if (_input.Length == _pieceCount)
            StartCoroutine(CheckAnswer());
        return false;
    }

    private bool ResetBtnPress()
    {
        ResetBtnSel.AddInteractionPunch(0.5f);
        if (_moduleSolved || _isAnimating)
            return false;
        Audio.PlaySoundAtTransform("GameOver", transform);
        _input = "";
        InputText.text = "";
        return false;
    }

    private void GenerateBoard()
    {
        int attempts = 0;
        _pieceCount = Rnd.Range(4, 7);
        var possibleWords =
            _pieceCount == 4 ? Enumerable.Range(0, _wordList[0].Length).ToArray().Shuffle().Take(4).Select(i => _wordList[0][i]).ToArray() :
            _pieceCount == 5 ? Enumerable.Range(0, _wordList[1].Length).ToArray().Shuffle().Take(4).Select(i => _wordList[1][i]).ToArray() :
            Enumerable.Range(0, _wordList[2].Length).ToArray().Shuffle().Take(5).Select(i => _wordList[2][i]).ToArray();
        tryAgain:
        attempts++;
        _pieceLetters = new List<char>[_pieceCount].Select(i => new List<char>()).ToArray();
        _piecePositions = Enumerable.Range(0, 36).ToArray().Shuffle().Take(_pieceCount).ToArray();
        Array.Sort(_piecePositions);
        for (int i = 0; i < 36; i++)
            _occupiedPositions[i] = _piecePositions.Contains(i);
        _chosenPieces = new ChessPiece[_pieceCount].Select(i => (ChessPiece)Rnd.Range(0, 5)).ToArray();
        if (_chosenPieces.Distinct().Count() < 3)
            goto tryAgain;
        for (int i = 0; i < _pieceCount; i++)
        {
            _pieceLetters[i] = GetPieceLetters(_chosenPieces[i], _piecePositions[i]);
            if (_pieceLetters[i].Count == 0)
                goto tryAgain;
        }
        var validWords = CheckForWords();
        if (validWords.Count != 1)
            goto tryAgain;
        _solutionWord = validWords[0];
        if (!possibleWords.Contains(_solutionWord))
            goto tryAgain;
        var list = new List<string>();
        for (int i = 0; i < _pieceCount; i++)
            list.Add(_chosenPieces[i] + " at " + GetCoord(_piecePositions[i]));
        var list2 = new List<string>();
        for (int i = 0; i < _pieceCount; i++)
            list2.Add(_pieceLetters[i].Join(""));
        for (int i = 0; i < 36; i++)
        {
            if (!_piecePositions.Contains(i))
                DiamondObjs[i].SetActive(false);
            else
            {
                DiamondObjs[i].SetActive(true);
                DiamondObjs[i].GetComponent<MeshRenderer>().material = DiamondMats[(int)_chosenPieces[Array.IndexOf(_piecePositions, i)]];
            }
        }
        Debug.LogFormat("[Chessword #{0}] The last digit of the serial number is {1}. Using the {2} board.", _moduleId, _lastDigit % 2 == 0 ? "even" : "odd", _lastDigit % 2 == 0 ? "left" : "right");
        Debug.LogFormat("[Chessword #{0}] Pieces: {1}.", _moduleId, list.Join(", "));
        Debug.LogFormat("[Chessword #{0}] The letters for each piece: {1}.", _moduleId, list2.Join(", "));
        Debug.LogFormat("[Chessword #{0}] The solution word is {1}.", _moduleId, _solutionWord);
        Debug.LogFormat("<Chessword #{0}> Attempts to generate a valid board, with words {1}: {2}.", _moduleId, possibleWords.Join(" "), attempts);
    }

    private string GetCoord(int num)
    {
        return "ABCDEF"[num % 6].ToString() + "123456"[num / 6].ToString();
    }

    private List<char> GetPieceLetters(ChessPiece piece, int pos)
    {
        var list = new List<char>();
        if (piece == ChessPiece.Knight)
        {
            if (pos % 6 > 0 && pos / 6 > 1 && !_occupiedPositions[pos - 13] && _letterBoard[pos - 13] != '.')
                list.Add(_letterBoard[pos - 13]);
            if (pos % 6 > 1 && pos / 6 > 0 && !_occupiedPositions[pos - 8] && _letterBoard[pos - 8] != '.')
                list.Add(_letterBoard[pos - 8]);
            if (pos % 6 > 0 && pos / 6 < 4 && !_occupiedPositions[pos + 11] && _letterBoard[pos + 11] != '.')
                list.Add(_letterBoard[pos + 11]);
            if (pos % 6 > 1 && pos / 6 < 5 && !_occupiedPositions[pos + 4] && _letterBoard[pos + 4] != '.')
                list.Add(_letterBoard[pos + 4]);
            if (pos % 6 < 5 && pos / 6 > 1 && !_occupiedPositions[pos - 11] && _letterBoard[pos - 11] != '.')
                list.Add(_letterBoard[pos - 11]);
            if (pos % 6 < 4 && pos / 6 > 0 && !_occupiedPositions[pos - 4] && _letterBoard[pos - 4] != '.')
                list.Add(_letterBoard[pos - 4]);
            if (pos % 6 < 5 && pos / 6 < 4 && !_occupiedPositions[pos + 13] && _letterBoard[pos + 13] != '.')
                list.Add(_letterBoard[pos + 13]);
            if (pos % 6 < 4 && pos / 6 < 5 && !_occupiedPositions[pos + 8] && _letterBoard[pos + 8] != '.')
                list.Add(_letterBoard[pos + 8]);
            return list;
        }
        if (piece == ChessPiece.King)
        {
            if (pos % 6 > 0 && !_occupiedPositions[pos - 1] && _letterBoard[pos - 1] != '.')
                list.Add(_letterBoard[pos - 1]);
            if (pos % 6 < 5 && !_occupiedPositions[pos + 1] && _letterBoard[pos + 1] != '.')
                list.Add(_letterBoard[pos + 1]);
            if (pos / 6 > 0 && !_occupiedPositions[pos - 6] && _letterBoard[pos - 6] != '.')
                list.Add(_letterBoard[pos - 6]);
            if (pos / 6 < 5 && !_occupiedPositions[pos + 6] && _letterBoard[pos + 6] != '.')
                list.Add(_letterBoard[pos + 6]);
            if (pos % 6 > 0 && pos / 6 > 0 && !_occupiedPositions[pos - 7] && _letterBoard[pos - 7] != '.')
                list.Add(_letterBoard[pos - 7]);
            if (pos % 6 < 5 && pos / 6 > 0 && !_occupiedPositions[pos - 5] && _letterBoard[pos - 5] != '.')
                list.Add(_letterBoard[pos - 5]);
            if (pos % 6 > 0 && pos / 6 < 5 && !_occupiedPositions[pos + 5] && _letterBoard[pos + 5] != '.')
                list.Add(_letterBoard[pos + 5]);
            if (pos % 6 < 5 && pos / 6 < 5 && !_occupiedPositions[pos + 7] && _letterBoard[pos + 7] != '.')
                list.Add(_letterBoard[pos + 7]);
            return list;
        }
        if (piece == ChessPiece.Bishop || piece == ChessPiece.Queen)
        {
            var oldPos = pos;
            while (pos % 6 > 0 && pos / 6 > 0)
            {
                if (!_occupiedPositions[pos - 7])
                {
                    if (_letterBoard[pos - 7] != '.')
                        list.Add(_letterBoard[pos - 7]);
                    pos = pos - 7;
                }
                else
                    pos = 0;
            }
            pos = oldPos;
            while (pos % 6 < 5 && pos / 6 > 0)
            {
                if (!_occupiedPositions[pos - 5])
                {
                    if (_letterBoard[pos - 5] != '.')
                        list.Add(_letterBoard[pos - 5]);
                    pos = pos - 5;
                }
                else
                    pos = 5;
            }
            pos = oldPos;
            while (pos % 6 > 0 && pos / 6 < 5)
            {
                if (!_occupiedPositions[pos + 5])
                {
                    if (_letterBoard[pos + 5] != '.')
                        list.Add(_letterBoard[pos + 5]);
                    pos = pos + 5;
                }
                else
                    pos = 0;
            }
            pos = oldPos;
            while (pos % 6 < 5 && pos / 6 < 5)
            {
                if (!_occupiedPositions[pos + 7])
                {
                    if (_letterBoard[pos + 7] != '.')
                        list.Add(_letterBoard[pos + 7]);
                    pos = pos + 7;
                }
                else
                    pos = 5;
            }
            pos = oldPos;
        }
        if (piece == ChessPiece.Rook || piece == ChessPiece.Queen)
        {
            var oldPos = pos;
            while (pos % 6 > 0)
            {
                if (!_occupiedPositions[pos - 1])
                {
                    if (_letterBoard[pos - 1] != '.')
                        list.Add(_letterBoard[pos - 1]);
                    pos = pos - 1;
                }
                else
                    pos = 0;
            }
            pos = oldPos;
            while (pos % 6 < 5)
            {
                if (!_occupiedPositions[pos + 1])
                {
                    if (_letterBoard[pos + 1] != '.')
                        list.Add(_letterBoard[pos + 1]);
                    pos = pos + 1;
                }
                else
                    pos = 5;
            }
            pos = oldPos;
            while (pos / 6 > 0)
            {
                if (!_occupiedPositions[pos - 6])
                {
                    if (_letterBoard[pos - 6] != '.')
                        list.Add(_letterBoard[pos - 6]);
                    pos = pos - 6;
                }
                else
                    pos = 0;
            }
            pos = oldPos;
            while (pos / 6 < 5)
            {
                if (!_occupiedPositions[pos + 6])
                {
                    if (_letterBoard[pos + 6] != '.')
                        list.Add(_letterBoard[pos + 6]);
                    pos = pos + 6;
                }
                else
                    pos = 30;
            }
            pos = oldPos;
        }
        return list;
    }

    private List<string> CheckForWords()
    {
        var list = new List<string>();
        if (_pieceCount == 4)
        {
            foreach (var word in _wordList[0])
            {
                if (_pieceLetters[0].Contains(word[0]))
                    list.Add(word);
                if (!_pieceLetters[1].Contains(word[1]) || !_pieceLetters[2].Contains(word[2]) || !_pieceLetters[3].Contains(word[3]))
                    list.Remove(word);
            }
        }
        else if (_pieceCount == 5)
        {
            foreach (var word in _wordList[1])
            {
                if (_pieceLetters[0].Contains(word[0]))
                    list.Add(word);
                if (!_pieceLetters[1].Contains(word[1]) || !_pieceLetters[2].Contains(word[2]) || !_pieceLetters[3].Contains(word[3]) || !_pieceLetters[4].Contains(word[4]))
                    list.Remove(word);
            }
        }
        else if (_pieceCount == 6)
        {
            foreach (var word in _wordList[2])
            {
                if (_pieceLetters[0].Contains(word[0]))
                    list.Add(word);
                if (!_pieceLetters[1].Contains(word[1]) || !_pieceLetters[2].Contains(word[2]) || !_pieceLetters[3].Contains(word[3]) || !_pieceLetters[4].Contains(word[4]) || !_pieceLetters[5].Contains(word[5]))
                    list.Remove(word);
            }
        }
        return list;
    }

    private List<int> GetKnightPositions(int pos)
    {
        var list = new List<int>();
        if (pos % 6 > 0 && pos / 6 > 1)
            list.Add(pos - 13);
        if (pos % 6 > 1 && pos / 6 > 0)
            list.Add(pos - 8);
        if (pos % 6 > 0 && pos / 6 < 4)
            list.Add(pos + 11);
        if (pos % 6 > 1 && pos / 6 < 5)
            list.Add(pos + 4);
        if (pos % 6 < 5 && pos / 6 > 1)
            list.Add(pos - 11);
        if (pos % 6 < 4 && pos / 6 > 0)
            list.Add(pos - 4);
        if (pos % 6 < 5 && pos / 6 < 4)
            list.Add(pos + 13);
        if (pos % 6 < 4 && pos / 6 < 5)
            list.Add(pos + 8);
        return list;
    }

    private IEnumerator CheckAnswer()
    {
        bool correct = _input == _solutionWord;
        _isAnimating = true;
        for (int i = 0; i < _input.Length; i++)
        {
            Audio.PlaySoundAtTransform("KnightMove", transform);
            InputText.text = _input.Substring(0, i) + InputText.text.Substring(i);
            yield return new WaitForSeconds(0.3f);
        }
        Audio.PlaySoundAtTransform("KnightMove", transform);
        InputText.text = _input;
        yield return new WaitForSeconds(0.5f);
        if (correct)
        {
            Audio.PlaySoundAtTransform("GameOver", transform);
            _moduleSolved = true;
            Module.HandlePass();
            Debug.LogFormat("[Chessword #{0}] Correctly submitted {1}. Module solved.", _moduleId, _input);
            InputText.color = new Color32(0, 255, 0, 255);
            yield break;
        }
        Module.HandleStrike();
        Debug.LogFormat("[Chessword #{0}] Inorrectly submitted {1}. Strike.", _moduleId, _input);
        InputText.color = new Color32(255, 0, 0, 255);
        yield return new WaitForSeconds(0.6f);
        for (int i = _input.Length - 1; i >= 0; i--)
        {
            InputText.text = InputText.text.Substring(0, i);
            yield return new WaitForSeconds(0.2f);
        }
        InputText.color = new Color32(255, 255, 255, 255);
        KnightObjs[_currentPosition].SetActive(false);
        _isAnimating = false;
        _hasPlaced = false;
        _input = "";
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} a1 b2 c3 [Press cells a1, b2, c3.] | !{0} star [Press the star button.] | !{0} reset [Press the reset button.] | Commands can be chained.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var parameters = command.ToUpperInvariant().Split(' ');
        var list = new List<KMSelectable>();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i] == "RESET")
                list.Add(ResetBtnSel);
            else if (parameters[i] == "STAR")
                list.Add(StarBtnSel);
            else
            {
                if (parameters[i].Length != 2)
                {
                    yield return "sendtochaterror " + parameters[i] + " is not a valid command!";
                    yield break;
                }
                if (parameters[i][0] >= 'A' && parameters[i][0] <= 'F' && parameters[i][1] >= '1' && parameters[i][1] <= '6')
                    list.Add(SquareSels[parameters[i][0] - 'A' + ((parameters[i][1] - '1') * 6)]);
                else
                {
                    yield return "sendtochaterror " + parameters[i] + " is not a valid command!";
                    yield break;
                }
            }
        }
        yield return null;
        yield return "solve";
        yield return "strike";
        for (int i = 0; i < list.Count; i++)
        {
            list[i].OnInteract();
            yield return new WaitForSeconds(0.2f);
            if (_isAnimating)
                yield break;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        int start = 0;
        for (int i = 0; i < _input.Length; i++)
        {
            if (_input[i] == _solutionWord[i])
                start++;
            else
            {
                ResetBtnSel.OnInteract();
                start = 0;
                i = _input.Length;
                yield return new WaitForSeconds(0.2f);
            }
        }
        if (!_hasPlaced)
        {
            SquareSels[_letterBoard.IndexOf(_solutionWord[0])].OnInteract();
            yield return new WaitForSeconds(0.2f);
            StarBtnSel.OnInteract();
            start = 1;
            yield return new WaitForSeconds(0.2f);
        }
        for (int i = start; i < _solutionWord.Length; i++)
        {
            var goal = _letterBoard.IndexOf(_solutionWord[i]);
            if (goal == -1)
                throw new NotImplementedException("You fucked up");
            var path = FindPath(_currentPosition, goal);
            for (int j = 0; j < path.Length; j++)
            {
                SquareSels[path[j]].OnInteract();
                yield return new WaitForSeconds(0.2f);
            }
            StarBtnSel.OnInteract();
            yield return new WaitForSeconds(0.2f);
        }
        while (!_moduleSolved)
            yield return true;
    }

    struct QueueItem
    {
        public int Cell;
        public int Parent;
        public QueueItem(int cell, int parent)
        {
            Cell = cell;
            Parent = parent;
        }
    }

    private int[] FindPath(int a, int b)
    {
        var visited = new Dictionary<int, QueueItem>();
        var q = new Queue<QueueItem>();
        var current = a;
        var sol = b;
        q.Enqueue(new QueueItem(current, -1));
        while (q.Count > 0)
        {
            var qi = q.Dequeue();
            if (visited.ContainsKey(qi.Cell))
                continue;
            visited[qi.Cell] = qi;
            if (qi.Cell == sol)
                break;
            if (qi.Cell % 6 > 0 && qi.Cell / 6 > 1)
                q.Enqueue(new QueueItem(qi.Cell - 13, qi.Cell));
            if (qi.Cell % 6 > 1 && qi.Cell / 6 > 0)
                q.Enqueue(new QueueItem(qi.Cell - 8, qi.Cell));
            if (qi.Cell % 6 > 0 && qi.Cell / 6 < 4)
                q.Enqueue(new QueueItem(qi.Cell + 11, qi.Cell));
            if (qi.Cell % 6 > 1 && qi.Cell / 6 < 5)
                q.Enqueue(new QueueItem(qi.Cell + 4, qi.Cell));
            if (qi.Cell % 6 < 5 && qi.Cell / 6 > 1)
                q.Enqueue(new QueueItem(qi.Cell - 11, qi.Cell));
            if (qi.Cell % 6 < 4 && qi.Cell / 6 > 0)
                q.Enqueue(new QueueItem(qi.Cell - 4, qi.Cell));
            if (qi.Cell % 6 < 5 && qi.Cell / 6 < 4)
                q.Enqueue(new QueueItem(qi.Cell + 13, qi.Cell));
            if (qi.Cell % 6 < 4 && qi.Cell / 6 < 5)
                q.Enqueue(new QueueItem(qi.Cell + 8, qi.Cell));
        }
        var r = sol;
        var path = new List<int>();
        while (true)
        {
            var nr = visited[r];
            if (nr.Parent == -1)
                break;
            path.Add(nr.Cell);
            r = nr.Parent;
        }
        path.Reverse();
        return path.ToArray();
    }
}
