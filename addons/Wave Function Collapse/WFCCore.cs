/*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
|||||||||||||||||||||||||||||||||||||||||||||| MIT LICENSE ||||||||||||||||||||||||||||||||||||||||||||||||||||
Copyright 2023 hamsterbyte
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the “Software”), to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and
to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions
of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
/////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

using System.Diagnostics;

namespace hamsterbyte.WFC{
    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    USING => REQUIRED NAMESPACE DECLARATIONS
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    using System;
    using System.Collections;
    using Godot;
    using System.Collections.Generic;
    using System.IO;
    using Godot.Collections;
    using System.Linq;
    using System.Text;
    using System.Text.Json;

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    DELEGATES => REQUIRED DELEGATES FOR CORE FUNCTIONALITY
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public abstract class Delegates{
        public delegate void OnComplete(WFCResult result);
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    WFCCELL => REQUIRED VARIABLES & CONSTRUCTOR
    THIS CLASS IS USED TO DEFINE THE DATA AND BEHAVIOR OF CELLS IN THE WFCGRID
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public partial class WFCCell{
        public int TileIndex{ get; private set; } = -1;
        public bool Collapsed{ get; private set; }
        public bool[] Options{ get; }
        public Coordinates Coordinates{ get; private set; }
        private readonly int[] rawFrequencies;
        private readonly double[] logFrequencies;
        private int sumOfRawFrequencies;
        private int sumOfPossibleFrequencies;
        private double sumOfPossibleFrequencyLogFrequencies;
        private readonly double entropyNoise;


        public WFCCell(Coordinates _coordinates, int[] _frequencies){
            Coordinates = _coordinates;
            rawFrequencies = _frequencies;
            logFrequencies = new double[rawFrequencies.Length];
            Options = new bool[rawFrequencies.Length];
            System.Array.Fill(Options, true);
            entropyNoise = WFCGrid.Random.NextDouble() * .0001;
            PrecalculateFrequencies();
        }
        
        
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    WFCGRID => REQUIRED VARIABLES, PROPERTIES, AND CONSTRUCTOR
    ALL MEMBERS DEFINED  IN THIS SECTION ARE REQUIRED FOR FUNCTIONALITY OF THE WFCGRID CLASS THESE SHOULD NOT BE
    MODIFIED WITHOUT A SOLID UNDERSTANDING OF HOW THIS SYSTEM WORKS.
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

     sealed partial class WFCGrid{
        #region VARIABLES

        public bool Busy;
        public readonly Queue<Coordinates> AnimationCoordinates = new();

        private int currentAttempt;
        private WFCCell[,] cells;
        private int remainingUncollapsedCells;
        private bool validCollapse = true;
        private readonly int[] rawFrequencies;
        private EntropyHeap entropyHeap;
        private readonly int[,,] adjacencyRules;
        private readonly Stack<RemovalUpdate> removalUpdates;
        private readonly bool suppressNotifications;

        #endregion

        #region DIMENSIONS

        public int Width{
            get{
                if (width == 0){
                    width = cells.GetLength(0);
                }

                return width;
            }
        }

        private int width;

        public int Height{
            get{
                if (height == 0){
                    height = cells.GetLength(1);
                }

                return height;
            }
        }

        private int height;

        #endregion

        #region RANDOM NUMBER GENERATOR

        public static Random Random => pseudoRandom ??= new Random();
        private static Random pseudoRandom;

        #endregion

        #region EVENTS

        public static Delegates.OnComplete onComplete;

        #endregion

        private bool IsInBounds(Coordinates c){
            return c.X >= 0 && c.X < Width && c.Y >= 0 && c.Y < Height;
        }

        /// <summary>
        /// Constructor
        /// A WFCGrid always requires a width, height, and rules
        /// The rules array will contain all adjacency and frequency rules and is loaded from a JSON file
        /// </summary>
        /// <param name="_width">Width of the WFCGrid in cells</param>
        /// <param name="_height">Height of the WFCGrid in cells</param>
        /// <param name="_rules">Array of WFCRule. Loaded from JSON</param>
        /// <param name="_suppressNotifications">Suppress GD.Print messages from this class. Set false for debugging</param>
        public WFCGrid(int _width, int _height, List<WFCRule> _rules, bool _suppressNotifications = false){
            onComplete += NotifyComplete;
            suppressNotifications = _suppressNotifications;
            cells = new WFCCell[_width, _height];
            remainingUncollapsedCells = cells.Length;
            rawFrequencies = new int[_rules.Count];
            for (int i = 0; i < rawFrequencies.Length; i++){
                rawFrequencies[i] = _rules[i].Frequency;
            }

            adjacencyRules = WFCRule.ToAdjacencyRules(_rules);
            for (int x = 0; x < _width; x++){
                for (int y = 0; y < _height; y++){
                    cells[x, y] = new WFCCell(new Coordinates(x, y), rawFrequencies);
                }
            }

            removalUpdates = new Stack<RemovalUpdate>();
            entropyHeap = new EntropyHeap(Width * Height);
        }


        private void NotifyComplete(WFCResult result){
            if (!suppressNotifications){
                GD.Print(result);
            }
        }

        private void Reset(bool _resetAttempts = false){
            cells = new WFCCell[Width, Height];
            for (int x = 0; x < Width; x++){
                for (int y = 0; y < Height; y++){
                    cells[x, y] = new WFCCell(new Coordinates(x, y), rawFrequencies);
                }
            }

            remainingUncollapsedCells = cells.Length;
            removalUpdates.Clear();
            entropyHeap = new EntropyHeap(Width * Height);
            validCollapse = true;
            Busy = false;
            currentAttempt = _resetAttempts ? 0 : currentAttempt;
            AnimationCoordinates.Clear();
        }
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    WFCGRID => ICOLLECTION IMPLEMENTATION
    IMPLEMENTED FOR EASE OF ACCESS TO THE CELLS ARRAY WITHOUT THE NEED TO GO THROUGH A PUBLIC REFERENCE
    OR UNNECESSARY LAYERS OF ABSTRACTION. WOULD RECOMMEND REVIEWING THIS CODE AT YOUR LEISURE
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public partial class WFCGrid : ICollection{
        #region ICOLLECTION IMPLEMENTATION

        /// <summary>
        /// Indexer to access cells array providing both x and y coordinates
        /// This is the preferred method for accessing the cells array as it requires no calculation
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public WFCCell this[int x, int y]{
            get => cells[x, y];
            set => cells[x, y] = value;
        }


        /// <summary>
        /// Indexer to access cells array providing only a single index
        /// Assumes iteration is from top to bottom, and left to right
        /// This is another way to access the array
        /// Less performant than 2D indexer as it requires several operations to expand the index
        /// Performance degradation will be negligible
        /// </summary>
        /// <param name="i"></param>
        public WFCCell this[int i]{
            get => cells[i % Width, i / Width];
            set => cells[i % Width, i / Width] = value;
        }

        /// <summary>
        /// Return the custom enumerator for the grid
        /// This is used for iterating with a foreach loop
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator(){
            return new CellEnumerator(cells);
        }

        /// <summary>
        /// Copy the cell array to another
        /// The output will be flattened
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        public void CopyTo(System.Array array, int index){
            foreach (WFCCell c in cells){
                array.SetValue(c, index);
                index++;
            }
        }

        /// <summary>
        /// These parameters are required for ICollection implementation
        /// MSDN Documentation can provide further information regarding these
        /// </summary>
        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;
        int ICollection.Count => cells.Length;

        #endregion
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    WFCGRID => CUSTOM ENUMERATOR IMPLEMENTATION
    THIS CODE IS REQUIRED FOR ACCESSING THE WFCGRID WITH A FOREACH LOOP. NOTE THAT THE ARRAY WILL BE FLATTENED AND
    IS ITERATED IN A TOP TO BOTTOM LEFT TO RIGHT ORDER
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public class CellEnumerator : IEnumerator{
        private readonly WFCCell[,] cells;
        private int Cursor;
        private readonly int Count;

        /// <summary>
        /// Constructor for custom enumerator
        /// </summary>
        /// <param name="_cells"></param>
        public CellEnumerator(WFCCell[,] _cells){
            cells = _cells;
            Cursor = -1;
            Count = _cells.GetLength(0) * cells.GetLength(1);
        }

        /// <summary>
        /// Used to reset the cursor position of the enumerator
        /// </summary>
        public void Reset(){
            Cursor = -1;
        }

        /// <summary>
        /// Used to move the cursor to the next value in the enumeration
        /// </summary>
        /// <returns></returns>
        public bool MoveNext(){
            if (Cursor < Count)
                Cursor++;
            return Cursor != Count;
        }

        /// <summary>
        /// Used to return the cell from the enumerator that exists at the cursor position
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public object Current{
            get{
                if (Cursor < 0 || Cursor == Count)
                    throw new InvalidOperationException();
                return cells[Cursor % cells.GetLength(0), Cursor / cells.GetLength(0)];
            }
        }
    }


    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ENTROPYHEAP => CUSTOM MINHEAP IMPLEMENTATION
    SIMILAR FUNCTIONALITY TO THIS CLASS CAN BE ACHIEVED BY USING HEAP SORT ON A DIFFERENT COLLECTION LIKE A LIST OR
    ARRAY, BUT THIS COLLECTION SORTS ITSELF BY LOWEST ENTROPY AUTOMATICALLY WHEN IT IS MODIFIED
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public class EntropyHeap{
        private readonly EntropyCoordinates[] coords;
        private int size;

        public EntropyHeap(int capacity){
            coords = new EntropyCoordinates[capacity];
        }

        private int GetLeftChildIndex(int i) => 2 * i + 1;
        private int GetRightChildIndex(int i) => 2 * i + 2;
        private int GetParentIndex(int i) => (i - 1) / 2;

        private bool HasLeftChild(int i) => GetLeftChildIndex(i) < size;
        private bool HasRightChild(int i) => GetRightChildIndex(i) < size;
        private bool IsRoot(int i) => i == 0;

        private EntropyCoordinates GetLeftChild(int i) => coords[GetLeftChildIndex(i)];
        private EntropyCoordinates GetRightChild(int i) => coords[GetRightChildIndex(i)];
        private EntropyCoordinates GetParent(int i) => coords[GetParentIndex(i)];

        public bool IsEmpty => size == 0;

        public EntropyCoordinates Peek() => size == 0 ? throw new IndexOutOfRangeException() : coords[0];

        private void Swap(int a, int b) => (coords[a], coords[b]) = (coords[b], coords[a]);

        public EntropyCoordinates Pop(){
            if (size == 0) throw new IndexOutOfRangeException();
            EntropyCoordinates result = coords[0];
            coords[0] = coords[size - 1];
            size--;
            RecalculateDown();
            return result;
        }

        public void Push(EntropyCoordinates _coords){
            if (size == coords.Length) throw new IndexOutOfRangeException();
            coords[size] = _coords;
            size++;
            RecalculateUp();
        }

        private void RecalculateDown(){
            int index = 0;
            while (HasLeftChild(index)){
                int lesserIndex = GetLeftChildIndex(index);
                if (HasRightChild(index) && GetRightChild(index).Entropy < GetLeftChild(index).Entropy){
                    lesserIndex = GetRightChildIndex(index);
                }

                if (coords[lesserIndex].Entropy >= coords[index].Entropy){
                    break;
                }

                Swap(lesserIndex, index);
                index = lesserIndex;
            }
        }

        private void RecalculateUp(){
            int index = size - 1;
            while (!IsRoot(index) && coords[index].Entropy < GetParent(index).Entropy){
                int parentIndex = GetParentIndex(index);
                Swap(parentIndex, index);
                index = parentIndex;
            }
        }
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    EXTENSIONS => EXTENSION METHODS
    IMPLEMENTED TO DECREASE CODE COMPLEXITY. ADD ANY FURTHER RELATED EXTENSION METHODS TO THIS CLASS IF YOU WISH TO
    KEEP THEM INSIDE THE SAME NAMESPACE
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public static partial class Extensions{
        public static int Wrap(this int n, int maxValue, int minValue = 0){
            int remainder = n % maxValue;
            return remainder < minValue ? maxValue + remainder : remainder;
        }

        public static Coordinates Wrap(this Coordinates c, int xMax, int yMax, int xMin = 0, int yMin = 0){
            c.X = c.X.Wrap(xMax, xMin);
            c.Y = c.Y.Wrap(yMax, yMin);
            return c;
        }

        public static Vector2I Wrap(this Vector2I v, int xMax, int yMax, int xMin = 0, int yMin = 0){
            v.X = v.X.Wrap(xMax, xMin);
            v.Y = v.Y.Wrap(yMax, yMin);
            return v;
        }

        public static bool InBounds(this Vector2I v){
            return v is{ X: >= 0, Y: >= 0 };
        }

        public static WFCCell Random(this WFCCell[,] cells){
            return cells[WFCGrid.Random.Next(0, cells.GetLength(0)), WFCGrid.Random.Next(0, cells.GetLength(1))];
        }
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    WFCRULE & SERIALIZE => USED FOR EDITOR PLUGIN AND SERIALIZATION
    DEFINES ALL REQUIRED MEMBERS FOR CREATING AND DESERIALIZING FREQUENCY AND ADJACENCY RULES. RULES ARE SERIALIZED
    USING JSON AND STORED ON DISK, BUT GODOT(V4.1) CURRENTLY DOESN'T HANDLE THIS VERY WELL AND CAN RESULT IN PLUGIN
    INSTABILITIES
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    [Serializable]
    public partial class WFCRule{
        public int Frequency{ get; set; }
        public WFCOptions Options{ get; set; } = new();

        /// <summary>
        /// Load a List of WFCRule's from disk from the given global path
        /// </summary>
        /// <param name="path">Global Path</param>
        /// <returns>A List of WFCRule loaded from disk</returns>
        public static List<WFCRule> FromJSONFile(string path){
            string json = File.ReadAllText(path);
            Godot.Collections.Array a = Json.ParseString(json).AsGodotArray();
            List<WFCRule> rules = new();
            for (int i = 0; i < a.Count; i++){
                Dictionary d = a[i].AsGodotDictionary();
                rules.Add(new WFCRule());
                rules[i].Frequency = d["Frequency"].AsInt32();
                foreach (KeyValuePair<Variant, Variant> k in d["Options"].AsGodotDictionary()){
                    switch (k.Key.ToString()){
                        case "Up":
                            foreach (string o in k.Value.AsStringArray()){
                                rules[i].Options.Up.Add(int.Parse(o));
                            }

                            break;
                        case "Right":
                            foreach (string o in k.Value.AsStringArray()){
                                rules[i].Options.Right.Add(int.Parse(o));
                            }

                            break;
                        case "Down":
                            foreach (string o in k.Value.AsStringArray()){
                                rules[i].Options.Down.Add(int.Parse(o));
                            }

                            break;
                        case "Left":
                            foreach (string o in k.Value.AsStringArray()){
                                rules[i].Options.Left.Add(int.Parse(o));
                            }

                            break;
                    }
                }
            }

            return rules;
        }

        /// <summary>
        /// Convert a list of WFCRule's to a 3-Dimensional array that is usable by the WFCGrid system
        /// This conversion is done for performance reasons
        /// </summary>
        /// <param name="_ruleList">Input list of WFCRule's</param>
        /// <returns>3-Dimensional integer array representing the original list of WFCRule's excepting frequency</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static int[,,] ToAdjacencyRules(List<WFCRule> _ruleList){
            int[,,] adjacencyRules = new int[_ruleList.Count, 4, _ruleList.Count];
            for (int r = 0; r < adjacencyRules.GetLength(0); r++){
                for (int d = 0; d < adjacencyRules.GetLength(1); d++){
                    for (int o = 0; o < adjacencyRules.GetLength(2); o++){
                        adjacencyRules[r, d, o] = d switch{
                            0 => _ruleList[r].Options.Up.Contains(o) ? 1 : 0,
                            1 => _ruleList[r].Options.Right.Contains(o) ? 1 : 0,
                            2 => _ruleList[r].Options.Down.Contains(o) ? 1 : 0,
                            3 => _ruleList[r].Options.Left.Contains(o) ? 1 : 0,
                            _ => throw new ArgumentOutOfRangeException()
                        };
                    }
                }
            }

            return adjacencyRules;
        }
    }

    public static class Serialize{
        public static string ToJSON(this List<WFCRule> self){
            string json = JsonSerializer.Serialize(self, new JsonSerializerOptions{ WriteIndented = true });
            return json;
        }
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    WFCOPTIONS => USED FOR EDITOR PLUGIN AND SERIALIZATION
    DEFINES ALL REQUIRED MEMBERS FOR CONSTRUCTING AND INTERACTING WITH WFCOPTIONS. USED BY EDITOR PLUGIN
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    [Serializable]
    public class WFCOptions{
        public List<int> Up{ get; private set; }
        public List<int> Right{ get; private set; }
        public List<int> Down{ get; private set; }
        public List<int> Left{ get; private set; }

        public string DebugString(){
            StringBuilder b = new();
            b.Append("UP:: {");
            for (int i = 0; i < Up.Count; i++){
                b.Append($"{Up[i]}");
                b.Append(i < Up.Count - 1 ? ", " : string.Empty);
            }

            b.Append("}\n");

            b.Append("RIGHT:: {");
            for (int i = 0; i < Right.Count; i++){
                b.Append($"{Right[i]}");
                b.Append(i < Right.Count - 1 ? ", " : string.Empty);
            }

            b.Append("}\n");

            b.Append("DOWN:: {");
            for (int i = 0; i < Down.Count; i++){
                b.Append($"{Down[i]}");
                b.Append(i < Down.Count - 1 ? ", " : string.Empty);
            }

            b.Append("}\n");


            b.Append("LEFT:: {");
            for (int i = 0; i < Left.Count; i++){
                b.Append($"{Left[i]}");
                b.Append(i < Left.Count - 1 ? ", " : string.Empty);
            }

            b.Append("}\n");

            return b.ToString();
        }

        public WFCOptions(){
            Up = new List<int>();
            Right = new List<int>();
            Down = new List<int>();
            Left = new List<int>();
        }

        public WFCOptions(int maxEntropy){
            Up = new List<int>();
            Right = new List<int>();
            Down = new List<int>();
            Left = new List<int>();
            for (int i = 0; i < maxEntropy; i++){
                Up.Add(i);
                Right.Add(i);
                Down.Add(i);
                Left.Add(i);
            }
        }

        public void MergeUp(WFCOptions other) => Up = Up.Intersect(other.Down).ToList();
        public void MergeRight(WFCOptions other) => Right = Right.Intersect(other.Left).ToList();
        public void MergeDown(WFCOptions other) => Down = Down.Intersect(other.Up).ToList();
        public void MergeLeft(WFCOptions other) => Left = Left.Intersect(other.Right).ToList();

        public void Toggle(NeighbourDirections direction, int tileIndex, bool toggled = true){
            switch (direction){
                case NeighbourDirections.Up:
                    if (toggled){
                        Up.Add(tileIndex);
                    }
                    else{
                        Up.Remove(tileIndex);
                    }

                    break;
                case NeighbourDirections.Right:
                    if (toggled){
                        Right.Add(tileIndex);
                    }
                    else{
                        Right.Remove(tileIndex);
                    }

                    break;
                case NeighbourDirections.Down:
                    if (toggled){
                        Down.Add(tileIndex);
                    }
                    else{
                        Down.Remove(tileIndex);
                    }

                    break;
                case NeighbourDirections.Left:
                    if (toggled){
                        Left.Add(tileIndex);
                    }
                    else{
                        Left.Remove(tileIndex);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    NEIGHBOURDIRECTIONS => USED FOR EDITOR PLUGIN AND SERIALIZATION
    BASIC ENUMERATION TO STORE DIRECTIONS IN PROPER INDEXED ORDER
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public enum NeighbourDirections{
        Up,
        Right,
        Down,
        Left
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    COORDINATES => CUSTOM CLASS SIMILAR TO VECTOR2I
    THIS CLASS IS USED TO PREVENT UNNECESSARY ACCESS TO THE GODOT API AND AVOID MARSHALLING COSTS DURING GENERATION
    IT INCLUDES UNUSED FUNCTIONALITY THAT WAS ADDED TO MAKE EXTENDING THE SYSTEM EASIER IN THE FUTURE
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public struct Coordinates{
        public int X;
        public int Y;

        public Vector2I AsVector2I => new(X, Y);

        public static Coordinates Up{ get; } = new(0, -1);
        public static Coordinates Right{ get; } = new(1, 0);
        public static Coordinates Left{ get; } = new(-1, 0);
        public static Coordinates Down{ get; } = new(0, 1);
        public static Coordinates UpLeft{ get; } = new(-1, -1);
        public static Coordinates UpRight{ get; } = new(1, -1);
        public static Coordinates DownLeft{ get; } = new(-1, 1);
        public static Coordinates DownRight{ get; } = new(1, 1);
        public static Coordinates[] Cardinals{ get; } ={ Up, Right, Down, Left };
        public static Coordinates[] Ordinals{ get; } ={ UpLeft, UpRight, DownRight, DownLeft };

        public static Coordinates[] Neighbours{ get; } =
            { UpLeft, Up, UpRight, Left, Right, DownLeft, Down, DownRight };

        public bool Equals(Coordinates other){
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj){
            return obj is Coordinates other && Equals(other);
        }

        public override int GetHashCode(){
            return HashCode.Combine(X, Y);
        }

        public static Coordinates operator +(Coordinates a, Coordinates b){
            a.X += b.X;
            a.Y += b.Y;
            return a;
        }

        public static Coordinates operator -(Coordinates a, Coordinates b){
            a.X -= b.X;
            a.Y -= b.Y;
            return a;
        }

        public static bool operator ==(Coordinates a, Coordinates b) => a.X == b.X && a.Y == b.Y;

        public static bool operator !=(Coordinates a, Coordinates b) => a.X != b.X || a.Y != b.Y;

        public override string ToString(){
            return $"Coord ({X}, {Y})";
        }

        public Coordinates(){
            X = 0;
            Y = 0;
        }

        public Coordinates(int _x, int _y){
            X = _x;
            Y = _y;
        }
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    REMOVALUPDATE => VALUE TYPE USED FOR QUEUEING OPTION REMOVALS
    THIS KEEPS INFORMATION RELEVANT TO REMOVING OPTIONS FROM NEIGHBOURING CELLS TOGETHER
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public struct RemovalUpdate{
        public int TileIndex;
        public Coordinates Coordinates;
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ENTROPYCOORDINATES => VALUE TYPE USED FOR QUEUEING CELL COLLAPSES
    THIS KEEPS INFORMATION RELEVANT TO CELL COLLAPSES TOGETHER AND TO AVOID PASSING THE CELLS DIRECTLY; THIS SHOULD
    HELP TO PREVENT RACE CONDITIONS IF THE CORE GETS MULTITHREADING IN THE FUTURE
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public struct EntropyCoordinates{
        public double Entropy;
        public Coordinates Coordinates;

        public static EntropyCoordinates Invalid => new()
            { Entropy = -1, Coordinates = new Coordinates() };
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    WFCResult => VALUE TYPE USED FOR RETURNING THE RESULT OF A GENERATION
    THIS KEEPS INFORMATION RELEVANT TO GENERATION TOGETHER
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

    public struct WFCResult{
        public WFCGrid Grid;
        public bool Success;
        public int Attempts;
        public long ElapsedMilliseconds;

        public override string ToString(){
            StringBuilder s = new();
            s.Append("Result: ");
            s.Append(Success ? "Successful\n" : "Contradiction Failure\n");
            s.Append($"Attempts: {Attempts}\n");
            s.Append($"Elapsed Time: {ElapsedMilliseconds}ms\n");
            return s.ToString();
        }
    }
}