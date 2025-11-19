// ReSharper disable RedundantUsingDirective
// ReSharper disable CheckNamespace

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

//  	Goal
// The first two Leagues have a special objective to achieve. The full game will play, but you can only win by completing the objective. Once you do, you can start work on your complete bot.
// 📜 Side quest
// A secondary alternative leaderboard is calculated for those who choose to play a little differently: can you build tracks to create paths to interesting locations instead of battling for first place?
//
// More details in the final league.
//
// 🎯 League Objective 1:
// Connect two towns to form an active train connection to instantly win the game.
//
// The Boss AI will skip their turns. If you fail to form a connection within 100 turns, you will lose. Win at least 3 or more times out of 5 to progress to the next league.
//  	Rules
// In this game both players use paint to draw train tracks on a magic map, connecting towns on the map will bring prosterity to your own world.
//
// The map is represented in the game by a grid.
//
// 🗺️ Map
// The grid is made up of cells that can have one of four types:
//
// Type 0 for plains.
// Type 1 for river.
// Type 2 for mountains.
// Type 3 for a place of interest.
// The grid is partitioned into regions. Each region is made up of multiple contiguous cells. Each region has a unique regionId. Regions are susceptible to disruption by players. More information on disruption in next league.
//
// Some regions will contain a town. Towns can only be found on plain cells.
//
// 🏯 Towns
// Each game starts with multiple towns placed randomly across the map. There will only be one per region and no two regions sharing a border will both contain a town.
//
// Each town has a unique townId.
//
// Each town will have a list of desiredConnections: a list of town ids representing all the other towns this town would like to be connected to via train tracks placed by players.
//
// Providing a town with train tracks connecting it to a desired town is how players score points.
//
// Desired connections are unilateral. Meaning if town 0 spawns with a desired connection to town 1 , town 1 will not want to connect to town 0 .
//
// A town can have zero desiredConnections, but will always be the subject of at least one other town's desiredConnections.
//
// 🛤️ Placing Train Tracks
// Players are given on each turn 3 paint points they can use to place train tracks on the map. ⚠️ These points do not carry over to the next turn and will be lost if left unused.
//
// It costs:
//
// 1 paint point to place a track on plains.
// 2 paint points to place a track on river.
// 3 paint points to place a track on mountains.
// 3 paint points to place a track on a place of interest.
// A track's owner is the playerId ( 0 - 1 ) of the player that placed it. They will be the same color.
//
// If both players place a track on the same turn at the same location, the tracks's owner will be 2 , indicating a neutral track piece.
//
// A train track cannot be placed on a town or on an existing track.
//
// Once placed, a train track will automatically connect to other tracks and towns orthogonally adjacent to it.
//
// 🏯🛤️🏯 Connections
// For each pair of towns in which one has the other in its desiredConnections, if at least one path between the two exists, the shortest such path becomes the active connection between those towns.
//
// A path is an uninterrupted sequence of orthogannly adjacent cells with a train track or a town.
//
// If there are multiple shortest paths, the chosen path will always prioritize the direction in this order when moving from the requesting town to the desired connected town:
//
// NORTH
// EAST
// SOUTH
// WEST
// At the end of every turn, each active connection will provide 1 point to each player for every track they own in the path.


// Simarly to paint points, players can also use 1 disruption point per turn. It can be used to tamper with the map, giving you an edge over your opponent. These points aren't retained in between turns either.
//
//     Players may spend their disruption point each turn to increase the instability of any region by 1 .
//
//     Once a region's instability reaches 3 , that region is inked out, washing any placed train tracks away, and rendering any future placements on it impossible. Any active connections via this region will be severed.
//
//     It is not possible to disrupt a region that is already inked out.


namespace TrainGame;

public static class DebugLog
{
    private static readonly bool InfoEnabled = true;
    private static readonly bool DebugEnabled = true;
    private static readonly bool CheckPointEnabled = true;

    public static void CHECKPOINT(string message)
    {
        if (CheckPointEnabled)
            Console.Error.WriteLine(message);
    }

    public static void INFO(string message)
    {
        if (InfoEnabled)
            Console.Error.WriteLine(message);
    }

    public static void DEBUG(string message)
    {
        if (DebugEnabled)
            Console.Error.WriteLine(message);
    }
}

public class Coordinate
{
    public required int X { get; init; }

    public required int Y { get; init; }


    public override string ToString()
    {
        return $"({X},{Y})";
    }

    // Returns N E W S based on the direction from this to the target coordinate
    public string GetDirectionTo(Coordinate target)
    {
        if (target.X > X) return "E";
        if (target.X < X) return "W";
        if (target.Y > Y) return "S";
        if (target.Y < Y) return "N";

        DebugLog.INFO(
            $"ERROR: GetDirectionTo called with same coordinates : this=({X},{Y}) target=({target.X},{target.Y})");
        throw new ArgumentOutOfRangeException();
        return "X"; // same position
    }


    // Get coordinate in direction
    public Coordinate GetCoordinateInDirection(string direction)
    {
        switch (direction)
        {
            case "N":
                return new Coordinate { X = X, Y = Y - 1 };
            case "E":
                return new Coordinate { X = X + 1, Y = Y };
            case "S":
                return new Coordinate { X = X, Y = Y + 1 };
            case "W":
                return new Coordinate { X = X - 1, Y = Y };
            default:
                DebugLog.INFO("ERROR: GetCoordinateInDirection called with invalid direction: " + direction);
                throw new ArgumentOutOfRangeException();
        }
    }
}

public enum CellType
{
    Plains,
    River,
    Mountain,
    Poi
}

public class GameNode : Coordinate
{
    public required CellType CellType { get; init; }
    public required int RegionId { get; init; }

    public int TracksOwner { get; set; } = -1;
    public int Instability { get; set; }
    public bool Inked { get; set; }
    public string PartOfActiveConnections { get; set; } = "";

    public int PaintCost
    {
        get
        {
            // Players are given on each turn 3 paint points they can use to place train tracks on the map. ⚠️ These points do not carry over to the next turn and will be lost if left unused.
            //
            //     It costs:
            //
            // 1 paint point to place a track on plains.
            // 2 paint points to place a track on river.
            // 3 paint points to place a track on mountains.
            // 3 paint points to place a track on a place of interest.

            return CellType switch
            {
                CellType.Plains => 1,
                CellType.River => 2,
                CellType.Mountain => 3,
                CellType.Poi => 3,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public override string ToString()
    {
        return $"Node[X={X}, Y={Y}";
    }
}

public class Town
{
    public required int TownId { get; init; }
    public required GameNode Location { get; init; }
    public required List<int> DesiredConnections { get; init; }
}

#region GameArena

public class GameArena
{
    private readonly GameNode[,] _grid;
    public int MyId;
    public int OppId;
    public int turns;

    public GameArena()
    {
        string[] inputs;
        var myId = int.Parse(Console.ReadLine()); // 0 or 1
        var width = int.Parse(Console.ReadLine()); // map size
        var height = int.Parse(Console.ReadLine());
        Width = width;
        Height = height;
        _grid = new GameNode[width, height];
        myId = myId;
        if (myId == 0)
            OppId = 1;
        else
            OppId = 0;


        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            inputs = Console.ReadLine().Split(' ');
            var regionId = int.Parse(inputs[0]);
            var type = int.Parse(inputs[1]); // 0 (PLAINS), 1 (RIVER), 2 (MOUNTAIN), 3 (POI)
            var node = new GameNode
            {
                X = x,
                Y = y,
                CellType = type switch
                {
                    0 => CellType.Plains,
                    1 => CellType.River,
                    2 => CellType.Mountain,
                    3 => CellType.Poi,
                    _ => throw new ArgumentOutOfRangeException()
                },
                RegionId = regionId
            };
            _grid[x, y] = node;
        }

        var townCount = int.Parse(Console.ReadLine());
        for (var i = 0; i < townCount; i++)
        {
            inputs = Console.ReadLine().Split(' ');
            var townId = int.Parse(inputs[0]);
            var townX = int.Parse(inputs[1]);
            var townY = int.Parse(inputs[2]);
            var desiredConnections = inputs[3]; // comma-separated town ids e.g. 0,1,2,3


            var desiredList = desiredConnections == "x"
                ? new List<int>()
                : desiredConnections.Split(',').Select(int.Parse).ToList();

            var town = new Town
            {
                TownId = townId,
                Location = GetNode(townX, townY),
                DesiredConnections = desiredList
            };

            Towns = Towns.Append(town).ToList().AsReadOnly();
        }
    }

    public ReadOnlyCollection<Town> Towns { get; internal set; } = new([]);

    public int myId { get; internal set; }

    public int myScore { get; internal set; }
    public int foeScore { get; internal set; }

    public int Width { get; init; }
    public int Height { get; init; }

    public ReadOnlyCollection<GameNode> Nodes
    {
        get
        {
            var nodeList = new List<GameNode>(Width * Height);
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
                nodeList.Add(_grid[x, y]);

            return nodeList.AsReadOnly();
        }
    }

    public List<int> GetEnemyPriorityRegions()
    {
        // get the cells that are parts of completed connections between towns
        // get the regions of those cells
        var regions = new HashSet<int>();
        foreach (var node in Nodes)
            if (node.PartOfActiveConnections != "x" && node.TracksOwner == OppId)
                regions.Add(node.RegionId);

        // sort by the region that has the most tracks owned by the enemy
        var regionList = regions.ToList();
        regionList.Sort((a, b) =>
        {
            var aCount = Nodes.Count(n => n.RegionId == a && n.TracksOwner == OppId);
            var bCount = Nodes.Count(n => n.RegionId == b && n.TracksOwner == OppId);
            return bCount.CompareTo(aCount);
        });

        regionList.AddRange(GetEnemyRegionsSortedByMostTracks());

        // exclude regions that have towns
        regionList = regionList.Where(rid => !Towns.Any(t => t.Location.RegionId == rid)).ToList();

        return regionList;
    }

    public List<int> GetEnemyRegionsSortedByMostTracks()
    {
        // get all regions where the enemy has tracks
        var regions = new HashSet<int>();
        foreach (var node in Nodes)
            if (node.TracksOwner == OppId)
                regions.Add(node.RegionId);

        // sort by the region that has the most tracks owned by the enemy
        var regionList = regions.ToList();
        regionList.Sort((a, b) =>
        {
            var aCount = Nodes.Count(n => n.RegionId == a && n.TracksOwner == OppId);
            var bCount = Nodes.Count(n => n.RegionId == b && n.TracksOwner == OppId);
            return bCount.CompareTo(aCount);
        });

        return regionList;
    }

    public List<GameNode> GetGameNodesThatAreCompletedConnectionsBetweenTowns()
    {
        // I want all the nodes that are part of active connections between towns
        // I want my nodes specifically
        var nodes = new List<GameNode>();
        foreach (var node in Nodes)
            if (node.PartOfActiveConnections != "x")
                nodes.Add(node);
        return nodes;
    }


    public void UpdateScores()
    {
        var _myScore = int.Parse(Console.ReadLine());
        var _foeScore = int.Parse(Console.ReadLine());

        myScore = _myScore;
        foeScore = _foeScore;
    }

    public void UpdateCellStatus()
    {
        string[] inputs;
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var cell = GetNode(x, y);
            inputs = Console.ReadLine().Split(' ');
            var tracksOwner = int.Parse(inputs[0]);
            var instability = int.Parse(inputs[1]); // region inked (destroyed) when this >= 3.
            var inked = inputs[2] != "0"; // true if region is destroyed.
            var
                partOfActiveConnections =
                    inputs
                        [3]; // if this cell is part of one or more railway connections, this will be town ids (separated by -) in a list separated by commas. e.g. 0-1,1-2,1-3. "x" otherwise.

            cell.TracksOwner = tracksOwner;
            cell.Instability = instability;
            cell.Inked = inked;
            cell.PartOfActiveConnections = partOfActiveConnections;
        }
    }

    public GameNode GetNode(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            DebugLog.INFO($"Arena Invalid coordinate requested: ({x},{y})");
            throw new ArgumentOutOfRangeException();
        }

        return _grid[x, y];
    }


    public List<GameNode> GetNeighbors(GameNode node)
    {
        var neighbors = new List<GameNode>();
        var directions = new (int dx, int dy)[]
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        foreach (var (dx, dy) in directions)
        {
            int nx = node.X + dx, ny = node.Y + dy;
            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
            neighbors.Add(_grid[nx, ny]);
        }

        return neighbors;
    }

    public int CalculateManhattanDistance(Coordinate a, Coordinate b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }


    public List<GameNode> FindAStarPath(GameNode start, GameNode goal, bool aggressiveDisrupt,
        List<GameNode>? nodesToExclude = null)
    {
        var openSet = new SortedSet<(int fScore, int x, int y)>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var gScore = new Dictionary<(int, int), int>();
        var fScore = new Dictionary<(int, int), int>();
        var inOpenSet = new HashSet<(int, int)>();

        (int, int) startPos = (start.X, start.Y);
        (int, int) goalPos = (goal.X, goal.Y);

        gScore[startPos] = 0;
        fScore[startPos] = CalculateManhattanDistance(start, goal);
        openSet.Add((fScore[startPos], start.X, start.Y));
        inOpenSet.Add(startPos);

        var excludeSet = nodesToExclude != null
            ? new HashSet<(int, int)>(nodesToExclude.Select(n => (n.X, n.Y)))
            : new HashSet<(int, int)>();

        while (openSet.Count > 0)
        {
            var currentTuple = openSet.Min;
            openSet.Remove(currentTuple);
            var currentPos = (currentTuple.x, currentTuple.y);

            if (currentPos == goalPos)
            {
                var path = new List<GameNode>();
                var pos = goalPos;
                while (pos != startPos)
                {
                    path.Add(GetNode(pos.Item1, pos.Item2));
                    pos = cameFrom[pos];
                }

                path.Add(start);
                path.Reverse();
                return path;
            }

            inOpenSet.Remove(currentPos);
            var currentNode = GetNode(currentPos.Item1, currentPos.Item2);

            foreach (var neighbor in GetNeighbors(currentNode))
            {
                if (aggressiveDisrupt)
                {
                    if (neighbor.Instability == 3)
                        continue;
                }
                else
                {
                    if (neighbor.Instability == 4)
                        continue;
                }

                var neighborPos = (neighbor.X, neighbor.Y);

                if (excludeSet.Contains(neighborPos))
                    continue;

                // Use PaintCost as the movement cost
                var tentativeGScore = gScore[currentPos] + neighbor.PaintCost;
                if (!gScore.ContainsKey(neighborPos) || tentativeGScore < gScore[neighborPos])
                {
                    cameFrom[neighborPos] = currentPos;
                    gScore[neighborPos] = tentativeGScore;
                    fScore[neighborPos] = tentativeGScore + CalculateManhattanDistance(neighbor, goal);

                    if (!inOpenSet.Contains(neighborPos))
                    {
                        openSet.Add((fScore[neighborPos], neighbor.X, neighbor.Y));
                        inOpenSet.Add(neighborPos);
                    }
                }
            }
        }

        return new List<GameNode>();
    }

    public int GetAStarDistance(GameNode start, GameNode goal, bool aggressiveDisrupt)
    {
        // if no path found return int.MaxValue
        var path = FindAStarPath(start, goal, aggressiveDisrupt);
        return path.Count == 0 ? int.MaxValue : path.Count - 1;
    }
}

#endregion

public class Strategy
{
    public const int PaintPoints = 3;
    public const int DisruptionPoints = 1;

    private readonly GameArena _arena;

    public Strategy(GameArena arena)
    {
        Actions = new List<IAction>();
        _arena = arena;
    }

    public List<IAction> Actions { get; }

    public void StrategyA()
    {
        var aggressiveDisrupt = _arena.turns > 50;

        // Connect towns by shortest distance first
        // Then disrupt enemy regions if possible
        DebugLog.CHECKPOINT("StrategyA");
        ConnectByShortestDistance();
        DebugLog.CHECKPOINT("ConnectByShortestDistance");
        DisruptIfWeCan();
        DebugLog.CHECKPOINT("DisruptIfWeCan");
        WaitIfNoActions();
        DebugLog.CHECKPOINT("WaitIfNoActions");
    }

    private void WaitIfNoActions()
    {
        if (Actions.Count == 0)
        {
            Actions.Add(new WaitAction());
            DebugLog.INFO("No actions possible, waiting.");
        }
    }

    private void DisruptIfWeCan()
    {
        var myTracks = _arena.Nodes.Where(c => c.TracksOwner == _arena.MyId).ToList();

        var enemyRegions = _arena.GetEnemyPriorityRegions();
        var myRegions = myTracks.Select(t => t.RegionId).Distinct().ToList();

        foreach (var enemyRegion in enemyRegions) DebugLog.DEBUG($"Enemy region candidate: {enemyRegion}");

        // find regions where enemy has tracks but we don't
        var candidateRegions = enemyRegions.Except(myRegions).ToList();
        candidateRegions.ForEach(x => { DebugLog.INFO($"Enemy has tracks in region {x}"); });
        foreach (var regionId in candidateRegions)
        {
            var regionNodes = _arena.Nodes.Where(n => n.RegionId == regionId).ToList();
            var regionInstability = regionNodes.First().Instability;
            var regionInked = regionNodes.First().Inked;

            if (!regionInked && regionInstability <= 3)
            {
                Actions.Add(new DisruptRegion(regionId.ToString()));
                DebugLog.INFO($"Disrupting region {regionId} at ({regionNodes[0].X},{regionNodes[0].Y})");
                break; // only disrupt one region per turn
            }
        }
    }

    private void ConnectByShortestDistance(bool aggressiveDisrupt = false)
    {
        var currentPoints = PaintPoints;
        var towns = _arena.Towns;


        // find town pairs with shortest distance
        var townPairs = new List<(GameNode, GameNode, int)>();
        foreach (var townA in towns)
        foreach (var townB in towns)
        {
            if (townA == townB) continue;
            if (!townA.DesiredConnections.Contains(townB.TownId)) continue;
            var distance = _arena.GetAStarDistance(townA.Location, townB.Location, aggressiveDisrupt);
            townPairs.Add((townA.Location, townB.Location, distance));
        }

        var townNodes = towns.Select(t => t.Location);

        var sortedTownPairs = townPairs.OrderBy(tp => tp.Item3).ToList();
        foreach (var (startTown, endTown, distance) in sortedTownPairs)
        {
            var pathBetweenTowns = _arena.FindAStarPath(startTown, endTown, aggressiveDisrupt);

            // place tracks as long as we have points
            foreach (var gameNode in pathBetweenTowns)
            {
                if (townNodes.Contains(gameNode) || gameNode.TracksOwner != -1) continue;

                if (gameNode.PaintCost <= currentPoints)
                {
                    Actions.Add(new PlaceTracks { X = gameNode.X, Y = gameNode.Y });
                    // DebugLog.INFO($"Placed track {gameNode.X},{gameNode.Y}");
                    currentPoints -= gameNode.PaintCost;
                    // DebugLog.INFO($"Paint cost: {gameNode.PaintCost}");
                }
                else
                {
                    // DebugLog.INFO($"Not enough paint points to place track at {gameNode.X},{gameNode.Y}");
                    break;
                }
            }
        }
    }


    public void Play()
    {
        DebugLog.INFO("PLAYING");
        // Actions must be separated by a semicolon ; and must on of the following:
        //
        // PLACE_TRACKS x y : place a track on a free cell.
        //     AUTOPLACE fromX fromY toX toY : automatically generates a list of actions for the cheapest path from from to to in terms of paint points. This will do nothing if a path already exists.
        //     The generated actions replace this command.
        //     WAIT : do nothing.

        var combinedActions = string.Join(";", Actions.Select(a => a.ToString()));
        Console.WriteLine(combinedActions);
    }
}

public interface IAction
{
    public string ToString();
}

public class DisruptCellAction : IAction
{
    public DisruptCellAction(int x, int y)
    {
        X = x;
        Y = y;
    }

    public required int X { get; init; }
    public required int Y { get; init; }


    public override string ToString()
    {
        return $"DISRUPT {X} {Y}";
    }
}

public class DisruptRegion : IAction
{
    private readonly string _region;

    public DisruptRegion(string region)
    {
        _region = region;
    }

    public override string ToString()
    {
        return $"DISRUPT {_region}";
    }
}

public class WaitAction : IAction
{
    public override string ToString()
    {
        return "WAIT";
    }
}

public class PlaceTracks : IAction
{
    public required int X { get; init; }
    public required int Y { get; init; }

    public override string ToString()
    {
        return $"PLACE_TRACKS {X} {Y}";
    }

    public void Play()
    {
        Console.WriteLine(ToString());
    }
}

# region Game

/**
 * Connect towns with your train tracks and disrupt the opponent's.
 */
internal class Player
{
    private static void Main(string[] args)
    {
        // string[] inputs;
        // var myId = int.Parse(Console.ReadLine()); // 0 or 1
        // var width = int.Parse(Console.ReadLine()); // map size
        // var height = int.Parse(Console.ReadLine());
        // for (var i = 0; i < height; i++)
        // for (var j = 0; j < width; j++)
        // {
        //     inputs = Console.ReadLine().Split(' ');
        //     var regionId = int.Parse(inputs[0]);
        //     var type = int.Parse(inputs[1]); // 0 (PLAINS), 1 (RIVER), 2 (MOUNTAIN), 3 (POI)
        // }


        // var townCount = int.Parse(Console.ReadLine());
        // for (var i = 0; i < townCount; i++)
        // {
        //     inputs = Console.ReadLine().Split(' ');
        //     var townId = int.Parse(inputs[0]);
        //     var townX = int.Parse(inputs[1]);
        //     var townY = int.Parse(inputs[2]);
        //     var desiredConnections = inputs[3]; // comma-separated town ids e.g. 0,1,2,3
        // }
        var arena = new GameArena();

        // game loop
        while (true)
        {
            arena.turns++;
            arena.UpdateScores();
            // for (var i = 0; i < arena.Height; i++)
            // for (var j = 0; j < arena.Width; j++)
            // {
            //     inputs = Console.ReadLine().Split(' ');
            //     var tracksOwner = int.Parse(inputs[0]);
            //     var instability = int.Parse(inputs[1]); // region inked (destroyed) when this >= 3.
            //     var inked = inputs[2] != "0"; // true if region is destroyed.
            //     var
            //         partOfActiveConnections =
            //             inputs
            //                 [3]; // if this cell is part of one or more railway connections, this will be town ids (separated by -) in a list separated by commas. e.g. 0-1,1-2,1-3. "x" otherwise.
            // }
            arena.UpdateCellStatus();

            // Write an action using Console.WriteLine()
            // To debug: Console.Error.WriteLine("Debug messages...");


            // AUTOPLACE x1 y1 x2 | PLACE_TRACKS x y | DISRUPT regionId | MESSAGE text
            var strategy = new Strategy(arena);
            strategy.StrategyA();
            strategy.Play();
            // Console.WriteLine("WAIT");
        }
    }
}

#endregion