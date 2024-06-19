#region Using directives
using UAManagedCore;
using FTOptix.HMIProject;
using FTOptix.Core;
using FTOptix.NetLogic;
using FTOptix.CommunicationDriver;
using System.Linq;
using FTOptix.Alarm;
using System;
using FTOptix.S7TCP;
using FTOptix.EventLogger;
using FTOptix.OPCUAServer;
#endregion

public class FromPLCToModel2 : BaseNetLogic
{
    private UAValue setDynamicLinks;
    private Folder destinationFolder;

    [ExportMethod]
    public void GenerateNodesIntoModel()
    {
        try
        {
            setDynamicLinks = LogicObject.GetVariable("SetDynamicLinks").Value;
            if (InformationModel.Get(LogicObject.GetVariable("DestinationFolder").Value)?.GetType() != typeof(Folder)) throw new TypeLoadException("Wrong type, Destination folder must be a Folder");
            destinationFolder = InformationModel.Get<Folder>(LogicObject.GetVariable("DestinationFolder").Value);
            IUANode startingNode = InformationModel.Get(LogicObject.GetVariable("InputNode").Value);
            CreateOrUpdate(startingNode, destinationFolder);
            CheckDatabinds();
        }
        catch (Exception ex) 
        {
            Log.Error(LogicObject.BrowseName, $"Error during generate model variable from plc tags: {ex.Message}");
        }
    }
    
    private void CreateOrUpdate(IUANode fieldNode, IUANode parentNode, string browseNamePrefix = "")
    {
        IUANode existingNode = GetChild(fieldNode, parentNode, browseNamePrefix);
        // Replacing "/" with "_". Nodes with browsename "/" are not allowed
        string filedNodeBrowseName = fieldNode.BrowseName.Replace("/", "_");
        if (existingNode == null)
        {
            switch (fieldNode.GetType())
            {
                case Type type when type.IsAssignableTo(typeof(Folder)):
                    existingNode = InformationModel.Make<Folder>(browseNamePrefix + filedNodeBrowseName);
                    break;
                case Type type when type.IsAssignableTo(typeof(FTOptix.CommunicationDriver.Tag)):
                    if (IsArrayDimentionsVar(fieldNode)) return;
                    IUAVariable mTag = (IUAVariable)fieldNode;
                    existingNode = InformationModel.MakeVariable(filedNodeBrowseName, mTag.DataType, mTag.ArrayDimensions);
                    if (!setDynamicLinks) break;
                    ((IUAVariable)existingNode).SetDynamicLink((UAVariable)fieldNode, FTOptix.CoreBase.DynamicLinkMode.ReadWrite);
                    break;
                case Type type when type.IsAssignableTo(typeof(FTOptix.CommunicationDriver.TagStructure)):
                    existingNode = InformationModel.MakeObject(browseNamePrefix + filedNodeBrowseName);
                    break;

            }
            parentNode.Add(existingNode);
        }
        if (fieldNode.GetType().IsAssignableTo(typeof(FTOptix.CommunicationDriver.Tag))) return;
        foreach (var t in fieldNode.Children.Where(c => !IsArrayDimentionsVar(c)))
        {
            CreateOrUpdate(t, existingNode);
        }
    }

    private void CheckDatabinds()
    {
        var lDataBinds = destinationFolder.FindNodesByType<IUAVariable>().Where<IUAVariable>(v => { return v.BrowseName == "DynamicLink"; });
        foreach (var vDataBind in lDataBinds)
        {
            var IsResolved = LogicObject.Context.ResolvePath(vDataBind.Owner, vDataBind.Value).ResolvedNode;
            if (IsResolved == null) { Log.Info($"{Log.Node(vDataBind.Owner)} has unresolved databind"); }
        }
    }

    static private bool IsTagStructureArray(IUANode fieldNode) => ((TagStructure)fieldNode).ArrayDimensions.Length != 0;

    private bool IsArrayDimentionsVar(IUANode n) => n.BrowseName.ToLower().Contains("arraydimen");

    private IUANode GetChild(IUANode child, IUANode parent, string browseNamePrefix = "") => parent.Children.FirstOrDefault(c => c.BrowseName == browseNamePrefix + child.BrowseName);

}

