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

public static class DebugLog
{
    private static readonly bool InfoEnabled = false;
    private static readonly bool DebugEnabled = false;

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

public static class Values
{
    public static readonly HashSet<string> ProteinTypes =
        new() { EntityType.A, EntityType.B, EntityType.C, EntityType.D };

    public static readonly HashSet<string> OrganismTypes =
        new() { EntityType.Basic, EntityType.Root, EntityType.Tentacle, EntityType.Harvester, EntityType.Sporer };
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

public class GameNode : Coordinate
{
    public GameEntity? GameEntity;
    public bool IsEmpty => GameEntity == null;

    public override string ToString()
    {
        return $"Node[X={X}, Y={Y}, Type={GameEntity?.Type ?? "EMPTY"}]";
    }
}

public class GameEntity : Coordinate
{
    public required string Type { get; init; }
    public required string OrganDir { get; init; }
    public required int OrganId { get; init; }
    public required int OrganParentId { get; init; }
    public required int OrganRootId { get; init; }
    public required int Owner { get; init; }

    public bool IsProteinSource => Values.ProteinTypes.Contains(Type);

    public bool IsOrganism => Values.OrganismTypes.Contains(Type);

    public bool IsWall => Type == EntityType.Wall;

    public override string ToString()
    {
        return $"Entity[X={X}, Y={Y}, Type={Type}, Owner={Owner}, OrganId={OrganId}]";
    }
}

public static class EntityType
{
    public const string Wall = "WALL";
    public const string Root = "ROOT";
    public const string Basic = "BASIC";
    public const string Tentacle = "TENTACLE";
    public const string Harvester = "HARVESTER";
    public const string Sporer = "SPORER";
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
}

public class Resources
{
    public readonly int A;
    public readonly int B;
    public readonly int C;
    public readonly int D;


    public Resources(int a, int b, int c, int d)
    {
        A = a;
        B = b;
        C = c;
        D = d;
    }
}

#region GameArena

public class GameArena
{
    private readonly GameNode[,] _grid;

    public ReadOnlyCollection<GameNode> Entities = Array.Empty<GameNode>().AsReadOnly();
    public Resources MyResources;
    public Resources OppResources;

    public GameArena()
    {
        string[] inputs;
        inputs = Console.ReadLine()!.Split(' ');
        var width = int.Parse(inputs[0]); // columns in the game grid
        var height = int.Parse(inputs[1]); // rows in the game grid

        Width = width;
        Height = height;
        _grid = new GameNode[width, height];
        ReInitializeGrid();
        DebugLog.INFO("Arena Initialized");
    }


    public ReadOnlyCollection<GameNode> ProteinSources =>
        Entities.Where(e => e?.GameEntity is not null && e.GameEntity.IsProteinSource)
            .ToList().AsReadOnly() ?? new ReadOnlyCollection<GameNode>([]);

    public ReadOnlyCollection<GameEntity> MyOrganisms => Entities
        .Where(e => e?.GameEntity is not null && e.GameEntity.IsOrganism && e.GameEntity.Owner == 1)
        .Select(e => e.GameEntity!)
        .ToList().AsReadOnly() ?? new ReadOnlyCollection<GameEntity>([]);

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

    public GameNode GetNode(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            DebugLog.INFO($"Arena Invalid coordinate requested: ({x},{y})");
            throw new ArgumentOutOfRangeException();
        }

        return _grid[x, y];
    }

    private void ReInitializeGrid()
    {
        for (var x = 0; x < Width; x++)
        for (var y = 0; y < Height; y++)
            _grid[x, y] = new GameNode
            {
                X = x,
                Y = y,
                GameEntity = null
            };
    }


    private static GameNode[] ParseEntities()
    {
        string[] inputs;
        var entityCount = int.Parse(Console.ReadLine());
        var entities = new GameNode[entityCount];

        for (var i = 0; i < entityCount; i++)
        {
            inputs = Console.ReadLine()!.Split(' ');
            var x = int.Parse(inputs[0]);
            var y = int.Parse(inputs[1]); // grid coordinate
            var type = inputs[2]; // WALL, ROOT, BASIC, TENTACLE, HARVESTER, SPORER, A, B, C, D
            var owner = int.Parse(inputs[3]); // 1 if your organ, 0 if enemy organ, -1 if neither
            var organId = int.Parse(inputs[4]); // id of this entity if it's an organ, 0 otherwise
            var organDir = inputs[5]; // N,E,S,W or X if not an organ
            var organParentId = int.Parse(inputs[6]);
            var organRootId = int.Parse(inputs[7]);

            entities[i] =
                new GameNode
                {
                    X = x,
                    Y = y,
                    GameEntity = new GameEntity
                    {
                        X = x,
                        Y = y,
                        Type = type,
                        Owner = owner,
                        OrganId = organId,
                        OrganDir = organDir,
                        OrganParentId = organParentId,
                        OrganRootId = organRootId
                    }
                };
        }

        DebugLog.INFO($"Entities Parsed: {entityCount}");
        return entities;
    }

    public void UpdateEntities()
    {
        Entities = ParseEntities().AsReadOnly();
        DebugLog.DEBUG($"Entities Set: {Entities.Count}");
        DebugLog.DEBUG($"My Organisms Count: {MyOrganisms.Count}");
        DebugLog.DEBUG($"Protein Sources Count: {ProteinSources.Count}");
        ReInitializeGrid();

        foreach (var nodeGameEntity in Entities)
        {
            var node = GetNode(nodeGameEntity.X, nodeGameEntity.Y);
            node.GameEntity = nodeGameEntity.GameEntity;
        }

        DebugLog.INFO("Arena Updated with Entities");
    }

    public void UpdateResources()
    {
        string[] inputs;
        inputs = Console.ReadLine()!.Split(' ');
        var myA = int.Parse(inputs[0]);
        var myB = int.Parse(inputs[1]);
        var myC = int.Parse(inputs[2]);
        var myD = int.Parse(inputs[3]); // your protein stock
        inputs = Console.ReadLine()!.Split(' ');
        var oppA = int.Parse(inputs[0]);
        var oppB = int.Parse(inputs[1]);
        var oppC = int.Parse(inputs[2]);
        var oppD = int.Parse(inputs[3]); // opponent's protein stock

        MyResources = new Resources(myA, myB, myC, myD);
        OppResources = new Resources(oppA, oppB, oppC, oppD);
        DebugLog.INFO("Arena Updated with Resources");
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

    public List<GameNode> FindFbsPath(GameNode start, GameNode goal)
    {
        var visited = new bool[Width, Height];
        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var queue = new Queue<GameNode>();
        queue.Enqueue(start);
        visited[start.X, start.Y] = true;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.X == goal.X && current.Y == goal.Y)
            {
                // Reconstruct path
                var path = new List<GameNode>();
                var pos = (goal.X, goal.Y);
                while (pos != (start.X, start.Y))
                {
                    var node = GetNode(pos.Item1, pos.Item2);
                    path.Add(node);
                    pos = cameFrom[pos];
                }

                path.Add(start);
                path.Reverse();
                return path;
            }

            foreach (var neighbor in GetNeighbors(current))
            {
                int nx = neighbor.X, ny = neighbor.Y;
                if (visited[nx, ny] ||
                    (((neighbor.GameEntity is not null && neighbor.GameEntity.IsWall) ||
                      (neighbor.GameEntity is not null && neighbor.GameEntity.IsOrganism)) &&
                     !(nx == goal.X && ny == goal.Y)))
                    continue;

                visited[nx, ny] = true;
                cameFrom[(nx, ny)] = (current.X, current.Y);
                queue.Enqueue(neighbor);
            }
        }

        DebugLog.INFO("No path found from (" + start.X + "," + start.Y + ") to (" + goal.X + "," + goal.Y + ")");
        return new List<GameNode>(); // No path found
    }


    public int GetFsbDistance(GameNode start, GameNode goal)
    {
        // if no path found return int.MaxValue
        var path = FindFbsPath(start, goal);
        return path.Count == 0 ? int.MaxValue : path.Count - 1;
    }
}

#endregion

#region PlayerActions

public abstract class Action
{
    public abstract void Play();
}

public class ActionSlot
{
    public required Action Action { get; set; }

    public void SetWaitAction()
    {
        Action = new WaitAction();
    }

    public void SetHarvestAction(GameEntity organ, GameNode target, List<GameNode> path)
    {
        var nextNode = path[1];
        // get coordinates in front of organ based on its direction
        var direction = nextNode.GetDirectionTo(target);

        Action = new HarvestAction(organ.OrganId, nextNode.X, nextNode.Y, direction);
    }

    public void SetGrowAction(int id, int x, int y, string type)
    {
        Action = new GrowAction(id, x, y, type);
    }
}

public class PlayerActions
{
    private readonly ActionSlot[] _actionSlots;

    public PlayerActions(int requiredActionsCount)
    {
        _actionSlots = new ActionSlot[requiredActionsCount];
        for (var i = 0; i < requiredActionsCount; i++) _actionSlots[i] = new ActionSlot { Action = new WaitAction() };
    }

    public IReadOnlyList<ActionSlot> ActionSlots => _actionSlots.AsReadOnly();

    public void Play()
    {
        foreach (var actionSlot in _actionSlots) actionSlot.Action.Play();
    }
}

public class WaitAction : Action
{
    public override void Play()
    {
        Console.WriteLine("WAIT");
    }
}

public class GrowAction : Action
{
    private readonly int _id;
    private readonly string _type;
    private readonly int _x;
    private readonly int _y;

    public GrowAction(int id, int x, int y, string type)
    {
        if (type != EntityType.Basic &&
            type != EntityType.Harvester &&
            type != EntityType.Root &&
            type != EntityType.Sporer &&
            type != EntityType.Tentacle)
        {
            DebugLog.INFO($"Invalid type for GrowAction: {type}");
            throw new ArgumentException($"Invalid type for GrowAction: {type}");
        }

        _id = id;
        _x = x;
        _y = y;
        _type = type;
    }

    public override void Play()
    {
        Console.WriteLine($"GROW {_id} {_x} {_y} {_type}");
    }
}

public class HarvestAction : Action
{
    private readonly string _direction;
    private readonly int _id;
    private readonly string _type;
    private readonly int _x;
    private readonly int _y;

    public HarvestAction(int id, int x, int y, string direction)
    {
        _id = id;
        _x = x;
        _y = y;
        _direction = direction;
    }

    public override void Play()
    {
        Console.WriteLine($"GROW {_id} {_x} {_y} HARVESTER {_direction}");
    }
}

#endregion

#region Strategies

public class Strategies
{
    private readonly GameArena _arena;
    private readonly PlayerActions _player;

    public Strategies(GameArena arena, PlayerActions player)
    {
        _arena = arena;
        _player = player;
    }

    public PlayerActions Strategy_GrowAndHarvest()
    {
        DebugLog.INFO("GROW AND HARVEST");
        GrowTowardsClosestProteinSource();
        HarvestIfPossible();
        GrowInAnyDirectionIfNothingElse();
        return _player;
    }


    private PlayerActions GrowInAnyDirectionIfNothingElse()
    {
        DebugLog.INFO("GROW IN ANY DIRECTION");
        var myOrganisms = _arena.MyOrganisms;
        // find any organism to grow
        foreach (var organism in myOrganisms)
            // grow towards
        foreach (var actionSlot in _player.ActionSlots.Where(x => x.Action is WaitAction))
        {
            var directions = new[] { "N", "E", "S", "W" };
            foreach (var direction in directions)
            {
                var nextNode = organism.GetCoordinateInDirection(direction);
                // check if nextNode is empty
                var node = _arena.GetNode(nextNode.X, nextNode.Y);
                if (node.IsEmpty)
                {
                    actionSlot.SetGrowAction(organism.OrganId, nextNode.X, nextNode.Y, EntityType.Basic);
                    DebugLog.INFO($"GROW IN ANY DIRECTION - SET: {direction}");
                    return _player;
                }
            }
        }

        DebugLog.INFO("GROW IN ANY DIRECTION NOT POSSIBLE");
        return _player;
    }

    private PlayerActions GrowTowardsClosestProteinSource()
    {
        DebugLog.INFO("GROW TOWARDS CLOSEST PROTEIN SOURCE");
        var myOrganisms = _arena.MyOrganisms;
        var proteinSources = _arena.ProteinSources;

        DebugLog.DEBUG($"My Organisms Count: {myOrganisms.Count}");
        DebugLog.DEBUG($"Protein Sources Count: {proteinSources.Count}");

        var shortestPath = new List<GameNode>();

        // find closest organism to any protein source
        foreach (var organism in myOrganisms)
        {
            DebugLog.DEBUG($"Organism: {organism}");
            foreach (var proteinSource in proteinSources)
            {
                var organNode = _arena.GetNode(organism.X, organism.Y);
                var proteinNode = _arena.GetNode(proteinSource.X, proteinSource.Y);

                if (shortestPath.Count == 0)
                {
                    shortestPath = _arena.FindFbsPath(organNode, proteinNode);
                    DebugLog.DEBUG(
                        $"Distance: {shortestPath.Count} from Organism({organism}) to ProteinSource({proteinSource})");
                    continue;
                }

                var path = _arena.FindFbsPath(organNode, proteinNode);
                DebugLog.DEBUG(
                    $"Distance: {shortestPath.Count} from Organism({organism}) to ProteinSource({proteinSource})");
                if (path.Count < shortestPath.Count && path.Count > 0)
                {
                    shortestPath = path;
                    DebugLog.DEBUG(
                        $"NEW SHORTEST PATH Distance: {shortestPath.Count} from Organism({organism}) to ProteinSource({proteinSource})");
                }
            }
        }

        foreach (var gameNode in shortestPath) DebugLog.DEBUG(gameNode.ToString());

        if (shortestPath.Count > 2)
            // grow towards
            foreach (var actionSlot in _player.ActionSlots)
            {
                var nextNode = shortestPath[1];
                var organismNode = shortestPath[0];
                actionSlot.SetGrowAction(organismNode.GameEntity.OrganId, nextNode.X, nextNode.Y, EntityType.Basic);
                DebugLog.INFO($"GROW TOWARDS CLOSEST PROTEIN SOURCE - SET TO: ({nextNode})");
                return _player;
            }

        DebugLog.INFO("NO GROWTH POSSIBLE TOWARDS PROTEIN SOURCE");
        return _player;
    }

    private PlayerActions HarvestIfPossible()
    {
        var canHarvest = _arena.MyResources.C > 0 && _arena.MyResources.D > 0;
        if (!canHarvest)
        {
            DebugLog.INFO("CANNOT HARVEST - INSUFFICIENT RESOURCES");
            return _player;
        }

        DebugLog.INFO("HARVEST IF POSSIBLE");
        var myOrganisms = _arena.MyOrganisms;
        var proteinSources = _arena.ProteinSources;
        // find closest organism to any protein source
        foreach (var organism in myOrganisms)
        foreach (var proteinSource in proteinSources)
        {
            var organNode = _arena.GetNode(organism.X, organism.Y);
            var path = _arena.FindFbsPath(organNode, proteinSource);
            if (path.Count == 3)
                // harvest
                foreach (var actionSlot in _player.ActionSlots)
                {
                    actionSlot.SetHarvestAction(organism, proteinSource, path);
                    DebugLog.INFO($"HARVEST IF POSSIBLE - SET TO HARVEST FROM: ({proteinSource})");
                    return _player;
                }
        }

        DebugLog.INFO("HARVEST NOT POSSIBLE");
        return _player;
    }
}

#endregion

public class Game
{
    private static void Main(string[] args)
    {
        var arena = new GameArena();

        // game loop
        while (true)
        {
            arena.UpdateEntities();
            arena.UpdateResources();

            var requiredActionsCount =
                int.Parse(Console.ReadLine()!); // your number of organisms, output an action for each one in any order

            var actionPlayer = new PlayerActions(requiredActionsCount);
            actionPlayer = new Strategies(arena, actionPlayer).Strategy_GrowAndHarvest();
            actionPlayer.Play();
        }
        // ReSharper disable once FunctionNeverReturns
    }
}