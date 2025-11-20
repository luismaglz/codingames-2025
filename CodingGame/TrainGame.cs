// ReSharper disable RedundantUsingDirective
// ReSharper disable CheckNamespace
// 36.7

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

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
    private static readonly bool InfoEnabled = false;
    private static readonly bool DebugEnabled = false;
    private static readonly bool CheckPointEnabled = false;

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

public class Connection
{
    public List<GameNode> Nodes = new();

    public bool IsActive { get; set; }

    // Indicates it has the highest score index of all connections between the two towns
    public bool HasHighestScoreIndex { get; set; }
    public bool HasHighestDisadvantageIndex { get; set; }

    // Positive values means my advantage, negative means opponent advantage
    public int ScoreIndex => MyScore - OppScore;

    public int OppScore
    {
        get { return Nodes.Count(n => n.TracksOwner == GameArena.OppId); }
    }

    public int MyScore
    {
        get { return Nodes.Count(n => n.TracksOwner == GameArena.MyId); }
    }

    private Owner Owner
    {
        get
        {
            var owners = Nodes.Select(n => n.TracksOwner).Distinct().ToList();
            var hasMe = owners.Contains(GameArena.MyId);
            var hasOpp = owners.Contains(GameArena.OppId);

            if (hasMe && hasOpp)
                return Owner.Shared;
            if (hasMe)
                return Owner.Me;
            if (hasOpp)
                return Owner.Opponent;
            return Owner.None;
        }
    }

    public List<Region> DisruptableRegions
    {
        get
        {
            return Nodes.Select(n => n.RegionId)
                .Distinct()
                .Where(id => GameArena.Regions.ContainsKey(id))
                .Select(id => GameArena.Regions[id])
                .Where(r => !r.ContainsTown && !r.Inked)
                .ToList();
        }
    }

    public List<Region> Regions
    {
        get
        {
            return Nodes.Select(n => n.RegionId)
                .Distinct()
                .Select(id => GameArena.Regions[id])
                .ToList();
        }
    }
}

public enum Owner
{
    None = -1,
    Me = 0,
    Opponent = 1,
    Shared = 2
}

public class Region
{
    private bool _containsTown;

    public Region(int regionId)
    {
        RegionId = regionId;
    }

    public int RegionId { get; }
    public int Instability { get; set; }
    public bool Inked { get; set; }
    public List<GameNode> Cells { get; } = new();

    public bool ContainsTown
    {
        get => _containsTown;
        set
        {
            if (value)
                _containsTown = true;
            // Ignore attempts to set to false
        }
    }

    public Owner Owner { get; private set; } = Owner.None;


    public int GetRegionScore(Region region, int instabilityWeight = 2, int cellCountWeight = 1)
    {
        return instabilityWeight * region.Instability + cellCountWeight * region.Cells.Count;
    }

    public void UpdateFromCell(GameNode cell)
    {
        Instability = cell.Instability > Instability ? cell.Instability : Instability;
        Inked = cell.Inked;
        Cells.Add(cell);

        var owners = Cells.Select(c => c.TracksOwner).Distinct().ToList();
        var hasMe = owners.Contains(GameArena.MyId);
        var hasOpp = owners.Contains(GameArena.OppId);

        if (hasMe && hasOpp)
            Owner = Owner.Shared;
        else if (hasMe)
            Owner = Owner.Me;
        else if (hasOpp)
            Owner = Owner.Opponent;
        else
            Owner = Owner.None;
    }

    public override string ToString()
    {
        return
            $"Region[Id={RegionId}, Instability={Instability}, Inked={Inked}, CellCount={Cells.Count}] Owner={Owner}";
    }
}

internal class ConnectionTracker
{
    private int _totalInstability;
    public List<GameNode> EnemyNodes = new();

    public List<GameNode> MyNodes = new();

    public HashSet<int> RegionIds = new();
    public List<GameNode> SharedNodes = new();

    public int TotalInstability
    {
        set
        {
            if (value > _totalInstability) _totalInstability = value;
        }

        get => _totalInstability;
    }


    public int EnemyPoints => EnemyNodes.Count;
    public int MyPoints => MyNodes.Count;
    public int ConnectionSize => EnemyNodes.Count + MyNodes.Count + SharedNodes.Count;

    public int ConnectionPoints => EnemyPoints - MyPoints;

    public int KillPriority()
    {
        if (EnemyPoints > MyPoints) return EnemyPoints - MyPoints + TotalInstability;

        return -1;
    }

    public override string ToString()
    {
        return
            $"ConnectionTracker(EnemyPoints={EnemyPoints}, MyPoints={MyPoints}, ConnectionPoints={ConnectionPoints}, TotalInstability={TotalInstability}) Regions=[{string.Join(",", RegionIds)}]";
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

    public List<string> Connections => PartOfActiveConnections.Split(',').ToList();

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
        return $"Node[X={X}, Y={Y}] Owner:{TracksOwner} Instability:{Instability} Inked:{Inked} RegionId:{RegionId}]";
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
    public static int MyId;
    public static int OppId;
    public static int SharedId = 2;
    public static Dictionary<int, Region> Regions = new();

    private readonly GameNode[,] _grid;

    public Dictionary<string, Connection> ActiveConnections = new();


    public Dictionary<string, List<GameNode>> PathCache = new();

    public List<int> RegionsCannotDisrupt = new();

    public GameArena()
    {
        string[] inputs;
        var myId = int.Parse(Console.ReadLine()); // 0 or 1
        var width = int.Parse(Console.ReadLine()); // map size
        var height = int.Parse(Console.ReadLine());
        Width = width;
        Height = height;
        _grid = new GameNode[width, height];
        MyId = myId;
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

    public void ResetOnEveryRound()
    {
        Regions = new Dictionary<int, Region>();
        PathCache = new Dictionary<string, List<GameNode>>();
        ActiveConnections = new Dictionary<string, Connection>();
        RegionsCannotDisrupt = new List<int>();
    }

    public Region? FindBestRegionToDisrupt(List<GameNode> nodes)
    {
        var regionToDisrupt = nodes
            .Select(n => n.RegionId)
            .Distinct()
            .Where(id => Regions.ContainsKey(id))
            .Select(id => Regions[id])
            .Where(r => !r.ContainsTown && !r.Inked)
            .OrderByDescending(r => r.GetRegionScore(r))
            .FirstOrDefault();
        return regionToDisrupt;
    }

    public List<Region> GetPathRegions(List<GameNode> nodes, GameNode start, GameNode goal)
    {
        var startRegion = Regions[start.RegionId];
        var goalRegion = Regions[goal.RegionId];

        var regions = nodes
            .Select(n => n.RegionId)
            .Distinct()
            .Where(id => Regions.ContainsKey(id) && id != startRegion.RegionId && id != goalRegion.RegionId)
            .Select(id => Regions[id])
            .ToList();
        return regions;
    }

    public List<GameNode> GetRegionAdjacentNodes(List<GameNode> nodes, GameNode start, GameNode goal)
    {
        var regions = nodes.Select(n => n.RegionId);

        // exclude the region of start and goal
        regions = regions.Where(rid => rid != start.RegionId && rid != goal.RegionId);

        // return all nodes in those regions
        var adjacentNodes = Nodes.Where(n => regions.Contains(n.RegionId)).ToList();
        return adjacentNodes;
    }


    public List<GameNode> NodesWithActiveConnectionsByOwner(int ownerId)
    {
        // get the cells that are parts of completed connections between towns
        // get the regions of those cells
        var nodes = new List<GameNode>();
        foreach (var node in Nodes)
            if (node.PartOfActiveConnections != "x" && node.TracksOwner == ownerId)
                nodes.Add(node);


        return nodes;
    }


    public List<int> GetRegionsOfDisputedConnections()
    {
        // get all connections that have enemy nodes
        var enemyNodesPartOfActiveConnections = NodesWithActiveConnectionsByOwner(OppId);
        var myNodesPartOfActiveConnections = NodesWithActiveConnectionsByOwner(MyId);

        Dictionary<string, ConnectionTracker> connectionTrackers = new();

        // Fill enemy nodes
        foreach (var node in enemyNodesPartOfActiveConnections)
        foreach (var connection in node.Connections)
        {
            if (!connectionTrackers.ContainsKey(connection))
                connectionTrackers[connection] = new ConnectionTracker();
            connectionTrackers[connection].RegionIds.Add(node.RegionId);
            connectionTrackers[connection].EnemyNodes.Add(node);
            connectionTrackers[connection].TotalInstability = node.Instability;
        }


        // Fill my nodes
        foreach (var node in myNodesPartOfActiveConnections)
        foreach (var connection in node.Connections)
        {
            if (!connectionTrackers.ContainsKey(connection))
                connectionTrackers[connection] = new ConnectionTracker();
            connectionTrackers[connection].RegionIds.Add(node.RegionId);
            connectionTrackers[connection].MyNodes.Add(node);
        }

        foreach (var connectionTrackersValue in connectionTrackers.Values)
            DebugLog.DEBUG(connectionTrackersValue.ToString());

        // connections with a kill priority > 0
        var connectionsToKill = connectionTrackers.Values.ToList().Where(ct => ct.KillPriority() > 0).ToList();

        var connectionWithMostPoints = connectionsToKill.OrderByDescending(ct => ct.KillPriority()).FirstOrDefault();

        if (connectionWithMostPoints == null)
            return new List<int>();

        var connectionRegions = connectionWithMostPoints.RegionIds;

        //sort regions by instability
        var sortedRegions = connectionRegions.ToList();
        sortedRegions.Sort((a, b) =>
        {
            var aInstability = Nodes.First(n => n.RegionId == a).Instability;
            var bInstability = Nodes.First(n => n.RegionId == b).Instability;
            return bInstability.CompareTo(aInstability);
        });

        return sortedRegions;
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

            // Update Regions dictionary
            if (!Regions.ContainsKey(cell.RegionId))
                Regions[cell.RegionId] = new Region(cell.RegionId);
            Regions[cell.RegionId].UpdateFromCell(cell);
        }

        foreach (var town in Towns)
        {
            var node = town.Location;
            Regions[node.RegionId].ContainsTown = true;
            RegionsCannotDisrupt.Add(node.RegionId);
        }
    }

    public int GetOriginFromConnectionKey(string connectionKey)
    {
        var parts = connectionKey.Split('-');
        return int.Parse(parts[0]);
    }

    public int GetTargetFromConnectionKey(string connectionKey)
    {
        var parts = connectionKey.Split('-');
        return int.Parse(parts[1]);
    }

    public string CreateConnectionKey(int townId, int targetId)
    {
        return $"{townId}-{targetId}";
    }

    public void FillConnections()
    {
        // Filled active dictionary
        foreach (var cell in Nodes)
            if (cell.PartOfActiveConnections != "x")
            {
                var connections = cell.PartOfActiveConnections.Split(',');
                foreach (var conn in connections)
                    if (!ActiveConnections.ContainsKey(conn))
                    {
                        var connection = new Connection();
                        connection.Nodes = new List<GameNode>();
                        connection.IsActive = true;
                        connection.Nodes.Add(cell);
                        ActiveConnections[conn] = connection;
                    }
                    else
                    {
                        ActiveConnections[conn].Nodes.Add(cell);
                    }
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

    // public List<(GameNode, GameNode, int, List<GameNode>)> SortTownPairs(
    //     List<(GameNode, GameNode, int, List<GameNode>)> townPairs)
    // {
    //     // Step 1: Find shortest paths for each pair
    //     var pathDict = new Dictionary<(GameNode, GameNode), List<GameNode>>();
    //     foreach (var (start, end, _, path) in townPairs) pathDict[(start, end)] = path;
    //
    //     // Step 2: Count shared nodes for each path
    //     var nodeUsage = new Dictionary<GameNode, int>();
    //     foreach (var path in pathDict.Values)
    //     foreach (var node in path)
    //     {
    //         if (!nodeUsage.ContainsKey(node))
    //             nodeUsage[node] = 0;
    //         nodeUsage[node]++;
    //     }
    //
    //     var pairSharedCount = new Dictionary<(GameNode, GameNode), int>();
    //     foreach (var kvp in pathDict)
    //     {
    //         var shared = kvp.Value.Sum(n => nodeUsage[n]) - kvp.Value.Count; // exclude self-count
    //         pairSharedCount[kvp.Key] = shared;
    //     }
    //
    //     // Step 3: Sort by cost, then by shared node count (descending)
    //     var sorted = townPairs
    //         .OrderBy(tp => tp.Item3)
    //         .ThenByDescending(tp => pairSharedCount[(tp.Item1, tp.Item2)])
    //         .ToList();
    //
    //     return sorted;
    // }

    public List<(GameNode, GameNode, int, List<GameNode>)> SortTownPairs(
        List<(GameNode, GameNode, int, List<GameNode>)> townPairs)
    {
        var MaxPaintPoints = 3;
        Dictionary<(GameNode, GameNode), int> layableTracks = new();

        foreach (var (start, end, _, path) in townPairs)
        {
            var pointsLeft = MaxPaintPoints;
            var tracks = 0;
            foreach (var node in path)
                if (node.TracksOwner == -1 && pointsLeft >= node.PaintCost)
                {
                    pointsLeft -= node.PaintCost;
                    tracks++;
                }

            layableTracks[(start, end)] = tracks;
        }

        var nodeUsage = new Dictionary<GameNode, int>();
        foreach (var (_, _, _, path) in townPairs)
        foreach (var node in path)
        {
            if (!nodeUsage.ContainsKey(node))
                nodeUsage[node] = 0;
            nodeUsage[node]++;
        }

        var pairSharedCount = new Dictionary<(GameNode, GameNode), int>();
        foreach (var (start, end, _, path) in townPairs)
        {
            var shared = path.Sum(n => nodeUsage[n]) - path.Count;
            pairSharedCount[(start, end)] = shared;
        }

        // Identify "super special" and "special" paths
        var superSpecialPairs = new HashSet<(GameNode, GameNode)>();
        var specialPairs = new HashSet<(GameNode, GameNode)>();
        foreach (var (start, end, _, path) in townPairs)
        {
            var regionIds = path.Select(n => n.RegionId).Distinct().ToList();
            if (regionIds.Count == 2)
            {
                var hasTown0 = Regions[regionIds[0]].ContainsTown;
                var hasTown1 = Regions[regionIds[1]].ContainsTown;
                if (hasTown0 && hasTown1)
                    superSpecialPairs.Add((start, end));
                else if (hasTown0 || hasTown1)
                    specialPairs.Add((start, end));
            }
        }

        var sorted = townPairs
            .OrderByDescending(tp => superSpecialPairs.Contains((tp.Item1, tp.Item2))) // highest priority
            .ThenByDescending(tp => specialPairs.Contains((tp.Item1, tp.Item2))) // next priority
            .ThenByDescending(tp => layableTracks[(tp.Item1, tp.Item2)])
            .ThenBy(tp => tp.Item3)
            .ToList();

        return sorted;
    }

    public int CalculateManhattanDistance(Coordinate a, Coordinate b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    public string CreatePathCacheKey(GameNode start, GameNode goal, List<GameNode>? nodesToExclude)
    {
        var excludePart = nodesToExclude != null
            ? string.Join(";", nodesToExclude.Select(n => $"{n.X},{n.Y}"))
            : "none";
        return $"{start.X},{start.Y}->{goal.X},{goal.Y}|exclude:{excludePart}";
    }

    public string CreatePathCacheKey(GameNode start, GameNode goal, List<Region>? regionsToExclude)
    {
        var excludePart = regionsToExclude != null
            ? string.Join(";", regionsToExclude.Select(n => $"{n.RegionId}"))
            : "none";
        return $"{start.X},{start.Y}->{goal.X},{goal.Y}|exclude:{excludePart}";
    }

    public List<GameNode> FindAStarPath(GameNode start, GameNode goal, List<Region>? regionsToAvoid = null)
    {
        var cacheKey = CreatePathCacheKey(start, goal, regionsToAvoid);
        if (PathCache.ContainsKey(cacheKey)) return PathCache[cacheKey];

        var nodesToExclude = regionsToAvoid != null
            ? Nodes.Where(n => regionsToAvoid.Select(r => r.RegionId).Contains(n.RegionId)).Distinct().ToList()
            : null;

        var path = FindAStarPath(start, goal, nodesToExclude);
        PathCache[cacheKey] = path;
        return path;
    }

    private List<GameNode> FindAStarPath(GameNode start, GameNode goal, List<GameNode>? nodesToExclude = null)
    {
        var cacheKey = CreatePathCacheKey(start, goal, nodesToExclude);
        if (PathCache.ContainsKey(cacheKey)) return PathCache[cacheKey];

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
                PathCache[cacheKey] = path;
                return path;
            }

            inOpenSet.Remove(currentPos);
            var currentNode = GetNode(currentPos.Item1, currentPos.Item2);

            foreach (var neighbor in GetNeighbors(currentNode))
            {
                if (neighbor.Inked) // Skip inked nodes
                    continue;

                var neighborPos = (neighbor.X, neighbor.Y);

                if (excludeSet.Contains(neighborPos))
                    continue;

                // Favor nodes in regions with towns
                var movementCost = neighbor.PaintCost;
                if (Regions.ContainsKey(neighbor.RegionId) && Regions[neighbor.RegionId].ContainsTown)
                    movementCost = Math.Max(1, movementCost - 1);
                var tentativeGScore = gScore[currentPos] + movementCost;
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

        PathCache[cacheKey] = new List<GameNode>();
        return new List<GameNode>();
    }

    public List<GameNode> FindAStarPathMinusExisting(GameNode start, GameNode goal)
    {
        // if no path found return int.MaxValue
        var path = FindAStarPath(start, goal, new List<Region>()).Where(n => n.TracksOwner == -1).ToList();
        return path;
    }

    public int GetPathCost(List<GameNode> path)
    {
        if (path.Count == 0) return int.MaxValue;

        var totalCost = path.Sum(n => n.PaintCost);

        // Favor fewer regions and regions with towns
        var regionIds = path.Select(n => n.RegionId).Distinct();
        var regionBonus = 0;
        foreach (var regionId in regionIds)
            // Subtract 2 for each region, subtract 3 if region has a town
            if (Regions[regionId].ContainsTown)
                regionBonus += 2;
            else
                regionBonus += 1;

        // Ensure cost doesn't go below 1
        return Math.Max(1, totalCost - regionBonus);
    }
}

#endregion

public class Strategy
{
    private readonly GameArena _arena;
    private bool DisruptionPointAvailable = true;
    private int PaintPoints = 3;

    public Strategy(GameArena arena)
    {
        Actions = new List<IAction>();
        _arena = arena;
    }

    public List<IAction> Actions { get; }

    public void StrategyA()
    {
        DebugLog.CHECKPOINT("StrategyA");
        ConnectByShortestDistance();
        DebugLog.CHECKPOINT("ConnectByShortestDistance");
        DisruptIfWeCan();


        DebugLog.CHECKPOINT("DisruptIfWeCan");
        WaitIfNoActions();
        DebugLog.CHECKPOINT("WaitIfNoActions");
    }

    public void BuildBackBetter()
    {
        DebugLog.CHECKPOINT("BuildBackBetter");

        ConnectByShortestDistance(useExistingInDistanceCalc: true, scramblePath: true);
        DebugLog.CHECKPOINT("ConnectByShortestDistance");

        DisruptIfWeCan();
        DebugLog.CHECKPOINT("DisruptIfWeCan");

        PlaceTrackInRandomEmptySquare();
        DebugLog.CHECKPOINT("PlaceRandomSquare");

        WaitIfNoActions();
    }


    public void BuildBackBetterV2()
    {
        DebugLog.CHECKPOINT("BuildBackBetterV2");

        DebugLog.CHECKPOINT("ConnectByShortestDistance Start");
        ConnectByShortestDistance(useExistingInDistanceCalc: true, scramblePath: true);
        DebugLog.CHECKPOINT("ConnectByShortestDistance End");

        DebugLog.CHECKPOINT("DisruptIfWeCan Start");
        DisruptSmartly();
        DebugLog.CHECKPOINT("DisruptIfWeCan End");

        DebugLog.CHECKPOINT("ConnectAlternativeRoutes Start");
        ConnectAlternativeRoutesV2();
        DebugLog.CHECKPOINT("ConnectAlternativeRoutes End");

        DebugLog.CHECKPOINT("PlaceRandomSquare Start");
        PlaceTrackInRandomEmptySquare();
        DebugLog.CHECKPOINT("PlaceRandomSquare End");

        WaitIfNoActions();
    }

    private void ConnectAlternativeRoutes()
    {
        DebugLog.CHECKPOINT("ConnectAlternativeRoutes");
        var townPairs = new List<(GameNode, GameNode, int, List<GameNode>)>();

        foreach (var town in _arena.Towns)
        foreach (var targetId in town.DesiredConnections)
        {
            var targetTown = _arena.Towns.FirstOrDefault(t => t.TownId == targetId);
            if (targetTown == null) continue;

            var key = _arena.CreateConnectionKey(town.TownId, targetId);
            if (_arena.ActiveConnections.ContainsKey(key))
            {
                var activeConnection = _arena.ActiveConnections[key];
                if (activeConnection != null)
                {
                    var regionsToExclude =
                        _arena.GetPathRegions(activeConnection.Nodes, town.Location, targetTown.Location);
                    var path = _arena.FindAStarPath(town.Location, targetTown.Location, regionsToExclude);
                    if (path.Count == 0) continue;
                    var cost = _arena.GetPathCost(path);
                    townPairs.Add((town.Location, targetTown.Location, cost, path));
                }
            }
        }

        var sortedTownPairs = _arena.SortTownPairs(townPairs);

        foreach (var (start, end, cost, path) in sortedTownPairs)
        {
            if (PaintPoints <= 0) break;
            var startTown = _arena.Towns.First(t => t.Location == start);
            var targetTown = _arena.Towns.First(t => t.Location == end);
            var key = _arena.CreateConnectionKey(startTown.TownId, targetTown.TownId);

            if (_arena.ActiveConnections.ContainsKey(key))
            {
                var activeConnection = _arena.ActiveConnections[key];

                if (activeConnection != null)
                    foreach (var node in path)
                    {
                        if (node.TracksOwner == -1 && PaintPoints >= node.PaintCost)
                        {
                            Actions.Add(new PlaceTracks { X = node.X, Y = node.Y });
                            PaintPoints -= node.PaintCost;
                        }

                        if (PaintPoints <= 0) break;
                    }
            }
        }
    }

    private void ConnectAlternativeRoutesV2()
    {
        // Trying to find alternative routes prioritizing connections with highest negative score index

        var townPairs = new List<(GameNode, GameNode, int, List<GameNode>)>();

        var activeConnections = _arena.ActiveConnections.Values.OrderBy(ac => ac.ScoreIndex).ToList();

        foreach (var activeConnection in activeConnections)
        {
            var townNode = activeConnection.Nodes[0];
            var town = _arena.Towns.FirstOrDefault(t => t.Location.X == townNode.X && t.Location.Y == townNode.Y);
            if (town == null) continue;
            foreach (var targetId in town.DesiredConnections)
            {
                var targetTown = _arena.Towns.FirstOrDefault(t => t.TownId == targetId);
                if (targetTown == null) continue;

                var key = _arena.CreateConnectionKey(town.TownId, targetId);
                if (_arena.ActiveConnections.ContainsKey(key))
                {
                    var regionsToExclude = activeConnection.DisruptableRegions;
                    if (regionsToExclude.Count == 0)
                        continue;
                    var mostRiskyRegion = regionsToExclude
                        .OrderByDescending(r => r.GetRegionScore(r))
                        .First();
                    var path = _arena.FindAStarPath(town.Location, targetTown.Location, [mostRiskyRegion]);
                    var cost = _arena.GetPathCost(path);
                    townPairs.Add((town.Location, targetTown.Location, cost, path));
                }
            }

            var sortedTownPairs = _arena.SortTownPairs(townPairs);

            foreach (var (start, end, cost, path) in sortedTownPairs)
            {
                if (PaintPoints <= 0) break;
                var startTown = _arena.Towns.First(t => t.Location == start);
                var targetTown = _arena.Towns.First(t => t.Location == end);

                foreach (var node in path)
                {
                    if (node.TracksOwner == -1 && PaintPoints >= node.PaintCost && node != startTown.Location &&
                        node != targetTown.Location)
                    {
                        Actions.Add(new PlaceTracks { X = node.X, Y = node.Y });
                        PaintPoints -= node.PaintCost;
                    }

                    if (PaintPoints <= 0) break;
                }
            }
        }
    }

    private void PlaceTrackInRandomEmptySquare()
    {
        // while we have points, place track on random empty square
        var rand = new Random();
        var emptySquares = _arena.Nodes.Where(n => n.TracksOwner == -1 && !n.Inked).ToList();
        var towns = _arena.Towns.Select(t => t.Location).ToList();
        emptySquares = emptySquares.Where(s => !towns.Contains(s)).ToList();
        while (PaintPoints > 0 && emptySquares.Count > 0)
        {
            var index = rand.Next(emptySquares.Count);
            var square = emptySquares[index];
            if (square.PaintCost <= PaintPoints)
            {
                Actions.Add(new PlaceTracks { X = square.X, Y = square.Y });
                PaintPoints -= square.PaintCost;
            }

            emptySquares.RemoveAt(index);
        }
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
        DebugLog.DEBUG("DisruptIfWeCan");
        if (!DisruptionPointAvailable)
        {
            DebugLog.DEBUG("Disruption point not available.");
            return;
        }

        var enemyRegions = GameArena.Regions.Values
            .Where(r => r.Owner == Owner.Opponent && !r.ContainsTown)
            .OrderByDescending(r => r.GetRegionScore(r))
            .ToList();

        foreach (var enemyRegion in enemyRegions) DebugLog.DEBUG($"Enemy region candidate: {enemyRegion}");

        foreach (var region in enemyRegions)
            if (!region.Inked)
            {
                Actions.Add(new DisruptRegion(region.RegionId.ToString()));
                DisruptionPointAvailable = false;
                DebugLog.INFO($"1 Disrupting region {region.RegionId}");
                return;
            }

        var regions = _arena.GetRegionsOfDisputedConnections();

        if (regions.Count == 0)
        {
            DebugLog.DEBUG("No disputed regions found.");
            return;
        }

        if (!DisruptionPointAvailable)
        {
            DebugLog.DEBUG("Disruption point not available.");
            return;
        }


        // Sort disputed regions by score, highest first
        var sortedDisputed = regions
            .Select(id => GameArena.Regions[id])
            .OrderByDescending(r => r.GetRegionScore(r))
            .ToList();

        foreach (var region in sortedDisputed)
            if (!region.Inked && region.Instability <= 3 && !region.ContainsTown)
            {
                Actions.Add(new DisruptRegion(region.RegionId.ToString()));
                DisruptionPointAvailable = false;
                DebugLog.INFO($"2 Disrupting disputed region {region.RegionId}");
                return;
            }
    }


    private void DisruptSmartly()
    {
        DebugLog.CHECKPOINT("DisruptSmartly");

        Dictionary<int, int> regionDisruptionDelta = new();

        foreach (var keyValuePair in _arena.ActiveConnections)
        {
            var key = keyValuePair.Key;
            var activeConnection = keyValuePair.Value;

            // Check if there is an active connection

            if (activeConnection == null)
            {
                DebugLog.DEBUG($"No active connection for {key}");
                continue;
            }

            // First we minimize disadvantage connections


            var startId = _arena.GetOriginFromConnectionKey(key);
            var endId = _arena.GetTargetFromConnectionKey(key);
            var startTown = _arena.Towns.First(t => t.TownId == startId);
            var targetTown = _arena.Towns.First(t => t.TownId == endId);
            var disruptableRegions = activeConnection.DisruptableRegions;
            if (disruptableRegions.Count == 0)
            {
                DebugLog.DEBUG($"No disruptable regions for connection {key}");
                continue;
            }

            var sortedByScoreRegions = disruptableRegions.OrderByDescending(r => r.GetRegionScore(r)).ToList();

            // Check if its worth disrupting
            if (activeConnection.ScoreIndex <= 0)
            {
                var firstRegion = sortedByScoreRegions.First();
                // get next smallest disadvantage connection
                var nextPath = _arena.FindAStarPath(startTown.Location, targetTown.Location,
                    [firstRegion]);
                var newConnection = new Connection
                {
                    Nodes = nextPath
                };

                if (newConnection.ScoreIndex > activeConnection.ScoreIndex)
                {
                    DebugLog.DEBUG(
                        $"Disrupting connection for {key} to minimize disadvantage from {activeConnection.ScoreIndex} to {newConnection.ScoreIndex}");

                    // Disrupt a region in this connection
                    var regionToDisrupt = _arena.FindBestRegionToDisrupt(activeConnection.Nodes);

                    if (regionToDisrupt is not null)
                    {
                        if (regionDisruptionDelta.ContainsKey(regionToDisrupt.RegionId))
                            regionDisruptionDelta[regionToDisrupt.RegionId] +=
                                newConnection.ScoreIndex - activeConnection.ScoreIndex;
                        else
                            regionDisruptionDelta[regionToDisrupt.RegionId] =
                                newConnection.ScoreIndex - activeConnection.ScoreIndex;
                    }
                }

                continue;
            }

            // Try to maximize advantage connections
            if (activeConnection.ScoreIndex >= 0)
            {
                var firstRegion = disruptableRegions.First();
                // get next best advantage connection
                var nextPath = _arena.FindAStarPath(startTown.Location, targetTown.Location,
                    [firstRegion]);
                var newConnection = new Connection
                {
                    Nodes = nextPath
                };

                if (newConnection.ScoreIndex > activeConnection.ScoreIndex)
                {
                    DebugLog.DEBUG(
                        $"Disrupting connection for {key} to maximize advantage from {activeConnection.ScoreIndex} to {newConnection.ScoreIndex}");

                    var regionToDisrupt = sortedByScoreRegions.FirstOrDefault();

                    if (regionToDisrupt is not null)
                    {
                        if (regionDisruptionDelta.ContainsKey(regionToDisrupt.RegionId))
                            regionDisruptionDelta[regionToDisrupt.RegionId] +=
                                newConnection.ScoreIndex - activeConnection.ScoreIndex;
                        else
                            regionDisruptionDelta[regionToDisrupt.RegionId] =
                                newConnection.ScoreIndex - activeConnection.ScoreIndex;
                    }
                }
            }
        }

        // if we have regions to disrupt from the analysis, pick the best one
        if (regionDisruptionDelta.Count > 0)
        {
            var descendingRegions = regionDisruptionDelta.OrderByDescending(kv => kv.Value);
            foreach (var keyValuePair in descendingRegions)
            {
                if (_arena.RegionsCannotDisrupt.Contains(keyValuePair.Key)) continue;
                Actions.Add(new DisruptRegion(keyValuePair.Key.ToString()));
                DebugLog.INFO(
                    $"Disrupting best analyzed region {keyValuePair.Key} with delta {keyValuePair.Value}");
                DisruptionPointAvailable = false;
                return;
            }
        }

        DisruptIfWeCan();
    }

    private void ConnectByShortestDistance(bool scramblePath = false,
        bool useExistingInDistanceCalc = false)
    {
        var towns = _arena.Towns;

        // find town pairs with shortest distance
        var townPairs = new List<(GameNode, GameNode, int, List<GameNode>)>();
        foreach (var townA in towns)
        foreach (var townB in towns)
        {
            if (townA == townB) continue;
            if (!townA.DesiredConnections.Contains(townB.TownId)) continue;
            var path = new List<GameNode>();

            if (useExistingInDistanceCalc)
                path = _arena.FindAStarPathMinusExisting(townA.Location, townB.Location);
            else
                path = _arena.FindAStarPath(townA.Location, townB.Location, new List<Region>());

            if (path.Count == 0)
                continue;

            var distance = _arena.GetPathCost(path);

            townPairs.Add((townA.Location, townB.Location, distance, path));
        }

        var townNodes = towns.Select(t => t.Location);

        // TODO : CHECK IF THIS BETTER
        // var sortedTownPairs = townPairs.OrderBy(tp => tp.Item3).ToList();

        var sortedTownPairs = _arena.SortTownPairs(townPairs);

        foreach (var (startTown, endTown, distance, path) in sortedTownPairs)
        {
            var pathBetweenTowns = path;
            if (scramblePath)
                // scramble the path 
                pathBetweenTowns = pathBetweenTowns.OrderBy(n => Guid.NewGuid()).ToList();

            // place tracks as long as we have points
            foreach (var gameNode in pathBetweenTowns)
            {
                if (gameNode.X == 9 && gameNode.Y == 8) DebugLog.DEBUG($"Node: {gameNode}");
                if (townNodes.Contains(gameNode) || gameNode.TracksOwner != -1 ||
                    townNodes.Contains(gameNode)) continue;

                if (gameNode.PaintCost <= PaintPoints)
                {
                    if (Actions.Any(a => a is PlaceTracks pt && pt.X == gameNode.X && pt.Y == gameNode.Y))
                        // already planning to place track here
                        continue;
                    Actions.Add(new PlaceTracks { X = gameNode.X, Y = gameNode.Y });
                    PaintPoints -= gameNode.PaintCost;
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
        var arena = new GameArena();

        // game loop
        while (true)
        {
            DebugLog.CHECKPOINT("Turn Start");
            arena.ResetOnEveryRound();
            DebugLog.CHECKPOINT("ResetOnEveryRound");
            arena.UpdateScores();
            DebugLog.CHECKPOINT("UpdateScores");
            arena.UpdateCellStatus();
            DebugLog.CHECKPOINT("UpdateCellStatus");
            DebugLog.CHECKPOINT("FillConnections - Start");
            arena.FillConnections();
            DebugLog.CHECKPOINT("FillConnections");

            var strategy = new Strategy(arena);
            strategy.BuildBackBetterV2();
            DebugLog.CHECKPOINT("Strategy Complete");
            strategy.Play();
        }
    }
}

#endregion