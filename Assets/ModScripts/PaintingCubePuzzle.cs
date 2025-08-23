using System;
using System.Collections.Generic;
using System.Linq;
using static UnityEngine.Random;


public struct OrientedCube
{
    public ColorInfo[] CurrentCube { get; set; }
    public ColorInfo[] CurrentGrid { get; set; }
    public int[] CurrentOrientation { get; set; }
    public int CurrentPosition { get; set; }

    public OrientedCube(ColorInfo[] currentCube, ColorInfo[] currentGrid, int[] currentOrientation, int currentPosition)
    {
        CurrentCube = currentCube;
        CurrentGrid = currentGrid;
        CurrentOrientation = currentOrientation;
        CurrentPosition = currentPosition;
    }
}

public class PaintingCubePuzzle
{
    private readonly PCColor _missingColor;

    private readonly ColorInfo[] _colorReferences;

    private bool failed;

    private int retries = 0;

    private readonly ColorInfo[] tempCube = new ColorInfo[6];
    public ColorInfo[] Grid;
    public int StartingPos;
    public List<DirectionInfo> GetTrackings;

    public bool CheckVertex(ColorInfo[] vertex) => Enumerable.Range(0, 3).Select(x => tempCube[x]).SequenceEqual(vertex);

    public string ObtainVertex() => Enumerable.Range(0, 3).Select(x => $"[{tempCube[x].Color}]").Join();
    public string ObtainGrid() => Enumerable.Range(0, 4).Select(row => Enumerable.Range(0, 4).Select(col => $"[{Grid[4 * row + col]?.Color.ToString() ?? "X"}]").Join("")).Join(";");

    private static readonly int[][] cubeOrientationTable =
    {
        new[] { 1, 5, 2, 0, 4, 3 },
        new[] { 4, 1, 0, 3, 5, 2 },
        new[] { 3, 0, 2, 5, 4, 1 },
        new[] { 2, 1, 5, 3, 0, 4 }
    };

    public PaintingCubePuzzle(PCColor missingColor, ColorInfo[] colorReferences, MonoRandom ruleSeeder)
    {
        _missingColor = missingColor;
        _colorReferences = colorReferences;

        GenerateCube(ruleSeeder);
    }



    private List<ColorInfo[]> gridCandidates = new List<ColorInfo[]>();
    private List<int> startingPosCandidates = new List<int>();
    private List<List<DirectionInfo>> trackingCandidates = new List<List<DirectionInfo>>();

    void GenerateCube(MonoRandom ruleSeeder)
    {
        var cubeNetOpposite = new Dictionary<int, int>
        {
            { 0, 5 },
            { 1, 3 },
            { 2, 4 },
            { 3, 1 },
            { 4, 2 },
            { 5, 0 }
        };

        var pcColors = (PCColor[]) Enum.GetValues(typeof(PCColor));
        var vertexIxes = new PCColor[7][];
        for (var cube = 0; cube < 7; cube++)
        {
            var colors = pcColors.ToList();
            colors.Remove((PCColor) cube);
            ruleSeeder.ShuffleFisherYates(colors);
            vertexIxes[cube] = new PCColor[3];
            for (var i = 0; i < 3; i++)
                vertexIxes[cube][i] = colors[i];
        }

        var vertex = Enumerable.Range(0, 3).ToArray();

        for (int i = 0; i < 3; i++)
            tempCube[vertex[i]] = _colorReferences[(int) vertexIxes[(int) _missingColor][i]];

        tryagain:

        var restColors = pcColors.Where(color => !vertexIxes[(int)_missingColor].Contains(color) && color != _missingColor).ToArray().Shuffle();

        for (int i = 0; i < 3; i++)
            tempCube[cubeNetOpposite[vertex[i]]] = _colorReferences[(int) restColors[i]];

        GeneratePuzzle();

        if (failed)
        {
            retries++;

            if (retries > 3)
                throw new Exception("The puzzle cannot be generated.");

            failed = false;

            goto tryagain;
        }

        var randomCandidate = Range(0, gridCandidates.Count);

        Grid = gridCandidates[randomCandidate];
        StartingPos = startingPosCandidates[randomCandidate];
        GetTrackings = trackingCandidates[randomCandidate];
    }

    void GeneratePuzzle()
    {
        var goalQueue = new Queue<int>(Enumerable.Range(0, 16).ToList().Shuffle());

        var oppositeDir = new Dictionary<CubeDirection, CubeDirection>
        {
            { CubeDirection.Up, CubeDirection.Down },
            { CubeDirection.Right, CubeDirection.Left },
            { CubeDirection.Down, CubeDirection.Up },
            { CubeDirection.Left, CubeDirection.Right }
        };

        DirectionInfo prev = null;

        while (goalQueue.Count > 0)
        {
            var goal = goalQueue.Dequeue();

            var cube = new OrientedCube(tempCube.ToArray(), new ColorInfo[16], Enumerable.Range(0, 6).ToArray(), goal);

            var trackedCandidates = new List<DirectionInfo>();

            while (true)
            {
                if (cube.CurrentCube.All(x => x == null) || trackedCandidates.Count > 40)
                    break;

                var randomMove = DirectionInfo.GetValidDirections(cube.CurrentPosition).Where(x => x != null).Where(x => prev?.Direction != x.Direction).PickRandom();

                trackedCandidates.Add(new DirectionInfo(oppositeDir[randomMove.Direction], cube.CurrentPosition));

                if (cube.CurrentGrid[cube.CurrentPosition] == null && cube.CurrentCube[cube.CurrentOrientation[5]] != null)
                {
                    cube.CurrentGrid[cube.CurrentPosition] = cube.CurrentCube[cube.CurrentOrientation[5]];
                    cube.CurrentCube[cube.CurrentOrientation[5]] = null;
                }
                else if (cube.CurrentGrid[cube.CurrentPosition] != null && cube.CurrentCube[cube.CurrentOrientation[5]] == null)
                {
                    cube.CurrentCube[cube.CurrentOrientation[5]] = cube.CurrentGrid[cube.CurrentPosition];
                    cube.CurrentGrid[cube.CurrentPosition] = null;
                }

                cube.CurrentPosition = randomMove.Position;
                prev = randomMove;
                var orientation = cube.CurrentOrientation.ToArray();
                cube.CurrentOrientation = cubeOrientationTable[(int)randomMove.Direction].Select(x => orientation[x]).ToArray();
            }

            prev = null;

            if (trackedCandidates.Count > 40 || cube.CurrentGrid[cube.CurrentPosition] != null || !IsValid(trackedCandidates, cube.CurrentGrid.ToArray(), cube.CurrentPosition))
                continue;           

            gridCandidates.Add(cube.CurrentGrid.ToArray());
            startingPosCandidates.Add(cube.CurrentPosition);
            trackingCandidates.Add(trackedCandidates);
        }

        if (gridCandidates.Count == 0)
            failed = true;

    }

    private bool IsValid(List<DirectionInfo> trackedDirections, ColorInfo[] grid, int pos)
    {
        var cube = new OrientedCube(new ColorInfo[6], grid.ToArray(), Enumerable.Range(0, 6).ToArray(), pos);

        for (int i = trackedDirections.Count - 1; i >= 0; i--)
        {
            if (cube.CurrentGrid[cube.CurrentPosition] == null && cube.CurrentCube[cube.CurrentOrientation[5]] != null)
            {
                cube.CurrentGrid[cube.CurrentPosition] = cube.CurrentCube[cube.CurrentOrientation[5]];
                cube.CurrentCube[cube.CurrentOrientation[5]] = null;
            }
            else if (cube.CurrentGrid[cube.CurrentPosition] != null && cube.CurrentCube[cube.CurrentOrientation[5]] == null)
            {
                cube.CurrentCube[cube.CurrentOrientation[5]] = cube.CurrentGrid[cube.CurrentPosition];
                cube.CurrentGrid[cube.CurrentPosition] = null;
            }

            cube.CurrentPosition = trackedDirections[i].Position;
            var orientation = cube.CurrentOrientation.ToArray();
            cube.CurrentOrientation = cubeOrientationTable[(int)trackedDirections[i].Direction].Select(x => orientation[x]).ToArray();
        }

        if (cube.CurrentCube.Count(x => x != null) == 5 && cube.CurrentGrid[cube.CurrentPosition] != null && cube.CurrentCube[cube.CurrentOrientation[5]] == null)
        {
            cube.CurrentCube[cube.CurrentOrientation[5]] = cube.CurrentGrid[cube.CurrentPosition];
            cube.CurrentGrid[cube.CurrentPosition] = null;
        }

        return cube.CurrentCube.All(x => x != null);
    }
}
