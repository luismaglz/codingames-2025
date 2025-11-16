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

internal class Arena
{
    private Node[] grid;
    private int HEIGHT;
    private int WIDTH;

    public Arena(int width, int height)
    {
        WIDTH = width;
        HEIGHT = height;
    }
}

internal class Node
{
    public int Id;
    public string Type;
    public int X;
    public int Y;
}

internal class Entity
{
    public string EntityType;
    public string OrganDir;
    public int OrganId;
    public int OrganParentId;
    public int OrganRootId;
    public int Owner;
    public int X;
    public int Y;
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

public class ActionPlayer
{
    private readonly Action[] actions;

    public ActionPlayer(int requiredActionsCount)
    {
        actions = new Action[requiredActionsCount];
        for (var i = 0; i < requiredActionsCount; i++) actions[i] = new WaitAction();
    }

    public IReadOnlyList<Action> Actions => actions;

    public void SetAction(int index, Action action)
    {
        if (index >= 0 && index < actions.Length && action != null) actions[index] = action;
    }

    public void Play()
    {
        foreach (var action in actions) action.Play();
    }
}

/**
 * Grow and multiply your organisms to end up larger than your opponent.
 */
internal class Game
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
    }

    private static void Main(string[] args)
    {
        string[] inputs;

        InitializeArena();

        // game loop
        while (true)
        {
            var entities = ParseEntities();
            ParseResources();
            var requiredActionsCount =
                int.Parse(Console.ReadLine()); // your number of organisms, output an action for each one in any order

            var actionPlayer = new ActionPlayer(requiredActionsCount);
            actionPlayer.Play();
        }
    }
}