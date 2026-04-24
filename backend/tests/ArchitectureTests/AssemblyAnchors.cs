using System.Reflection;

namespace ArchitectureTests;

internal static class AssemblyAnchors
{
    public const string DomainLayerAssemblyName = "Domain";
    
    public const string ApplicationLayerAssemblyName = "Application";
    
    public const string InfrastructureLayerAssemblyName = "Infrastructure";
    
    public const string PresentationLayerAssemblyName = "Api";
    
    public static readonly Assembly DomainLayer = System.Reflection.Assembly.Load(DomainLayerAssemblyName);
    
    public static readonly Assembly ApplicationLayer = System.Reflection.Assembly.Load(ApplicationLayerAssemblyName);
    
    public static readonly Assembly InfrastructureLayer = System.Reflection.Assembly.Load(InfrastructureLayerAssemblyName);
    
    public static readonly Assembly PresentationLayer = System.Reflection.Assembly.Load(PresentationLayerAssemblyName);
}