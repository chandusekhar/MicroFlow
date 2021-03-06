﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Annotations;

namespace MicroFlow.Graph
{
  using Bag = Dictionary<string, object>;

  public class FlowGraphBuilder : NodeVisitor
  {
    private static readonly XNamespace RootNamespace = "http://schemas.microsoft.com/vs/2009/dgml";

    private readonly XElement myNodes;
    private readonly XElement myLinks;

    public FlowGraphBuilder()
    {
      Result = new XElement(RootNamespace + "DirectedGraph");

      myNodes = new XElement(RootNamespace + "Nodes");
      myLinks = new XElement(RootNamespace + "Links");

      Result.Add(myNodes);
      Result.Add(myLinks);

      AddCategories();
    }

    [NotNull]
    public XElement Result { get; }

    [NotNull]
    public XElement GenerateDgml([NotNull] FlowDescription flowDescription)
    {
      if (flowDescription == null) throw new ArgumentNullException(nameof(flowDescription));

      foreach (var node in flowDescription.Nodes)
      {
        node.Accept(this);
      }

      return Result;
    }

    protected override void VisitActivity<TActivity>(ActivityNode<TActivity> activityNode)
    {
      AddNode(
        activityNode.Id,
        activityNode.Name ?? typeof (TActivity).Name,
        activityNode.ToCategory(),
        new Bag {["ActivityType"] = typeof (TActivity).Name});

      AddLink(activityNode, activityNode.PointsTo, Categories.NormalFlowEdge);
      AddLink(activityNode, activityNode.FaultHandler, Categories.FaultFlowEdge);
      AddLink(activityNode, activityNode.CancellationHandler, Categories.CancellationFlowEdge);
    }

    protected override void VisitSwitch<TChoice>(SwitchNode<TChoice> switchNode)
    {
      AddNode(switchNode);

      AddLink(switchNode, switchNode.DefaultCase, "Default");

      foreach (var valueNodePair in switchNode.Cases)
      {
        AddLink(switchNode, valueNodePair.Value, Categories.NormalFlowEdge, valueNodePair.Key?.ToString() ?? "");
      }
    }

    protected override void VisitCondition(ConditionNode conditionNode)
    {
      AddNode(conditionNode);

      AddLink(conditionNode, conditionNode.WhenFalse, Categories.NormalFlowEdge, "False");
      AddLink(conditionNode, conditionNode.WhenTrue, Categories.NormalFlowEdge, "True");
    }

    protected override void VisitForkJoin(ForkJoinNode forkJoinNode)
    {
      AddNode(forkJoinNode);

      foreach (var fork in forkJoinNode.Forks)
      {
        AddNode(
          fork.Id, fork.Name, Categories.ForkNode,
          new Bag {["ActivityType"] = fork.ActivityType.Name});

        AddLink(forkJoinNode.Id, fork.Id, Categories.ParallelFlowEdge);

        AddLink(fork.Id, forkJoinNode.PointsTo?.Id, Categories.NormalFlowEdge);
        AddLink(fork.Id, forkJoinNode.FaultHandler?.Id, Categories.FaultFlowEdge);
        AddLink(fork.Id, forkJoinNode.CancellationHandler?.Id, Categories.CancellationFlowEdge);
      }
    }

    protected override void VisitBlock(BlockNode blockNode)
    {
      AddNode(blockNode, new Bag {["Group"] = "Expanded"});

      foreach (var innerNode in blockNode.InnerNodes)
      {
        AddLink(blockNode, innerNode, "Contains");
      }

      AddLink(blockNode, blockNode.PointsTo, Categories.NormalFlowEdge);
    }

    private void AddNode(IFlowNode node, Bag properties = null)
    {
      AddNode(node.Id, node.Name, node.ToCategory(), properties);
    }

    private void AddNode(Guid id, string name, string category = null, Dictionary<string, object> properties = null)
    {
      var node = new XElement(
        RootNamespace + "Node",
        new XAttribute("Id", id),
        new XAttribute("Label", name ?? ""),
        properties.ToAttributes());

      node.SetOptionalAttribute("Category", category);

      myNodes.Add(node);
    }

    private void AddLink(
      [NotNull] IFlowNode from,
      [CanBeNull] IFlowNode to,
      [CanBeNull] string category = null, [CanBeNull] string label = null)
    {
      AddLink(from.Id, to?.Id, category, label);
    }

    private void AddLink(Guid? from, Guid? to, string category = null, string label = null)
    {
      if (from == null || to == null) return;

      var link = new XElement(
        RootNamespace + "Link",
        new XAttribute("Source", from),
        new XAttribute("Target", to));

      link.SetOptionalAttribute("Category", category);
      link.SetOptionalAttribute("Label", label);

      myLinks.Add(link);
    }

    private void AddCategories()
    {
      var nodeCategories = Categories
        .NodeCategories
        .Select(c => new XElement(
          RootNamespace + "Category",
          new XAttribute("Id", c),
          new XAttribute("Background", Background.OfCategory(c))));

      var categories = new XElement(RootNamespace + "Categories", nodeCategories);

      Result.Add(categories);
    }
  }
}