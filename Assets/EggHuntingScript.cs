using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rng = UnityEngine.Random;

public class EggHuntingScript : MonoBehaviour
{
    public Texture2D[] SpriteTextures;
    public Renderer[] SpriteHolders;
    public KMSelectable[] Buttons;

    private int _id = ++_idc;
    private static int _idc;

    private int _currentStage, _currentShownStage, _currentMetal = 1;
    private bool _inputting, _enabled;
    private int[] _currentInput = new int[4];

    private readonly List<Frame> _stages = new List<Frame>();
    private readonly int[] _placings = new int[4];
    private string[] _ignoredModules;
    private KMBombInfo _info;
    private KMBombInfo Info
    {
        get { return _info; }
        set { if(_info == null) _info = value; }
    }

    public void Awake()
    {
        foreach(Renderer r in SpriteHolders)
            r.enabled = false;
    }

    public void Start()
    {
        Info = GetComponent<KMBombInfo>();
        GetComponent<KMBombModule>().OnActivate += () => _enabled = true;

        GetComponent<KMAudio>().PlaySoundAtTransform("Birdsong", transform);

        StartCoroutine(WatchSolves());

        Buttons[0].OnInteract += FFLeft;
        Buttons[1].OnInteract += FFRight;

        Buttons[2].OnInteract += Metal;
        for(int i = 0; i < 4; ++i)
        {
            int j = i;
            Buttons[i + 3].OnInteract += () => { Basket(j); return false; };
        }

        // Generate Race
        List<string> modules = Info.GetSolvableModuleNames();
        _ignoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Egg Hunt", new string[] { "Egg Hunt" });

        int stages = modules.Count(nm => !_ignoredModules.Contains(nm));
        if(stages < 5)
        {
            stages = 5;
            _currentStage = 5;
        }

        List<Egg> frameSpecials = new List<Egg>(stages);
        List<Egg> frameSpecialColors = new List<Egg>(stages);
        while(frameSpecials.Count < stages)
            frameSpecials.AddRange(new Egg[] { Egg.Special, Egg.Special, Egg.Special, Egg.Magnet, Egg.Foiler }.Shuffle());
        while(frameSpecialColors.Count < stages)
            frameSpecialColors.AddRange(new Egg[] { Egg.Red, Egg.Green, Egg.Blue, Egg.Yellow }.Shuffle());

        int[] positions = new int[4] { 0, 2, 6, 8 }.Shuffle();

        _stages.Add(new Frame(
            new EggData() { Row = positions[0] / 3, Col = positions[0] % 3, Egg = Egg.Red | Egg.Basket },
            new EggData() { Row = positions[1] / 3, Col = positions[1] % 3, Egg = Egg.Green | Egg.Basket },
            new EggData() { Row = positions[2] / 3, Col = positions[2] % 3, Egg = Egg.Blue | Egg.Basket },
            new EggData() { Row = positions[3] / 3, Col = positions[3] % 3, Egg = Egg.Yellow | Egg.Basket }
        ));

        int[] goals = positions.ToArray();
        int[] progress = new int[4];
        int[] nextFrame = new int[] { 1, 1, 1, 1 };
        int[] speedUp = new int[4];
        int nextPlace = 1;
        int time = 0;

        while(nextFrame.Any(next => next <= stages + 1))
        {
            bool specialAvailable = true;
            bool foil = false;
            bool placeTaken = false;
            int magnet = -1;
            bool[] grabbed = new bool[4];

            foreach(int h in Enumerable.Range(0, 4)
                .Where(i => nextFrame[i] <= stages + 1 && TimeFromTo(positions[i], goals[i]) * (speedUp[i] == 0 ? 2 : 1) <= progress[i])
                .OrderBy(i => nextFrame[i]).ThenBy(i => Rng.value))
            {
                grabbed[h] = true;

                if(nextFrame[h] == stages + 1)
                {
                    _placings[h] = nextPlace;
                    placeTaken = true;
                    nextFrame[h]++;
                    continue;
                }

                while(nextFrame[h] >= _stages.Count)
                    _stages.Add(new Frame());

                int[] used = _stages[nextFrame[h]].GetEggs().Select(d => d.Row * 3 + d.Col).ToArray();
                List<int> open = Enumerable.Range(0, 9).ToList();
                open.RemoveAll(p => used.Contains(p));
                open.Remove(goals[h]);
                int pos = open.PickRandom();

                if(speedUp[h] != 0)
                    --speedUp[h];

                Egg egg = (Egg)(1 << h);
                if(specialAvailable && nextFrame[h] > 1 && egg == frameSpecialColors[nextFrame[h] - 1])
                {
                    Egg special = frameSpecials[nextFrame[h] - 2];
                    EggData data = _stages[nextFrame[h] - 1].GetEggs().FirstOrDefault(d => (d.Egg & (Egg)(1 << h)) != Egg.None);
                    data.Egg |= special;
                    _stages[nextFrame[h] - 1].AddEgg(data);
                    specialAvailable = false;

                    if(special == Egg.Special)
                        speedUp[h] += 3;
                    if(special == Egg.Magnet)
                        magnet = goals[h];
                    if(special == Egg.Foiler)
                        foil = true;
                }
                _stages[nextFrame[h]].AddEgg(egg, pos / 3, pos % 3);

                positions[h] = goals[h];
                goals[h] = pos;
                progress[h] = 0;

                nextFrame[h]++;
            }

            if(magnet != -1)
            {
                positions = Enumerable.Repeat(magnet, 4).ToArray();
                progress = Enumerable.Repeat(0, 4).ToArray();
            }

            if(foil)
                for(int h = 0; h < 4; ++h)
                    progress[h] = -Math.Abs(progress[h]);

            for(int h = 0; h < 4; ++h)
                ++progress[h];

            if(time == 0)
                Debug.LogFormat("[Egg Hunt #{0}]: At time 0, all hunters pick up their baskets and begin.", _id);
            else
                for(int i = 0; i < 4; ++i)
                    if(grabbed[i])
                        Debug.LogFormat("[Egg Hunt #{0}]: At time {1}, {2} grabbed egg #{3}.", _id, time, Name(i), nextFrame[i] - 2);

            if(placeTaken)
                ++nextPlace;

            time++;
        }

        string log = _stages.Select(f => f.ToString()).Join("\n[Egg Hunt #{0}]: ");

        Debug.LogFormat("[Egg Hunt #{0}]: The stages are as follows:\n[Egg Hunt #{0}]: " + log, _id);
        Debug.LogFormat("[Egg Hunt #{0}]: The awards should be: Red: {1} Green: {2} Blue: {3} Yellow: {4}", _id, _placings[0], _placings[1], _placings[2], _placings[3]);
        ShowStage(0);
    }

    private string Name(int i)
    {
        switch(i)
        {
            case 0:
                return "Ruby";
            case 1:
                return "Vera";
            case 2:
                return "Blake";
            case 3:
                return "Jane";
        }
        return "Nobody";
    }

    private IEnumerator WatchSolves()
    {
        yield return null;
        while(_currentStage < _stages.Count - 1)
        {
            yield return new WaitForSeconds(0.2f);
            int c = Info.GetSolvedModuleNames().Count(n => !_ignoredModules.Contains(n));
            if(c > _currentStage)
            {
                _currentStage = c;
                ShowStage(_currentStage);
                GetComponent<KMAudio>().PlaySoundAtTransform("Birdsong", transform);
            }
        }
    }

    private bool FFLeft()
    {
        if(!_enabled)
            return false;

        if(_currentShownStage != 0)
            ShowStage(_currentShownStage - 1);
        return false;
    }

    private bool FFRight()
    {
        if(!_enabled)
            return false;

        if(_currentShownStage < _currentStage)
            ShowStage(_currentShownStage + 1);
        else if(_currentShownStage == _currentStage && _currentStage == _stages.Count - 1)
            InputMode();
        return false;
    }

    private bool Metal()
    {
        if(!_enabled || !_inputting || _currentShownStage != _stages.Count)
            return false;

        ++_currentMetal;
        if(_currentMetal == 5)
            _currentMetal = 1;
        ShowMetal();

        return false;
    }

    private void Basket(int j)
    {
        if(!_enabled || !_inputting || _currentShownStage != _stages.Count)
            return;

        _currentInput[j] = _currentMetal;
        Buttons[j + 3].GetComponent<Renderer>().material.mainTexture = MetalTexture(_currentMetal);

        if(_currentInput.All(i => i != 0))
            CheckAnswer();
    }

    private void CheckAnswer()
    {
        if(_currentInput.SequenceEqual(_placings))
        {
            _enabled = false;
            Buttons[0].gameObject.SetActive(false);
            Buttons[1].gameObject.SetActive(false);
            Buttons[2].transform.parent.gameObject.SetActive(false);
            Debug.LogFormat("[Egg Hunt #{0}]: Module correggctly solved!", _id);
            GetComponent<KMBombModule>().HandlePass();
            StartCoroutine(PlaySolve());
        }
        else
        {
            Debug.LogFormat("[Egg Hunt #{0}]: Incorreggct. You submitted: Red: {1} Green: {2} Blue: {3} Yellow: {4}", _id, _currentInput[0], _currentInput[1], _currentInput[2], _currentInput[3]);
            GetComponent<KMBombModule>().HandleStrike();
            ShowStage(0);
        }
    }

    private IEnumerator PlaySolve()
    {
        GetComponent<KMAudio>().PlaySoundAtTransform("Yay", transform);
        yield return new WaitForSeconds(4.133f);
        GetComponent<KMAudio>().PlaySoundAtTransform("Cheer", transform);
    }

    private void ShowMode()
    {
        _inputting = false;
        Buttons[2].transform.parent.gameObject.SetActive(false);
        _currentInput = new int[4];
        _currentMetal = 1;
    }

    private void InputMode()
    {
        _inputting = true;
        _currentShownStage = _stages.Count;
        Buttons[0].GetComponent<Renderer>().material.color = new Color(0.5f, 0.5f, 0.5f);
        Buttons[1].GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0.5f);

        foreach(Renderer r in SpriteHolders)
            r.enabled = false;

        Buttons[2].transform.parent.gameObject.SetActive(true);
        for(int i = 3; i < 7; ++i)
            Buttons[i].GetComponent<Renderer>().material.mainTexture = SpriteTextures[i + 13];

        ShowMetal();
    }

    private void ShowMetal()
    {
        Buttons[2].GetComponent<Renderer>().material.mainTexture = MetalTexture(_currentMetal);
    }

    private void ShowStage(int ix)
    {
        ShowMode();

        foreach(Renderer r in SpriteHolders)
            r.enabled = false;
        foreach(EggData e in _stages[ix].GetEggs())
        {
            SpriteHolders[e.Row * 3 + e.Col].material.mainTexture = GetTexture(e.Egg);
            SpriteHolders[e.Row * 3 + e.Col].enabled = true;
        }

        if(ix == 0)
            Buttons[0].GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0.5f);
        else
            Buttons[0].GetComponent<Renderer>().material.color = new Color(0.5f, 0.5f, 0.5f);

        if(ix == _currentStage && _currentStage != _stages.Count - 1)
            Buttons[1].GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0.5f);
        else
            Buttons[1].GetComponent<Renderer>().material.color = new Color(0.5f, 0.5f, 0.5f);

        _currentShownStage = ix;
    }

    private Texture GetTexture(Egg egg)
    {
        int r = 0;
        if((egg & Egg.Green) != Egg.None)
            r += 1;
        else if((egg & Egg.Blue) != Egg.None)
            r += 2;
        else if((egg & Egg.Yellow) != Egg.None)
            r += 3;

        if((egg & Egg.Special) != Egg.None)
            r += 4;
        else if((egg & Egg.Magnet) != Egg.None)
            r += 8;
        else if((egg & Egg.Foiler) != Egg.None)
            r += 12;
        else if((egg & Egg.Basket) != Egg.None)
            r += 16;

        return SpriteTextures[r];
    }

    private Texture MetalTexture(int metal)
    {
        return SpriteTextures[metal % 4 + 20];
    }

    private int TimeFromTo(int a, int b)
    {
        int x = (a / 3) - (b / 3);
        int y = (a % 3) - (b % 3);

        return x * x + y * y;
    }

    private class Frame
    {
        public const int SIZE = 3;

        private Egg[] Eggs
        {
            get;
            set;
        }

        public Frame(params EggData[] eggs)
        {
            Eggs = new Egg[SIZE * SIZE];
            foreach(EggData data in eggs)
                AddEgg(data);
        }

        public void AddEgg(EggData egg)
        {
            AddEgg(egg.Egg, egg.Row, egg.Col);
        }

        public void AddEgg(Egg egg, int row, int col)
        {
            int colors = new bool[] { (egg & Egg.Red) != 0, (egg & Egg.Green) != 0, (egg & Egg.Blue) != 0, (egg & Egg.Yellow) != 0 }.Count(b => b);
            if(colors != 1)
                throw new ArgumentException("Bad Egg Colors: " + egg.ToString());
            int types = new bool[] { (egg & Egg.Special) != 0, (egg & Egg.Magnet) != 0, (egg & Egg.Foiler) != 0, (egg & Egg.Basket) != 0 }.Count(b => b);
            if(types > 1)
                throw new ArgumentException("Bad Egg Types: " + egg.ToString());
            if(row < 0 || col < 0 || row > SIZE || col > SIZE)
                throw new ArgumentOutOfRangeException("Bad Position: " + row + " " + col);

            //if(Eggs[SIZE * row + col] != Egg.None)
            //    throw new InvalidOperationException("Duplicate Position: " + row + " " + col);

            Eggs[SIZE * row + col] = egg;
        }

        public List<EggData> GetEggs()
        {
            return Eggs.Select((e, i) => new EggData() { Egg = e, Row = i / SIZE, Col = i % SIZE }).Where(d => d.Egg != Egg.None).ToList();
        }

        public bool Has(Egg egg)
        {
            return Eggs.Contains(egg);
        }
        public bool Has(int row, int col)
        {
            if(row < 0 || col < 0 || row > SIZE || col > SIZE)
                throw new ArgumentOutOfRangeException("Bad Position: " + row + " " + col);

            return Eggs[SIZE * row + col] != Egg.None;
        }

        public override string ToString()
        {
            return Eggs.Select(e => "[" + EggString(e) + "]").Join("");
        }
        private static string EggString(Egg e)
        {
            if(e == Egg.None)
                return "  ";
            string r = "";
            if((e & Egg.Red) != Egg.None)
                r += "R";
            else if((e & Egg.Green) != Egg.None)
                r += "G";
            else if((e & Egg.Blue) != Egg.None)
                r += "B";
            else if((e & Egg.Yellow) != Egg.None)
                r += "Y";
            else
                r += "X";

            if((e & Egg.Special) != Egg.None)
                r += "S";
            else if((e & Egg.Magnet) != Egg.None)
                r += "U";
            else if((e & Egg.Foiler) != Egg.None)
                r += "F";
            else if((e & Egg.Basket) != Egg.None)
                r += "B";
            else
                r += " ";

            return r;
        }
    }

    private struct EggData
    {
        public Egg Egg;
        public int Row;
        public int Col;
    }

    [Flags]
    private enum Egg
    {
        None = 0,

        Red = 1,
        Green = 2,
        Blue = 4,
        Yellow = 8,

        Special = 16,
        Magnet = 32,
        Foiler = 64,
        Basket = 128
    }
}
