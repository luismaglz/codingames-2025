// ReSharper disable RedundantUsingDirective

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

//	Rules
// The game is played on a grid.
// 
// For the lower leagues, you need only beat the Boss in specific situations.
// 
// 
// 🔵🔴 The Organisms
// Organisms are made up of organs that take up one tile of space on the game grid.
// 
// 
// Each player starts with a ROOT type organ. In this league, your organism can GROW a new BASIC type organ on each turn in order to cover a larger area.
// 
// 
// A new organ can grow from any existing organ, onto an empty adjacent location.
// 
// 
// In order to GROW, your organism needs proteins.
// 
// In this league, you start with 10 proteins of type A. Growing 1 BASIC organ requires 1 of these proteins.
// 
// 
// You can obtain more proteins by growing an organ onto a tile of the grid containing a protein source, these are tiles with a letter in them. Doing so will grant you 3 proteins of the corresponding type.
// 
// 
// Grow more organs than the Boss to advance to the next league.
// 
// 
// You organism can receive the following command:
// 
// GROW id x y type: creates a new organ at location x, y from organ with id id. If the target location is not a neighbour of id, the organ will be created on the shortest path to x, y.

// Click to expand
//     Game Protocol
//     Initialization Input
//     First line: two integers width and height for the size of the grid.
//     Input for One Game Turn
//     First line: one integer entityCount for the number of entities on the grid.
//     Next entityCount lines: the following 7 inputs for each entity:
// x: X coordinate (0 is leftmost)
// y: Y coordinate (0 is topmost)
// type:
// WALL for a wall
// ROOT for a ROOT type organ
// BASIC for a BASIC type organ
// A for an A protein source
// owner:
// 1 if you are the owner of this organ
// 0 if your opponent owns this organ
//     -1 if this is not an organ
// organId: unique id of this entity if it is an organ, 0 otherwise
// organDir: N, W, S, or E, not used in this league
// organParentId: if it is an organ, the organId of the organ that this organ grew from (0 for ROOT organs), else 0.
//     organRootId: if it is an organ, the organId of the ROOT that this organ originally grew from, else 0.
//     Next line: 4 integers: myA,myB,myC,myD for the amount of each protein type you have.
//     Next line: 4 integers: oppA,oppB,oppC,oppD for the amount of each protein type your opponent has.
//     Next line: the integer requiredActionsCount which equals 1 in this league.
//     Output
//     A single line with your action: GROW id x y type : attempt to grow a new organ of type type at location x, y from organ with id id. If the target location is not a neighbour of id, the organ will be created on the shortest path to x, y.

public class Arena
{
    public readonly int HEIGHT;
    public readonly int WIDTH;

    private Entity[] _entities;


    private Node[] grid;

    public Entity[] MyOrgans;
    public int Owner_Me = 1;
    public Node[] Resources;

    public Arena(int width, int height)
    {
        WIDTH = width;
        HEIGHT = height;
    }

    public ReadOnlyCollection<Node> Grid => grid.AsReadOnly();

    public void UpdateArenaWithEntities(Entity[] entities)
    {
        _entities = entities;
        MyOrgans = entities.Where(e => e.Owner == Owner_Me).ToArray();

        grid = new Node[WIDTH * HEIGHT];
        for (var i = 0; i < WIDTH * HEIGHT; i++)
            grid[i] = new Node
            {
                X = i % WIDTH,
                Y = i / WIDTH,
                Type = NodeType.EMPTY,
                Occupier = null
            };

        foreach (var entity in entities)
        {
            var index = entity.Y * WIDTH + entity.X;
            grid[index].Occupier = entity;
            switch (entity.EntityType)
            {
                case EntityType.WALL:
                    grid[index].Type = NodeType.WALL;
                    break;
                case EntityType.A:
                case EntityType.B:
                case EntityType.C:
                case EntityType.D:
                    grid[index].Type = NodeType.PROTEIN_SOURCE;
                    break;
                default:
                    grid[index].Type = NodeType.ORGANISM;
                    break;
            }
        }

        Resources = UpdateResourceNodes();
    }

    public Node[] GetNodesICanGrowInto()
    {
        var myOrgans = _entities.Where(e => e.Owner == Owner_Me);
        var growableNodes = new List<Node>();

        foreach (var organ in myOrgans)
        {
            var adjacentPositions = new (int X, int Y)[]
            {
                (organ.X + 1, organ.Y),
                (organ.X - 1, organ.Y),
                (organ.X, organ.Y + 1),
                (organ.X, organ.Y - 1)
            };

            foreach (var pos in adjacentPositions)
            {
                if (pos.X < 0 || pos.X >= WIDTH || pos.Y < 0 || pos.Y >= HEIGHT)
                    continue;

                var index = pos.Y * WIDTH + pos.X;
                var node = grid[index];
                if (node.Type == NodeType.EMPTY || node.Type == NodeType.PROTEIN_SOURCE) growableNodes.Add(node);
            }
        }

        return growableNodes.ToArray();
    }

    private Node[] UpdateResourceNodes()
    {
        return grid.Where(n => n.Type == NodeType.PROTEIN_SOURCE).ToArray();
    }

    public Node[] SortNodesByDistanceTo(Node targetNode)
    {
        return grid.OrderBy(n => Utilities.CalculateDistance(n, targetNode)).ToArray();
    }

    public Node[] SortByDistanceToNodesICanGrowInto(Node[] nodesICanGrowInto)
    {
        return grid.OrderBy(n => nodesICanGrowInto.Min(growNode => Utilities.CalculateDistance(n, growNode))).ToArray();
    }

    public Node[] SortByDistanceToResourceNodes(Node[] nodesICanGrowInto)
    {
        var resourceNodes = UpdateResourceNodes();
        return grid.OrderBy(n => resourceNodes.Min(resourceNode => Utilities.CalculateDistance(n, resourceNode)))
            .ToArray();
    }
}

public static class Utilities
{
    public static int CalculateDistance((int X, int Y) a, (int X, int Y) b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    public static int CalculateDistance(BaseNode a, BaseNode b)
    {
        return CalculateDistance((a.X, a.Y), (b.X, b.Y));
    }
}

public enum NodeType
{
    EMPTY,
    WALL,
    ORGANISM,
    PROTEIN_SOURCE
}

public abstract class BaseNode
{
    public int X;
    public int Y;
}

public class Node : BaseNode
{
    public Entity Occupier;
    public NodeType Type;

    public override string ToString()
    {
        return $"Node[X={X}, Y={Y}, Type={Type}, Occupier={(Occupier != null ? Occupier.EntityType : "None")}]";
    }
}

public class Entity : BaseNode
{
    public string EntityType;
    public string OrganDir;
    public int OrganId;
    public int OrganParentId;
    public int OrganRootId;
    public int Owner;

    public override string ToString()
    {
        return $"Entity[X={X}, Y={Y}, Type={EntityType}, Owner={Owner}, OrganId={OrganId}]";
    }
}

public static class EntityType
{
    public const string WALL = "WALL";
    public const string ROOT = "ROOT";
    public const string BASIC = "BASIC";
    public const string TENTACLE = "TENTACLE";
    public const string HARVESTER = "HARVESTER";
    public const string SPORER = "SPORER";
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

public class ActionSlot
{
    public Action Action { get; set; }
}

public abstract class Action
{
    public abstract void Play();
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
    private readonly int id;
    private readonly string type;
    private readonly int x;
    private readonly int y;

    public GrowAction(int id, int x, int y, string type)
    {
        this.id = id;
        this.x = x;
        this.y = y;
        this.type = type;
    }

    public override void Play()
    {
        Console.WriteLine($"GROW {id} {x} {y} {type}");
    }
}

public static class DebugLog
{
    public static void Log(string message)
    {
        Console.Error.WriteLine(message);
    }
}

public class Strategies
{
    private readonly Arena _arena;
    private readonly Resources _myResources;
    private readonly Resources _oppResources;
    private readonly ActionPlayer _player;

    public Strategies(Resources myResources, Resources oppResources, Arena arena, ActionPlayer player)
    {
        _myResources = myResources;
        _oppResources = oppResources;
        _arena = arena;
        _player = player;
    }

    public Node GetClosestOrganToNode(Node node)
    {
        var myOrgans = _arena.MyOrgans;

        Entity closestOrgan = null;
        var closestDistance = int.MaxValue;

        foreach (var organ in myOrgans)
        {
            var distance = Utilities.CalculateDistance(organ, node);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestOrgan = organ;
            }
        }

        if (closestOrgan == null) return null;

        return new Node
        {
            X = closestOrgan.X,
            Y = closestOrgan.Y,
            Occupier = closestOrgan,
            Type = NodeType.ORGANISM
        };
    }

    public bool DoIHaveEnoughResourcesForGrowth()
    {
        return _myResources.A >= 1;
    }

    public Node? GetClosestResourceNodeToAnyOfMyOrgans()
    {
        var resourceNodes = _arena.Resources;
        var myOrgans = _arena.MyOrgans;

        Node closestResourceNode = null;
        var closestDistance = int.MaxValue;

        foreach (var resourceNode in resourceNodes)
        foreach (var organ in myOrgans)
        {
            var distance = Utilities.CalculateDistance(organ, resourceNode);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestResourceNode = resourceNode;
            }
        }

        return closestResourceNode;
    }

    public Entity GetClosestOrganToResourceNode(Node resourceNode)
    {
        var myOrgans = _arena.MyOrgans;

        Entity closestOrgan = null;
        var closestDistance = int.MaxValue;

        foreach (var organ in myOrgans)
        {
            var distance = Utilities.CalculateDistance(organ, resourceNode);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestOrgan = organ;
            }
        }

        return closestOrgan;
    }

    public Node[] GetNodesItCanGrowInto(BaseNode node)
    {
        var adjacentPositions = new (int X, int Y)[]
        {
            (node.X + 1, node.Y),
            (node.X - 1, node.Y),
            (node.X, node.Y + 1),
            (node.X, node.Y - 1)
        };

        var growableNodes = new List<Node>();

        foreach (var pos in adjacentPositions)
        {
            if (pos.X < 0 || pos.X >= _arena.WIDTH || pos.Y < 0 || pos.Y >= _arena.HEIGHT)
                continue;

            var index = pos.Y * _arena.WIDTH + pos.X;
            var gridNode = _arena.Grid[index];
            if (gridNode.Type == NodeType.EMPTY || gridNode.Type == NodeType.PROTEIN_SOURCE)
                growableNodes.Add(gridNode);
        }

        return growableNodes.ToArray();
    }

    public Node[] SortByClosesToNode(BaseNode node)
    {
        return _arena.Grid.OrderBy(n => Utilities.CalculateDistance(n, node)).ToArray();
    }

    public ActionPlayer AggresiveStratey()
    {
        var nodesICanGrowInto = _arena.GetNodesICanGrowInto();
        var nodes = _arena.SortByDistanceToNodesICanGrowInto(nodesICanGrowInto);

        DebugLog.Log($"nodes I can grow into: {nodes.Length}");
        DebugLog.Log($"Actions slots available: {_player.ActionSlotSlot.Count}");

        for (var i = 0; i < _player.ActionSlotSlot.Count; i++)
        {
            if (!DoIHaveEnoughResourcesForGrowth()) break;
            DebugLog.Log($"I have enough Resources for growth. Processing action slot {i}");

            var actionSlot = _player.ActionSlotSlot[i];
            var targetNode = nodesICanGrowInto.FirstOrDefault();

            DebugLog.Log($"targetNode {targetNode}");

            if (targetNode == null) continue;

            var growFromEntity = GetClosestOrganToNode(targetNode)?.Occupier;

            DebugLog.Log($"sourceEntity {growFromEntity}");

            if (growFromEntity == null) continue;

            actionSlot.Action = new GrowAction(growFromEntity.OrganId, targetNode.X, targetNode.Y, EntityType.BASIC);
        }


        return _player;
    }

    public ActionPlayer AggresiveStrateyV2()
    {
        var closestResourceNode = GetClosestResourceNodeToAnyOfMyOrgans();
        if (closestResourceNode == null) return _player;

        DebugLog.Log($"Closest resource node: {closestResourceNode}");

        var sourceOrgan = GetClosestOrganToResourceNode(closestResourceNode);
        DebugLog.Log($"Source organ to grow from: {sourceOrgan}");

        var nodesItCanGrowInto = GetNodesItCanGrowInto(sourceOrgan);


        DebugLog.Log($"nodes I can grow into from source organ: {nodesItCanGrowInto.Length}");

        var sortedNodes = SortByClosesToNode(closestResourceNode);

        for (var i = 0; i < _player.ActionSlotSlot.Count; i++)
        {
            if (!DoIHaveEnoughResourcesForGrowth()) break;
            DebugLog.Log($"I have enough Resources for growth. Processing action slot {i}");
            var actionSlot = _player.ActionSlotSlot[i];
            var targetNode = sortedNodes.FirstOrDefault();
            DebugLog.Log($"targetNode {targetNode}");
            if (targetNode == null) continue;
            actionSlot.Action = new GrowAction(sourceOrgan.OrganId, targetNode.X, targetNode.Y, EntityType.BASIC);
        }

        return _player;
    }
}

public class ActionPlayer
{
    private readonly ActionSlot[] actionSlots;

    public ActionPlayer(int requiredActionsCount)
    {
        actionSlots = new ActionSlot[requiredActionsCount];
        for (var i = 0; i < requiredActionsCount; i++) actionSlots[i] = new ActionSlot { Action = new WaitAction() };
    }

    public IReadOnlyList<ActionSlot> ActionSlotSlot => actionSlots.AsReadOnly();

    public void Play()
    {
        foreach (var actionSlot in actionSlots) actionSlot.Action.Play();
    }
}


/**
 * Grow and multiply your organisms to end up larger than your opponent.
 */
public class Game
{
    private static Arena arena;
    private static Resources MyResources;
    private static Resources OppResources;

    private static Resources ParseResources()
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        var myA = int.Parse(inputs[0]);
        var myB = int.Parse(inputs[1]);
        var myC = int.Parse(inputs[2]);
        var myD = int.Parse(inputs[3]); // your protein stock
        inputs = Console.ReadLine().Split(' ');
        var oppA = int.Parse(inputs[0]);
        var oppB = int.Parse(inputs[1]);
        var oppC = int.Parse(inputs[2]);
        var oppD = int.Parse(inputs[3]); // opponent's protein stock

        MyResources = new Resources(myA, myB, myC, myD);
        OppResources = new Resources(oppA, oppB, oppC, oppD);

        return MyResources;
    }

    private static Entity[] ParseEntities()
    {
        string[] inputs;
        var entityCount = int.Parse(Console.ReadLine());
        var entities = new Entity[entityCount];

        for (var i = 0; i < entityCount; i++)
        {
            inputs = Console.ReadLine().Split(' ');
            var x = int.Parse(inputs[0]);
            var y = int.Parse(inputs[1]); // grid coordinate
            var type = inputs[2]; // WALL, ROOT, BASIC, TENTACLE, HARVESTER, SPORER, A, B, C, D
            var owner = int.Parse(inputs[3]); // 1 if your organ, 0 if enemy organ, -1 if neither
            var organId = int.Parse(inputs[4]); // id of this entity if it's an organ, 0 otherwise
            var organDir = inputs[5]; // N,E,S,W or X if not an organ
            var organParentId = int.Parse(inputs[6]);
            var organRootId = int.Parse(inputs[7]);

            entities[i] = new Entity
            {
                X = x,
                Y = y,
                EntityType = type,
                Owner = owner,
                OrganId = organId,
                OrganDir = organDir,
                OrganParentId = organParentId,
                OrganRootId = organRootId
            };
        }

        return entities;
    }

    private static void InitializeArena()
    {
        string[] inputs;
        inputs = Console.ReadLine().Split(' ');
        var width = int.Parse(inputs[0]); // columns in the game grid
        var height = int.Parse(inputs[1]); // rows in the game grid
        arena = new Arena(width, height);
    }

    private static void Main(string[] args)
    {
        string[] inputs;

        InitializeArena();

        // game loop
        while (true)
        {
            var entities = ParseEntities();
            arena.UpdateArenaWithEntities(entities);
            ParseResources();

            var requiredActionsCount =
                int.Parse(Console.ReadLine()); // your number of organisms, output an action for each one in any order

            var actionPlayer = new ActionPlayer(requiredActionsCount);
            actionPlayer = new Strategies(MyResources, OppResources, arena, actionPlayer).AggresiveStrateyV2();
            actionPlayer.Play();
        }
    }
}