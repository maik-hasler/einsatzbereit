using System.Reflection;

namespace ArchitectureTests;

internal static class AssemblyAnchors
{
    public const string DomainLayerAssemblyName = "Domain";
    
    public const string ApplicationLayerAssemblyName = "Application";
    
    public const string InfrastructureLayerAssemblyName = "Infrastructure";
    
    public const string PresentationLayerAssemblyName = "Api";
    
    public static readonly Assembly DomainLayer = Assembly.Load(DomainLayerAssemblyName);
    
    public static readonly Assembly ApplicationLayer = Assembly.Load(ApplicationLayerAssemblyName);
    
    public static readonly Assembly InfrastructureLayer = Assembly.Load(InfrastructureLayerAssemblyName);
    
    public static readonly Assembly PresentationLayer = Assembly.Load(PresentationLayerAssemblyName);
}