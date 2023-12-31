﻿using Codecool.MarsExploration.MapGenerator.Calculators.Model;
using Codecool.MarsExploration.MapExplorer.Extensions;
using Codecool.MarsExploration.MapExplorer.MarsRover.Model;
using Codecool.MarsExploration.MapExplorer.Logger;
using Codecool.MarsExploration.MapExplorer.CommandCenter.Services.AssemblingRoutine;

namespace Codecool.MarsExploration.MapExplorer.CommandCenter.Model;

public class CommandCenter
{
    public string Id { get; }
    public Coordinate Position { get; init; }
    public int Radius { get; init; }
    public List<Coordinate> AdjacentCoordinates { get; init; }
    public int BuildProgress { get; set; }
    public int AssemblyProgress { get; set; }
    public List<ResourceNode> ResourceNodes { get; init; }
    public Dictionary<string, int> Resources { get; set; }
    public Dictionary<string, int> TotalCollectedResources { get; set; }
    public bool ExploringRoverNeeded { get; private set; }
    public CommandCenterStatus CommandCenterStatus { get; set; }
    private readonly IAssemblyRoutine _assemblyRoutine;

    public CommandCenter(
        int id,
        Rover builderRover,
        Coordinate position,
        int radius,
        int mapDimension,
        Dictionary<string, HashSet<Coordinate>> discoveredResources,
        //List<ResourceNode> resourceNodes, 
        bool exploringRoverNeeded,
        IAssemblyRoutine assemblyRoutine)
    {
        Id = $"base-{id}";
        Position = position;
        Radius = radius;
        Resources = new Dictionary<string, int>(); //Resources in Inventory
        AdjacentCoordinates = position.GetAdjacentCoordinates(mapDimension, radius).ToList();
        ResourceNodes = GetResourcesInSight(discoveredResources);
        TotalCollectedResources = new Dictionary<string, int>();
        ExploringRoverNeeded = exploringRoverNeeded;
        BuildProgress = 0;
        _assemblyRoutine = assemblyRoutine;
        AssemblyProgress = 0;
        CommandCenterStatus = CommandCenterStatus.UnderConstruction;
        AssignResourceAndCommandCenterToTheRover(builderRover);
    }

    public void AssignResourceAndCommandCenterToTheRover(Rover rover)
    {
        rover.AssignCommandCenter(this);
        AssignResourceNodeToRover(rover);
    }

    public void AddToResources(Dictionary<string, int> resources)
    {
        foreach (var resource in resources)
        {
            if (!Resources.ContainsKey(resource.Key))
            {
                Resources.Add(resource.Key, resource.Value);
            }
            else
            {
                Resources[resource.Key] += resource.Value;
            }
            AddToTotalCollectedResources(resource);
        }
    }

    private void AddToTotalCollectedResources(KeyValuePair<string, int> resource)
    {
        if (!TotalCollectedResources.ContainsKey(resource.Key))
        {
            TotalCollectedResources.Add(resource.Key, resource.Value);
        }
        else
        {
            TotalCollectedResources[resource.Key] += resource.Value;
        }
    }


    public void AssignResourceNodeToRover(Rover rover) //rover has built => run
    {
        var mineralResource = ResourceNodes.Count(r => r.HasRoverAssinged == true) == 0
            ? ResourceNodes.First(x => x.Type == "%")
            : ResourceNodes.First(x => x.HasRoverAssinged == false);

        rover.AssignResourceNode(mineralResource);
        mineralResource.HasRoverAssinged = true;
    }

    public Rover? UpdateStatus(int roverCost)
    {
        int Minerals = Resources["%"];

        if (ResourceNodes.Any(x => !x.HasRoverAssinged) && Minerals >= roverCost)
        {
            return AssembleRover(roverCost, false);
        }

        else if (ExploringRoverNeeded && Minerals >= roverCost)
        {
            var assemblyStatus = AssembleRover(roverCost, true);
            if (assemblyStatus != null)
            {
                ExploringRoverNeeded = false;
            }
            return assemblyStatus;
        }

        CommandCenterStatus = CommandCenterStatus.Idle;
        return null;
    }


    public bool IsConstructable(int resourceNeeded)
    {
        int totalResource = 0;
        Resources.TryGetValue("%", out totalResource);
        return CommandCenterStatus == CommandCenterStatus.UnderConstruction && resourceNeeded <= totalResource;
    }


    private List<ResourceNode> GetResourcesInSight(Dictionary<string, HashSet<Coordinate>> discoveredResources)
    {
        List<ResourceNode> resources = new List<ResourceNode>();
        foreach (var discResource in discoveredResources)
        {
            foreach (Coordinate coord in AdjacentCoordinates)
            {
                if (discResource.Value.Any(x => x.X == coord.X && x.Y == coord.Y))
                {
                    resources.Add(new ResourceNode(discResource.Key, coord, false));
                }
            }
        }
        return resources;
    }

    private Rover? AssembleRover(int roverCost, bool exploring)
    {
        CommandCenterStatus = CommandCenterStatus.RoverProduction;
        var roverAssemblyStatus = _assemblyRoutine.Assemble(this);
        if (roverAssemblyStatus != null)
        {
            Resources["%"] -= roverCost;

            if (!exploring)
            {
                AssignResourceAndCommandCenterToTheRover(roverAssemblyStatus);
            }

            CommandCenterStatus = CommandCenterStatus.Idle;
        }

        return roverAssemblyStatus;
    }
}