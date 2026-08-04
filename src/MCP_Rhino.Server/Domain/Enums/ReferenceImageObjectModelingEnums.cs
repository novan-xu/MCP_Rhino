namespace MCP_Rhino.Server.Domain.Enums;

public enum ReferenceImageObjectEdgeCharacter
{
    Unknown,
    Hard,
    Soft,
    Mixed
}

public enum ReferenceImagePrimitiveVocabularyKind
{
    Unknown,
    Box,
    RoundedBox,
    Sphere,
    Ellipsoid,
    Cylinder,
    Cone,
    Capsule,
    Pipe,
    Torus,
    PlanarPanel,
    ProfileExtrusion,
    Sweep,
    Loft,
    TaperedBox,
    SubD,
    BooleanCutter,
    RaisedStrip,
    DecalPlane,
    MaterialOnly
}

public enum ReferenceImagePartRole
{
    PrimaryMass,
    StructuralMass,
    AdditiveDetail,
    SubtractiveDetail,
    SurfaceDetail,
    MaterialOnly,
    Reference
}

public enum ReferenceImageToolFamily
{
    None,
    GeneralPrimitive,
    CurveOps,
    GeometryEdit,
    Boolean,
    SubD,
    Material,
    Viewport
}

public enum ReferenceImageMaterialIntent
{
    Unknown,
    Matte,
    Satin,
    Glossy,
    Transparent,
    Metallic,
    Fabric,
    Plastic,
    Wood,
    Stone,
    Rubber,
    Emissive
}

public enum ReferenceImageRefinementActionKind
{
    SoftenEdge,
    AddRaisedStrip,
    AddGroove,
    AddCutout,
    AddHandleOrSupport,
    AddRepeatedDetail,
    AddDecalOrLabel,
    UseMaterialInstead
}

public enum ReferenceImageIterationDecisionKind
{
    ReviseMassing,
    ReviseDetail,
    ReviseMaterial,
    Accept,
    StopAndReportGap
}

public enum ReferenceImageObjectModelingStatus
{
    NotStarted,
    Completed,
    NeedsIteration,
    PartialCompleted,
    CapabilityGap,
    Failed
}

public enum ReferenceImageCapabilityGapLayer
{
    Tool,
    Skill,
    Agent,
    Resource,
    ExternalConnector,
    LiveRhino
}
