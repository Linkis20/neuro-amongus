﻿using System;
using System.Collections.Generic;
using System.Linq;
using Neuro.Pathfinding.DataStructures;
using UnityEngine;

namespace Neuro.Pathfinding;

public sealed class PathfindingHandler
{
    internal float gridDensity = 5f; // TODO: Fine-tune individual maps to optimize performance
    internal int gridBaseWidth = 100;

    private int _gridSize = 0;
    private int _gridLowerBounds = 0;
    private int _gridUpperBounds = 0;

    private Node[,] _grid;

    public void Initialize()
    {
        _gridSize = (int)(gridBaseWidth * gridDensity);
        _gridLowerBounds = _gridSize / -2;
        _gridUpperBounds = _gridSize / 2;

        GenerateNodeGrid();
        FloodFill(ShipStatus.Instance.MeetingSpawnCenter + Vector2.down * ShipStatus.Instance.SpawnRadius);
    }

    public Vector2[] FindPath(Vector2 start, Vector2 target)
    {
        bool pathSuccess = false;

        /*GameObject test = new GameObject("Test");
        //Info(test.transform);
        test.transform.position = (Vector3)start;

        LineRenderer renderer = test.AddComponent<LineRenderer>();
        renderer.SetPosition(0, start);
        renderer.SetPosition(1, (Vector3)target + new Vector3(0, 0.1f, 0));
        renderer.widthMultiplier = 0.2f;
        renderer.positionCount = 2;
        renderer.startColor = Color.blue;*/

        Info("Start Node");
        Node startNode = FindClosestNode(start);
        Info("End Node");
        Node targetNode = FindClosestNode(target);

        Vector3 targetNodePosition = targetNode.worldPosition;

        GameObject endNodeObj = new("Task Visual Point")
        {
            transform =
            {
                position = targetNodePosition
            }
        };

        LineRenderer renderer2 = endNodeObj.AddComponent<LineRenderer>();
        renderer2.SetPosition(0, targetNodePosition);
        renderer2.SetPosition(1, targetNodePosition + new Vector3(0f, 0.3f, 0));
        renderer2.widthMultiplier = 0.3f;
        renderer2.positionCount = 2;
        renderer2.material = new Material(Shader.Find("Unlit/MaskShader"));
        renderer2.startColor = Color.blue;
        renderer2.endColor = Color.blue;

        Info($"startNode: {startNode.worldPosition} targetNode: {targetNode.worldPosition}");

        if (startNode is not { accessible: true } || targetNode is not { accessible: true }) return Array.Empty<Vector2>();

        Heap<Node> openSet = new(_gridSize * _gridSize);
        HashSet<Node> closedSet = new();

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet.RemoveFirst();

            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                pathSuccess = true;
                break;
            }

            foreach (Node neighbour in GetNeighbours(currentNode))
            {
                if (!neighbour.accessible || closedSet.Contains(neighbour)) continue;

                int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);

                /*GameObject nodeGO = new GameObject("Test");
                //Info(test.transform);
                nodeGO.transform.position = (Vector3)currentNode.worldPosition;

                LineRenderer nodeRenderer = nodeGO.AddComponent<LineRenderer>();
                nodeRenderer.SetPosition(0, (Vector3)currentNode.worldPosition);
                nodeRenderer.SetPosition(1, (Vector3)currentNode.worldPosition + new Vector3(0, 0.1f, 0));
                nodeRenderer.widthMultiplier = 0.1f;
                nodeRenderer.positionCount = 2;
                nodeRenderer.startColor = Color.red;*/

                if (newMovementCostToNeighbour >= neighbour.gCost && openSet.Contains(neighbour)) continue;

                neighbour.gCost = newMovementCostToNeighbour;
                neighbour.hCost = GetDistance(neighbour, targetNode);
                neighbour.parent = currentNode;

                if (!openSet.Contains(neighbour))
                    openSet.Add(neighbour);
                else
                    openSet.UpdateItem(neighbour);
            }
        }

        if (pathSuccess)
        {
            Info("Path found successfully.");
            return RetracePath(startNode, targetNode);
        }

        Warning("Failed to find path");
        return Array.Empty<Vector2>();
    }

    private void GenerateNodeGrid()
    {
        _grid = new Node[_gridSize, _gridSize];

        float nodeRadius = 1 / gridDensity;

        const float OFFSET = 1 / 5f; // Must be less than 1 / 4f or it will flood fill through walls
        Vector2[] offsetCoords =
        {
            new(-OFFSET, -OFFSET), new(0, -OFFSET), new(OFFSET, -OFFSET),
            new(-OFFSET, 0), Vector2.zero, new(OFFSET, 0),
            new(-OFFSET, OFFSET), new(0, OFFSET), new(OFFSET, OFFSET)
        };

        for (int x = _gridLowerBounds; x < _gridUpperBounds; x++)
        for (int y = _gridLowerBounds; y < _gridUpperBounds; y++)
        {
            Vector2 point = Vector2.zero;
            bool accessible = false;
            for (int i = 0; i < 9; i++)
            {
                var b = (i * 4 + 4) % 9; // Noncontinuous linear index through the array
                if (IsAccessible(x + offsetCoords[b].x, y + offsetCoords[b].y, out point))
                {
                    accessible = true;
                    break;
                }
            }

            _grid[x + _gridUpperBounds, y + _gridUpperBounds] = new Node(accessible, point, x + _gridUpperBounds, y + _gridUpperBounds);
        }

        bool IsAccessible(float x, float y, out Vector2 point)
        {
            point = new Vector2(x / gridDensity, y / gridDensity);

            Collider2D[] cols = Physics2D.OverlapCircleAll(point, nodeRadius, LayerMask.GetMask("Ship", "ShortObjects"));
            int validColsCount = cols.Count(col =>
                !col.isTrigger &&
                !col.GetComponentInParent<Vent>() &&
                !col.GetComponentInParent<SomeKindaDoor>()
            );

            // TODO: Add edge case for Airship ladders

            return validColsCount == 0;
        }
    }

    private void FloodFill(Vector2 accessiblePosition)
    {
        Node startingNode = NodeFromWorldPoint(accessiblePosition);

        // Flood fill
        List<Node> openSet = new();
        HashSet<Node> closedSet = new();
        openSet.Add(startingNode);

        while (openSet.Count > 0)
        {
            Node node = openSet[0];
            openSet.Remove(node);
            closedSet.Add(node);

            foreach (Node neighbour in GetNeighbours(node))
            {
                if (!neighbour.accessible || closedSet.Contains(neighbour)) continue;
                if (!openSet.Contains(neighbour)) openSet.Add(neighbour);

                neighbour.parent = node;
            }
        }

        Material nodeMaterial = new(Shader.Find("Unlit/MaskShader"));
        foreach (Node node in closedSet.ToList())
        {
            Vector3 nodePosition = node.worldPosition;

            GameObject nodeVisualPoint = new("Node Visual Point")
            {
                transform =
                {
                    position = nodePosition
                }
            };

            LineRenderer renderer = nodeVisualPoint.AddComponent<LineRenderer>();
            renderer.SetPosition(0, nodePosition);
            renderer.SetPosition(1, nodePosition + new Vector3(0, 0.1f, 0));
            renderer.widthMultiplier = 0.1f;
            renderer.positionCount = 2;
            renderer.material = nodeMaterial;
            renderer.startColor = Color.red;
            renderer.endColor = Color.red;
        }

        // Set all nodes not in closed set to inaccessible
        foreach (Node node in _grid)
        {
            if (!closedSet.Contains(node)) node.accessible = false;
        }
    }

    private Node NodeFromWorldPoint(Vector2 position)
    {
        position *= gridDensity;
        float clampedX = Mathf.Clamp(position.x, _gridLowerBounds, _gridUpperBounds);
        float clampedY = Mathf.Clamp(position.y, _gridLowerBounds, _gridUpperBounds);

        int xIndex = Mathf.RoundToInt(clampedX + _gridUpperBounds);
        int yIndex = Mathf.RoundToInt(clampedY + _gridUpperBounds);

        return _grid[xIndex, yIndex];
    }

    private Node FindClosestNode(Vector2 position)
    {
        Node closestNode = NodeFromWorldPoint(position);

        Queue<Node> queue = new();
        List<Node> nodes = new();
        queue.Enqueue(closestNode);
        nodes.Add(closestNode);

        while (!closestNode.accessible)
        {
            closestNode = queue.Dequeue();
            float closestDistance = Mathf.Infinity;
            Node closestNeighbour = null;
            foreach (Node neighbour in GetNeighbours(closestNode))
            {
                if (neighbour.accessible)
                {
                    float distance = Vector2.Distance(position, neighbour.worldPosition);
                    if (distance < closestDistance)
                    {
                        closestNeighbour = neighbour;
                        closestDistance = distance;
                    }
                }
                else
                {
                    if (!nodes.Contains(neighbour))
                    {
                        queue.Enqueue(neighbour);
                        nodes.Add(neighbour);
                    }
                }
            }

            if (closestNeighbour != null) closestNode = closestNeighbour;
        }

        return closestNode;
    }

    private List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new();

        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        {
            if (x == 0 && y == 0)
                continue;

            int checkX = node.gridX + x;
            int checkY = node.gridY + y;

            if (checkX >= 0 && checkX < _gridSize && checkY >= 0 && checkY < _gridSize) neighbours.Add(_grid[checkX, checkY]);
        }

        return neighbours;
    }

    private Vector2[] RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new();
        Node currentNode = endNode;
        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        Vector2[] waypoints = path.Select(p => p.worldPosition).ToArray();
        new Span<Vector2>(waypoints).Reverse();

        return waypoints;
    }

    private void DrawPath(Vector2[] path)
    {
        // TODO: Cache old path instead of destroying it by name
        GameObject.Destroy(GameObject.Find("Neuro Path"));
        GameObject test = new("Neuro Path");
        //Info(test.transform);
        test.transform.position = PlayerControl.LocalPlayer.transform.position;

        LineRenderer renderer = test.AddComponent<LineRenderer>();
        renderer.positionCount = path.Length;
        for (int i = 0; i < path.Length; i++)
        {
            Info(path[i].ToString());
            renderer.SetPosition(i, path[i]);
        }

        renderer.widthMultiplier = 0.2f;
        renderer.startColor = Color.blue;
    }

    private static int GetDistance(Node a, Node b)
    {
        int dstX = Mathf.Abs(a.gridX - b.gridX);
        int dstY = Mathf.Abs(a.gridY - b.gridY);

        return 14 * dstY + 10 * Math.Abs(dstX - dstY); // TODO: 14 magic number?
    }
}