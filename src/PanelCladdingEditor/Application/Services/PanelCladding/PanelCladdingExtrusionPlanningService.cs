using System.Globalization;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingExtrusionPlanningService
{
    public const string PanelSurfaceLayerRootPath = "01_CW Panels::Surfaces-PNL";
    public const string CurveLayerRootPath = "02_CW Extrusions::Curves-PNL";
    public const string CurveUserTextKey = "CRV";
    public const string AssignedExtrusionsUserTextKey = "Extrusions";
    public static readonly PanelColorRgb MainFrameObjectColor = new(0, 0, 255);
    public static readonly PanelColorRgb HorizontalObjectColor = new(128, 0, 128);
    public static readonly PanelColorRgb VerticalObjectColor = new(0, 100, 0);
    private readonly PanelFrameProfileFormulaService _formulas = new();

    public OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>> CreatePlan(
        string panelId,
        string panelCid,
        double panelWidth,
        double panelHeight,
        PanelCladdingKeySet keySet,
        string sourcePanelLayerPath,
        string? releaseNumber = null)
    {
        if (string.IsNullOrWhiteSpace(panelId) || string.IsNullOrWhiteSpace(panelCid))
        {
            return OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>>.Fail(
                "PANEL_CLADDING_EXTRUSION_PANEL_METADATA_REQUIRED");
        }
        if (panelWidth <= 0d || panelHeight <= 0d)
        {
            return OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>>.Fail(
                "PANEL_CLADDING_EXTRUSION_PANEL_EXTENT_INVALID");
        }

        OperationResponse<string> curveLayer = ResolveCurveLayerPath(sourcePanelLayerPath);
        if (!curveLayer.Success || curveLayer.Data is null)
        {
            return OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>>.Fail(
                curveLayer.Message);
        }

        var curves = new List<PanelCladdingExtrusionCurvePlan>
        {
            Frame("FRM_0", PanelCladdingTopologyAxis.Horizontal, 0d, panelWidth),
            Frame("FRM_1", PanelCladdingTopologyAxis.Horizontal, panelHeight, panelWidth),
            Frame("FRM_2", PanelCladdingTopologyAxis.Vertical, 0d, panelHeight),
            Frame("FRM_3", PanelCladdingTopologyAxis.Vertical, panelWidth, panelHeight)
        };
        double[] xCuts = [0d, .. keySet.VerticalOffsets, panelWidth];
        double[] yCuts = [0d, .. keySet.HorizontalOffsets, panelHeight];
        var missing = keySet.Topology.MissingSegments.ToHashSet();
        var hidden = keySet.Topology.HiddenSegments.ToHashSet();
        var suppressed = missing.Union(hidden).ToHashSet();
        var claimed = new HashSet<PanelCladdingSegmentCoordinate>();

        foreach (PanelCladdingMergeRun run in keySet.Topology.MergeRuns
            .OrderBy(run => run.Axis)
            .ThenBy(run => run.Track)
            .ThenBy(run => run.StartBay))
        {
            PanelCladdingSegmentCoordinate[] atoms = Enumerable
                .Range(run.StartBay, run.EndBay - run.StartBay + 1)
                .Select(bay => new PanelCladdingSegmentCoordinate(run.Axis, run.Track, bay))
                .Where(segment => !suppressed.Contains(segment))
                .ToArray();
            if (atoms.Length < 2)
            {
                continue;
            }
            foreach (PanelCladdingSegmentCoordinate atom in atoms)
            {
                claimed.Add(atom);
            }
            double[] cuts = run.Axis == PanelCladdingTopologyAxis.Horizontal ? xCuts : yCuts;
            double offset = run.Axis == PanelCladdingTopologyAxis.Horizontal
                ? keySet.HorizontalOffsets[run.Track]
                : keySet.VerticalOffsets[run.Track];
            curves.Add(Curve(
                BuildMergedCode(run, cuts.Length - 1),
                PanelCladdingExtrusionCurveKind.Merged,
                run.Axis,
                offset,
                cuts[run.StartBay],
                cuts[run.EndBay + 1],
                atoms));
        }

        AddAtomicCurves(
            curves,
            PanelCladdingTopologyAxis.Horizontal,
            keySet.HorizontalOffsets,
            xCuts,
            suppressed,
            claimed);
        AddAtomicCurves(
            curves,
            PanelCladdingTopologyAxis.Vertical,
            keySet.VerticalOffsets,
            yCuts,
            suppressed,
            claimed);

        string pid = panelId.Trim();
        string cid = panelCid.Trim();
        var attributed = new List<PanelCladdingExtrusionCurvePlan>();
        foreach (PanelCladdingExtrusionCurvePlan curve in curves)
        {
            string curveCid = $"{cid}-{curve.Code}";
            IReadOnlyList<string> assignedCodes = PanelFrameAssignmentService.CodesForCurve(
                keySet.FrameAssignments,
                curve.Code,
                curve.AtomicSegments);
            double modifier = PanelFrameAssignmentService.ModifierForCurve(
                keySet.FrameAssignments,
                curve.Code,
                curve.AtomicSegments);
            var assignedValues = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string assignedCode in assignedCodes)
            {
                if (!keySet.FrameAssignments.Definitions.TryGetValue(
                        assignedCode,
                        out PanelFrameProfileDefinition? definition))
                {
                    if (!assignedCode.StartsWith("1D-", StringComparison.OrdinalIgnoreCase))
                    {
                        return OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>>.Fail(
                            $"PANEL_FRAME_PROFILE_DEFINITION_MISSING: {assignedCode}");
                    }
                    string baseCode = assignedCode[3..];
                    definition = new PanelFrameProfileDefinition
                    {
                        Code = assignedCode,
                        BaseCode = baseCode.StartsWith("ALU-", StringComparison.OrdinalIgnoreCase)
                            ? baseCode[4..]
                            : baseCode,
                        SourceCode = baseCode,
                        Dimension = PanelFrameProfileDimension.OneDimensional,
                        Calculation = PanelFrameProfileCalculation.Length,
                        CalculationValue = 1d
                    };
                }
                OperationResponse<string> formula = _formulas.CreateFormula(definition, modifier);
                if (!formula.Success || formula.Data is null)
                {
                    return OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>>.Fail(formula.Message);
                }
                assignedValues[assignedCode] = formula.Data;
            }
            var userText = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = pid,
                [PanelCladdingSpawnPlanningService.CidUserTextKey] = curveCid,
                [CurveUserTextKey] = curve.Code
            };
            if (!string.IsNullOrWhiteSpace(releaseNumber))
            {
                userText[PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = releaseNumber.Trim();
            }
            if (assignedCodes.Count > 0)
            {
                userText[AssignedExtrusionsUserTextKey] = string.Join(';', assignedCodes);
                foreach ((string code, string formula) in assignedValues)
                {
                    userText[code] = formula;
                }
            }
            attributed.Add(new PanelCladdingExtrusionCurvePlan
            {
                Code = curve.Code,
                Kind = curve.Kind,
                Axis = curve.Axis,
                Offset = curve.Offset,
                Start = curve.Start,
                End = curve.End,
                AtomicSegments = curve.AtomicSegments,
                AssignedExtrusionCodes = assignedCodes,
                AssignedExtrusionValues = assignedValues,
                Cid = curveCid,
                LayerPath = curveLayer.Data,
                ObjectColor = ResolveObjectColor(curve.Kind, curve.Axis),
                UserTextWrites = userText
            });
        }
        return OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>>.Ok(attributed);
    }

    public static PanelColorRgb ResolveObjectColor(
        PanelCladdingExtrusionCurveKind kind,
        PanelCladdingTopologyAxis axis) =>
        kind == PanelCladdingExtrusionCurveKind.Frame
            ? MainFrameObjectColor
            : axis == PanelCladdingTopologyAxis.Horizontal
                ? HorizontalObjectColor
                : VerticalObjectColor;

    public static OperationResponse<string> ResolveCurveLayerPath(string sourcePanelLayerPath)
    {
        string source = (sourcePanelLayerPath ?? string.Empty).Trim();
        string requiredPrefix = PanelSurfaceLayerRootPath + "::";
        if (!source.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_PANEL_LAYER_NOT_SUPPORTED: {sourcePanelLayerPath}");
        }

        string suffix = source[requiredPrefix.Length..];
        if (suffix.Length == 0 || suffix.Split(
                new[] { "::" },
                StringSplitOptions.None).Any(string.IsNullOrWhiteSpace))
        {
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_PANEL_TYPE_LAYER_REQUIRED: {sourcePanelLayerPath}");
        }

        return OperationResponse<string>.Ok($"{CurveLayerRootPath}::{suffix}");
    }

    private static void AddAtomicCurves(
        ICollection<PanelCladdingExtrusionCurvePlan> curves,
        PanelCladdingTopologyAxis axis,
        IReadOnlyList<double> offsets,
        IReadOnlyList<double> cuts,
        IReadOnlySet<PanelCladdingSegmentCoordinate> missing,
        IReadOnlySet<PanelCladdingSegmentCoordinate> claimed)
    {
        for (int track = 0; track < offsets.Count; track++)
        {
            for (int bay = 0; bay < cuts.Count - 1; bay++)
            {
                var segment = new PanelCladdingSegmentCoordinate(axis, track, bay);
                if (missing.Contains(segment) || claimed.Contains(segment))
                {
                    continue;
                }
                string code = axis == PanelCladdingTopologyAxis.Horizontal
                    ? $"INT_{Alpha(track)}{bay.ToString(CultureInfo.InvariantCulture)}"
                    : $"INT_{track.ToString(CultureInfo.InvariantCulture)}{Alpha(bay)}";
                curves.Add(Curve(
                    code,
                    PanelCladdingExtrusionCurveKind.Segment,
                    axis,
                    offsets[track],
                    cuts[bay],
                    cuts[bay + 1],
                    [segment]));
            }
        }
    }

    private static string BuildMergedCode(PanelCladdingMergeRun run, int bayCount)
    {
        string track = run.Axis == PanelCladdingTopologyAxis.Horizontal
            ? Alpha(run.Track)
            : run.Track.ToString(CultureInfo.InvariantCulture);
        if (run.StartBay == 0 && run.EndBay == bayCount - 1)
        {
            return $"INT_{track}";
        }
        string start = run.Axis == PanelCladdingTopologyAxis.Horizontal
            ? run.StartBay.ToString(CultureInfo.InvariantCulture)
            : Alpha(run.StartBay);
        string end = run.Axis == PanelCladdingTopologyAxis.Horizontal
            ? run.EndBay.ToString(CultureInfo.InvariantCulture)
            : Alpha(run.EndBay);
        return $"INT_{track}{start}–{end}";
    }

    private static PanelCladdingExtrusionCurvePlan Frame(
        string code,
        PanelCladdingTopologyAxis axis,
        double offset,
        double end) =>
        Curve(code, PanelCladdingExtrusionCurveKind.Frame, axis, offset, 0d, end, []);

    private static PanelCladdingExtrusionCurvePlan Curve(
        string code,
        PanelCladdingExtrusionCurveKind kind,
        PanelCladdingTopologyAxis axis,
        double offset,
        double start,
        double end,
        IReadOnlyList<PanelCladdingSegmentCoordinate> atoms) => new()
        {
            Code = code,
            Kind = kind,
            Axis = axis,
            Offset = offset,
            Start = start,
            End = end,
            AtomicSegments = atoms
        };

    public static string Alpha(int index)
    {
        string result = string.Empty;
        int value = index + 1;
        while (value > 0)
        {
            value--;
            result = (char)('A' + value % 26) + result;
            value /= 26;
        }
        return result;
    }
}
